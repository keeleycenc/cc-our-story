// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Models;

namespace OurStory.Services.Notifications;

/// <summary>
/// 已完成 Topic 与用户偏好过滤的通知收件人
/// </summary>
internal sealed record NotificationRecipient(int UserId, UserRole Role, string EmailAddress);

/// <summary>
/// 一个独立的通知投递渠道
/// </summary>
internal interface INotificationChannel {
    /// <summary>
    /// 获取渠道类型
    /// </summary>
    NotificationChannelKind Kind { get; }

    /// <summary>
    /// 获取一个值，指示站点级配置是否允许此渠道工作
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// 把通知交给此渠道，调用前已完成业务偏好过滤
    /// </summary>
    Task<NotificationDeliveryResult> SendAsync(
        NotificationRequest request,
        IReadOnlyList<NotificationRecipient> recipients,
        CancellationToken cancellationToken = default);
}
