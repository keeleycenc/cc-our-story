// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Core.Time;
using OurStory.Data;

namespace OurStory.Services.Notifications;

internal sealed class NotificationService(
    OurStoryDbContext db,
    IWebPushSender sender,
    IEnumerable<INotificationChannel> channels,
    ActiveConfiguration configuration,
    SiteClock clock,
    ILogger<NotificationService> logger) : INotificationService {
    private readonly IReadOnlyList<INotificationChannel> _channels = [.. channels];

    public bool IsConfigured => sender.IsConfigured;

    public bool IsEmailConfigured =>
        _channels.Any(channel => channel.Kind == NotificationChannelKind.Email && channel.IsConfigured);

    public bool HasConfiguredChannel => _channels.Any(channel => channel.IsConfigured);

    public string PublicKey => sender.PublicKey;

    public async Task<NotificationSetting> GetSettingAsync(int userId, CancellationToken cancellationToken = default) {
        var setting = await db.NotificationSettings
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (setting is not null) {
            return setting;
        }

        setting = new NotificationSetting { UserId = userId, UpdatedAt = SiteClock.UtcNow };
        _ = db.NotificationSettings.Add(setting);
        _ = await db.SaveChangesAsync(cancellationToken);
        return setting;
    }

    public async Task SaveSettingAsync(int userId, NotificationPreferences preferences, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(preferences);

        var setting = await GetSettingAsync(userId, cancellationToken);

        setting.Enabled = preferences.Enabled;
        setting.WebPushEnabled = preferences.WebPushEnabled;
        setting.EmailEnabled = preferences.EmailEnabled;
        setting.EmailAddress = (preferences.EmailAddress ?? string.Empty).Trim();
        setting.Moments = preferences.Moments;
        setting.Anniversaries = preferences.Anniversaries;
        setting.Shop = preferences.Shop;
        setting.MissYou = preferences.MissYou;
        setting.Comments = preferences.Comments;
        setting.Affinity = preferences.Affinity;
        setting.Cycle = preferences.Cycle;
        setting.RemindMinutes = Math.Clamp(preferences.RemindMinutes, 0, 1439);
        setting.UpdatedAt = SiteClock.UtcNow;

        _ = await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PushDevice> RegisterDeviceAsync(
        int userId,
        PushDeviceRegistration registration,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(registration);

        var endpoint = (registration.Endpoint ?? string.Empty).Trim();
        if (endpoint.Length == 0 || !Uri.TryCreate(endpoint, UriKind.Absolute, out _)) {
            throw new ArgumentException("推送地址不合法。", nameof(registration));
        }

        _ = VapidKeys.Decode(registration.P256dh, VapidKeys.PublicKeyLength, "p256dh");
        _ = VapidKeys.Decode(registration.Auth, 16, "auth");

        EnsureSubject(siteOrigin);

        var key = (registration.DeviceKey ?? string.Empty).Trim();
        var now = SiteClock.UtcNow;

        var device = key.Length > 0
            ? await db.PushDevices.FirstOrDefaultAsync(item => item.DeviceKey == key, cancellationToken)
            : null;

        device ??= await db.PushDevices.FirstOrDefaultAsync(item => item.Endpoint == endpoint, cancellationToken);

        if (device is null && (registration.PreviousEndpoint ?? string.Empty).Trim() is { Length: > 0 } previous) {
            device = await db.PushDevices.FirstOrDefaultAsync(item => item.Endpoint == previous, cancellationToken);
        }

        if (device is null) {
            device = new PushDevice { CreatedAt = now };
            _ = db.PushDevices.Add(device);
        }

        device.UserId = userId;
        device.DeviceKey = key.Length > 0 ? key : Guid.NewGuid().ToString("n");
        device.Endpoint = endpoint;
        device.P256dh = registration.P256dh.Trim();
        device.Auth = registration.Auth.Trim();
        device.DeviceName = DeviceNames.Guess(registration.UserAgent);
        device.LastSeenAt = now;
        device.FailureCount = 0;

        _ = await db.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task<bool> RemoveDeviceAsync(int userId, string endpoint, CancellationToken cancellationToken = default) {
        var deleted = await db.PushDevices
            .Where(device => device.UserId == userId && device.Endpoint == endpoint)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> RemoveDeviceAsync(int userId, long deviceId, CancellationToken cancellationToken = default) {
        var deleted = await db.PushDevices
            .Where(device => device.UserId == userId && device.Id == deviceId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<PushDeviceOwnership> GetOwnershipAsync(int userId, string endpoint, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return PushDeviceOwnership.Unknown;
        }

        var owner = await db.PushDevices
            .Where(device => device.Endpoint == endpoint)
            .Select(device => (int?)device.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return owner is null
            ? PushDeviceOwnership.Unknown
            : owner == userId ? PushDeviceOwnership.Mine : PushDeviceOwnership.Other;
    }

    public async Task<IReadOnlyList<PushDeviceCard>> GetDevicesAsync(
        int userId,
        CancellationToken cancellationToken = default) {
        var devices = await db.PushDevices
            .Where(device => device.UserId == userId)
            .OrderByDescending(device => device.LastSeenAt)
            .ThenByDescending(device => device.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. devices.Select(device => new PushDeviceCard(
            device.Id,
            device.DeviceKey,
            device.DeviceName,
            clock.ToLocal(device.CreatedAt),
            device.LastPushedAt is { } pushed ? clock.ToLocal(pushed) : null))];
    }

    public async Task<PartnerReadiness> GetPartnerReadinessAsync(int userId, CancellationToken cancellationToken = default) {
        var partner = await db.Users
            .Where(user => user.Id != userId && user.IsActive && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (partner is null) {
            return PartnerReadiness.None;
        }

        var setting = await db.NotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == partner.Id, cancellationToken);

        var devices = await db.PushDevices.CountAsync(device => device.UserId == partner.Id, cancellationToken);

        return new PartnerReadiness(
            setting?.Enabled ?? false,
            devices,
            setting?.WebPushEnabled ?? true,
            setting?.EmailEnabled ?? false,
            IsEmailConfigured && EmailOptions.IsValidAddress(setting?.EmailAddress));
    }

    public async Task<int?> GetPartnerIdAsync(int userId, CancellationToken cancellationToken = default) {
        var partner = await db.Users
            .Where(user => user.Id != userId && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return partner;
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        var recipients = await ResolveRecipientsAsync(request, cancellationToken);
        if (recipients.Count == 0) {
            return NotificationDeliveryResult.Empty;
        }

        var requestedChannel = request.Channel
            ?? (request.TargetDeviceId is not null ? NotificationChannelKind.WebPush : null);
        var result = NotificationDeliveryResult.Empty;

        foreach (var channel in _channels.Where(channel => requestedChannel is null || channel.Kind == requestedChannel)) {
            if (!channel.IsConfigured) {
                logger.LogWarning("通知渠道 {Channel} 尚未配置，本次已跳过。", channel.Kind);
                continue;
            }

            var targets = recipients
                .Where(recipient => AllowsChannel(recipient.Setting, request.Topic, channel.Kind))
                .Select(recipient => new NotificationRecipient(
                    recipient.UserId,
                    recipient.Role,
                    recipient.Setting?.EmailAddress ?? string.Empty))
                .ToList();

            if (targets.Count == 0) {
                continue;
            }

            try {
                var channelResult = await channel.SendAsync(request, targets, cancellationToken);
                result = Merge(result, channelResult);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception exception) {
                logger.LogError(exception, "通知渠道 {Channel} 投递失败，继续尝试其它渠道。", channel.Kind);
                result = Merge(result, Failure(channel.Kind, targets.Count));
            }
        }

        return result;
    }

    public async Task<EmailDeliveryResult> SendTestEmailAsync(
        int userId,
        string emailAddress,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default) {
        if (!EmailOptions.IsValidAddress(emailAddress)) {
            return new EmailDeliveryResult(0, 1, EmailFailureReason.NotConfigured);
        }

        var emailChannel = _channels.FirstOrDefault(channel => channel.Kind == NotificationChannelKind.Email);
        if (emailChannel is null || !emailChannel.IsConfigured) {
            return new EmailDeliveryResult(0, 1, EmailFailureReason.NotConfigured);
        }

        var user = await db.Users
            .Where(user => user.Id == userId && user.IsActive && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null) {
            return new EmailDeliveryResult(0, 1, EmailFailureReason.NotConfigured);
        }

        var request = new NotificationRequest(
            NotificationTopic.Test,
            new PushMessage(
                "Our Story 邮件测试",
                "本封邮件用于确认 SMTP 邮件通知通道已正常工作。",
                "/admin/notifications",
                "email-test"),
            TargetUserId: user.Id,
            Channel: NotificationChannelKind.Email,
            SiteOrigin: siteOrigin);

        var result = await emailChannel.SendAsync(
            request,
            [new NotificationRecipient(user.Id, user.Role, emailAddress.Trim())],
            cancellationToken);

        return result.Email;
    }

    #region 私有方法

    private async Task<List<ResolvedRecipient>> ResolveRecipientsAsync(NotificationRequest request, CancellationToken cancellationToken) {
        var candidates = await db.Users
            .Where(user => user.IsActive && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => new { user.Id, user.Role })
            .ToListAsync(cancellationToken);

        if (request.TargetUserId is { } target) {
            candidates = [.. candidates.Where(user => user.Id == target)];
        }

        if (request.ExceptUserId is { } except) {
            candidates = [.. candidates.Where(user => user.Id != except)];
        }

        if (candidates.Count == 0) {
            return [];
        }

        var candidateIds = candidates.Select(user => user.Id).ToList();
        var settings = await db.NotificationSettings
            .Where(setting => candidateIds.Contains(setting.UserId))
            .AsNoTracking()
            .ToDictionaryAsync(setting => setting.UserId, cancellationToken);

        return [.. candidates
            .Select(user => new ResolvedRecipient(
                user.Id,
                user.Role,
                settings.GetValueOrDefault(user.Id)))
            .Where(recipient => recipient.Setting?.Allows(request.Topic) == true
                || request.Topic == NotificationTopic.Test)];
    }

    private static bool AllowsChannel(
        NotificationSetting? setting,
        NotificationTopic topic,
        NotificationChannelKind channel) {
        if (topic == NotificationTopic.Test) {
            return true;
        }

        return channel switch {
            NotificationChannelKind.WebPush => setting?.WebPushEnabled == true,
            NotificationChannelKind.Email => setting?.EmailEnabled == true,
            _ => false
        };
    }

    private static NotificationDeliveryResult Merge(
        NotificationDeliveryResult left,
        NotificationDeliveryResult right) => new(
            new PushDeliveryResult(
                left.WebPush.Sent + right.WebPush.Sent,
                left.WebPush.Failed + right.WebPush.Failed,
                left.WebPush.Dropped + right.WebPush.Dropped,
                (PushFailureReason)Math.Max((int)left.WebPush.Reason, (int)right.WebPush.Reason)),
            new EmailDeliveryResult(
                left.Email.Sent + right.Email.Sent,
                left.Email.Failed + right.Email.Failed,
                (EmailFailureReason)Math.Max((int)left.Email.Reason, (int)right.Email.Reason)));

    private static NotificationDeliveryResult Failure(NotificationChannelKind channel, int count) => channel switch {
        NotificationChannelKind.WebPush => new(
            new PushDeliveryResult(0, count, 0, PushFailureReason.Rejected),
            EmailDeliveryResult.Empty),
        NotificationChannelKind.Email => new(
            PushDeliveryResult.Empty,
            new EmailDeliveryResult(0, count, EmailFailureReason.SendFailed)),
        _ => NotificationDeliveryResult.Empty
    };

    private void EnsureSubject(string? siteOrigin) {
        if (VapidSubject.IsUsable(configuration.Current.Push.Subject)) {
            return;
        }

        if (VapidSubject.FromOrigin(siteOrigin) is not { } subject) {
            return;
        }

        if (configuration.Update(next => next.Push.Subject = subject, out var error)) {
            logger.LogInformation("已把 VAPID 联系方式记成 {Subject}。", subject);
            return;
        }

        logger.LogWarning("没能把 VAPID 联系方式写进配置文件：{Error}", error);
    }

    private sealed record ResolvedRecipient(
        int UserId,
        UserRole Role,
        NotificationSetting? Setting);

    #endregion
}
