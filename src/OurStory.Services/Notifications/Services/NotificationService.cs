// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using System.Text.Json;

namespace OurStory.Services.Notifications;

internal sealed class NotificationService(
    OurStoryDbContext db,
    IWebPushSender sender,
    ActiveConfiguration configuration,
    SiteClock clock,
    ILogger<NotificationService> logger) : INotificationService {
    private const int MaxFailures = 8;
    private const int TitleLimit = 80;
    private const int BodyLimit = 300;

    private static readonly JsonSerializerOptions PayloadJson = new() {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public bool IsConfigured => sender.IsConfigured;

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
        setting.Moments = preferences.Moments;
        setting.Anniversaries = preferences.Anniversaries;
        setting.Shop = preferences.Shop;
        setting.MissYou = preferences.MissYou;
        setting.Comments = preferences.Comments;
        setting.Affinity = preferences.Affinity;
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
        if (await GetPartnerIdAsync(userId, cancellationToken) is not { } partnerId) {
            return PartnerReadiness.None;
        }

        var enabled = await db.NotificationSettings
            .AnyAsync(setting => setting.UserId == partnerId && setting.Enabled, cancellationToken);

        var devices = await db.PushDevices.CountAsync(device => device.UserId == partnerId, cancellationToken);
        return new PartnerReadiness(enabled, devices);
    }

    public async Task<int?> GetPartnerIdAsync(int userId, CancellationToken cancellationToken = default) {
        var partner = await db.Users
            .Where(user => user.Id != userId && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => (int?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return partner;
    }

    public async Task<PushDeliveryResult> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (!sender.IsConfigured) {
            logger.LogWarning("VAPID 密钥还没准备好，这条通知没有发出去。");
            return PushDeliveryResult.Empty;
        }

        var recipients = await ResolveRecipientsAsync(request, cancellationToken);
        if (recipients.Count == 0) {
            return PushDeliveryResult.Empty;
        }

        var candidates = db.PushDevices.Where(device => recipients.Contains(device.UserId));

        if (request.TargetDeviceId is { } deviceId) {
            candidates = candidates.Where(device => device.Id == deviceId);
        }

        var devices = await candidates.ToListAsync(cancellationToken);

        if (devices.Count == 0) {
            return PushDeliveryResult.Empty;
        }

        var payload = JsonSerializer.Serialize(new {
            title = Clamp(request.Message.Title, TitleLimit),
            body = Clamp(request.Message.Body, BodyLimit),
            url = request.Message.Url,
            tag = request.Message.Tag
        }, PayloadJson);

        var sent = 0;
        var failed = 0;
        var dropped = new List<PushDevice>();
        var reason = PushFailureReason.None;
        var now = SiteClock.UtcNow;

        foreach (var device in devices) {
            var outcome = await sender.SendAsync(device, payload, cancellationToken);

            switch (outcome) {
                case PushSendOutcome.Delivered:
                    device.LastPushedAt = now;
                    device.FailureCount = 0;
                    sent++;
                    break;

                case PushSendOutcome.Gone:
                    dropped.Add(device);
                    reason = Worse(reason, PushFailureReason.Expired);
                    break;

                case PushSendOutcome.NotConfigured:
                    return PushDeliveryResult.Empty;

                case PushSendOutcome.Unreachable:
                    failed++;
                    reason = Worse(reason, PushFailureReason.Unreachable);
                    break;

                case PushSendOutcome.Unauthorized:
                    failed++;
                    reason = Worse(reason, PushFailureReason.Unauthorized);
                    break;

                case PushSendOutcome.Failed:
                default:
                    device.FailureCount++;
                    if (device.FailureCount >= MaxFailures) {
                        dropped.Add(device);
                    } else {
                        failed++;
                    }

                    reason = Worse(reason, PushFailureReason.Rejected);
                    break;
            }
        }

        if (dropped.Count > 0) {
            db.PushDevices.RemoveRange(dropped);
        }

        _ = await db.SaveChangesAsync(cancellationToken);
        return new PushDeliveryResult(sent, failed, dropped.Count, reason);
    }

    #region 私有方法

    private static PushFailureReason Worse(PushFailureReason current, PushFailureReason next) =>
        (PushFailureReason)Math.Max((int)current, (int)next);

    private static string Clamp(string? value, int limit) {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= limit) {
            return text;
        }

        var cut = char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
        return text[..cut] + "…";
    }

    private async Task<List<int>> ResolveRecipientsAsync(NotificationRequest request, CancellationToken cancellationToken) {
        var candidates = await db.Users
            .Where(user => user.IsActive && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (request.TargetUserId is { } target) {
            candidates = [.. candidates.Where(id => id == target)];
        }

        if (request.ExceptUserId is { } except) {
            candidates = [.. candidates.Where(id => id != except)];
        }

        if (candidates.Count == 0) {
            return [];
        }

        var settings = await db.NotificationSettings
            .Where(setting => candidates.Contains(setting.UserId))
            .AsNoTracking()
            .ToDictionaryAsync(setting => setting.UserId, cancellationToken);

        return [.. candidates.Where(id =>
            settings.TryGetValue(id, out var setting)
                ? setting.Allows(request.Topic)
                : request.Topic == NotificationTopic.Test)];
    }

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

    #endregion
}
