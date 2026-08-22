// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Models;

namespace OurStory.Services.Notifications;

/// <summary>
/// 按个人通知设置中的邮箱地址投递的 Email 通知渠道
/// </summary>
internal sealed class EmailNotificationChannel(IEmailSender sender) : INotificationChannel {
    public NotificationChannelKind Kind => NotificationChannelKind.Email;

    public bool IsConfigured => sender.IsConfigured;

    public async Task<NotificationDeliveryResult> SendAsync(
        NotificationRequest request,
        IReadOnlyList<NotificationRecipient> recipients,
        CancellationToken cancellationToken = default) {
        if (!IsConfigured || recipients.Count == 0) {
            return NotificationDeliveryResult.Empty;
        }

        var sent = 0;
        var failed = 0;
        var reason = EmailFailureReason.None;

        foreach (var recipient in recipients) {
            if (string.IsNullOrWhiteSpace(recipient.EmailAddress)) {
                continue;
            }

            var result = await sender.SendAsync(recipient.EmailAddress, request.Message, request.SiteOrigin, cancellationToken);
            sent += result.Sent;
            failed += result.Failed;
            reason = Worse(reason, result.Reason);
        }

        return new NotificationDeliveryResult(
            PushDeliveryResult.Empty,
            new EmailDeliveryResult(sent, failed, reason));
    }

    private static EmailFailureReason Worse(EmailFailureReason current, EmailFailureReason next) =>
        (EmailFailureReason)Math.Max((int)current, (int)next);
}
