// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using System.Text.Json;

namespace OurStory.Services.Notifications;

/// <summary>
/// Web Push 通知渠道，负责设备选择、投递结果统计和失效订阅清理
/// </summary>
internal sealed class WebPushNotificationChannel(
    OurStoryDbContext db,
    IWebPushSender sender) : INotificationChannel {
    private const int MaxFailures = 8;
    private const int TitleLimit = 80;
    private const int BodyLimit = 300;

    private static readonly JsonSerializerOptions PayloadJson = new() {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public NotificationChannelKind Kind => NotificationChannelKind.WebPush;

    public bool IsConfigured => sender.IsConfigured;

    public async Task<NotificationDeliveryResult> SendAsync(
        NotificationRequest request,
        IReadOnlyList<NotificationRecipient> recipients,
        CancellationToken cancellationToken = default) {
        if (!sender.IsConfigured || recipients.Count == 0) {
            return NotificationDeliveryResult.Empty;
        }

        var userIds = recipients.Select(recipient => recipient.UserId).ToList();
        var candidates = db.PushDevices.Where(device => userIds.Contains(device.UserId));

        if (request.TargetDeviceId is { } deviceId) {
            candidates = candidates.Where(device => device.Id == deviceId);
        }

        var devices = await candidates.ToListAsync(cancellationToken);
        if (devices.Count == 0) {
            return NotificationDeliveryResult.Empty;
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
                    return NotificationDeliveryResult.Empty;

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
        return new NotificationDeliveryResult(
            new PushDeliveryResult(sent, failed, dropped.Count, reason),
            EmailDeliveryResult.Empty);
    }

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
}
