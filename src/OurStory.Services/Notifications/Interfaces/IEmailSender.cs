// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Models;

namespace OurStory.Services.Notifications;

/// <summary>
/// 负责一封邮件的 MIME 构建、SMTP 连接、认证和发送
/// </summary>
internal interface IEmailSender {
    /// <summary>
    /// 获取一个值，指示 SMTP 站点配置是否完整
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// 向一个收件地址发送通知邮件
    /// </summary>
    Task<EmailDeliveryResult> SendAsync(
        string recipientEmail,
        PushMessage message,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default);
}
