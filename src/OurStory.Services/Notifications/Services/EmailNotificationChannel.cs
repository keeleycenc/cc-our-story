// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;

namespace OurStory.Services.Notifications;

/// <summary>
/// 按用户角色映射收件地址的 Email 通知渠道
/// </summary>
internal sealed class EmailNotificationChannel(
    IEmailSender sender,
    ActiveConfiguration configuration) : INotificationChannel {
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
            var address = AddressOf(configuration.Email, recipient.Role);
            if (string.IsNullOrWhiteSpace(address)) {
                continue;
            }

            var result = await sender.SendAsync(address, request.Message, request.SiteOrigin, cancellationToken);
            sent += result.Sent;
            failed += result.Failed;
            reason = Worse(reason, result.Reason);
        }

        return new NotificationDeliveryResult(
            PushDeliveryResult.Empty,
            new EmailDeliveryResult(sent, failed, reason));
    }

    private static string AddressOf(EmailOptions options, UserRole role) => role switch {
        UserRole.Boy => options.BoyEmail,
        UserRole.Girl => options.GirlEmail,
        _ => string.Empty
    };

    private static EmailFailureReason Worse(EmailFailureReason current, EmailFailureReason next) =>
        (EmailFailureReason)Math.Max((int)current, (int)next);
}
