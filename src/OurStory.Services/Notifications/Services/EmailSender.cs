// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;
using System.Net.Sockets;
using System.Text.Encodings.Web;

namespace OurStory.Services.Notifications;

/// <summary>
/// 使用 MailKit 通过通用 SMTP 发送通知邮件
/// </summary>
internal sealed class EmailSender(
    ActiveConfiguration configuration,
    ILogger<EmailSender> logger) : IEmailSender {
    private const int SubjectLimit = 180;

    public bool IsConfigured => configuration.Email.Enabled && configuration.Email.IsConfigured;

    public async Task<EmailDeliveryResult> SendAsync(
        string recipientEmail,
        PushMessage message,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(message);

        var options = configuration.Email;
        if (!options.Enabled
            || !options.IsConfigured
            || !EmailOptions.IsValidAddress(recipientEmail)) {
            return new EmailDeliveryResult(0, 1, EmailFailureReason.NotConfigured);
        }

        var mail = BuildMessage(options, recipientEmail.Trim(), message, siteOrigin);

        try {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                options.Host.Trim(),
                options.Port,
                ToSocketOptions(options.Security),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.Username)) {
                await client.AuthenticateAsync(options.Username.Trim(), options.Password, cancellationToken);
            }

            _ = await client.SendAsync(mail, cancellationToken);
            try {
                await client.DisconnectAsync(true, CancellationToken.None);
            } catch (Exception exception) {
                logger.LogDebug(exception, "SMTP 邮件已发送，但正常断开连接失败。");
            }

            return new EmailDeliveryResult(1, 0);
        } catch (OperationCanceledException) {
            throw;
        } catch (MailKit.Security.AuthenticationException exception) {
            logger.LogWarning(exception, "SMTP 认证失败，邮件没有发出。");
            return new EmailDeliveryResult(0, 1, EmailFailureReason.AuthenticationFailed);
        } catch (Exception exception) when (exception is SocketException
            or IOException
            or SmtpProtocolException
            or System.Security.Authentication.AuthenticationException) {
            logger.LogWarning(exception, "无法连接 SMTP 服务，邮件没有发出。");
            return new EmailDeliveryResult(0, 1, EmailFailureReason.ConnectionFailed);
        } catch (SmtpCommandException exception) {
            logger.LogWarning(exception, "SMTP 服务拒绝接收邮件。");
            return new EmailDeliveryResult(0, 1, EmailFailureReason.SendFailed);
        } catch (Exception exception) {
            logger.LogError(exception, "发送邮件时发生未预期的错误。");
            return new EmailDeliveryResult(0, 1, EmailFailureReason.SendFailed);
        }
    }

    internal MimeMessage BuildMessage(
        EmailOptions options,
        string recipient,
        PushMessage message,
        string? siteOrigin) {
        var brand = string.IsNullOrWhiteSpace(options.SenderName) ? "Our Story" : options.SenderName.Trim();
        var title = string.IsNullOrWhiteSpace(message.Title) ? "你有一条新通知" : message.Title.Trim();
        var body = string.IsNullOrWhiteSpace(message.Body) ? "打开站点查看这条通知的详细内容。" : message.Body.Trim();
        var url = EmailLinks.Resolve(message.Url, siteOrigin, configuration);
        var settingsUrl = EmailLinks.Resolve("/admin/notifications", siteOrigin, configuration);

        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(brand, options.SenderEmail.Trim()));
        mail.To.Add(MailboxAddress.Parse(recipient));
        mail.Subject = Clamp(title, SubjectLimit);

        var encoder = HtmlEncoder.Default;
        var encodedBrand = encoder.Encode(brand);
        var encodedTitle = encoder.Encode(title);
        var htmlBody = string.Join(
            "<br>",
            body.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(encoder.Encode));
        var preheader = encoder.Encode(Clamp(body, 100));

        var action = string.Empty;
        if (url is not null) {
            var encodedUrl = encoder.Encode(url);
            action = $"""
                <tr>
                  <td style="padding:0 32px 32px;">
                    <a href="{encodedUrl}" style="display:inline-block;padding:12px 22px;border-radius:10px;background:#c65f7c;color:#ffffff;font-size:14px;font-weight:600;line-height:20px;text-decoration:none;">查看详情</a>
                  </td>
                </tr>
                """;
        }

        var settings = settingsUrl is null
            ? "你可以登录站点修改邮件通知设置。"
            : $"你可以在 <a href=\"{encoder.Encode(settingsUrl)}\" style=\"color:#9d5369;text-decoration:underline;\">通知设置</a> 中修改接收偏好。";

        var html = $"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>{encodedTitle}</title>
            </head>
            <body style="margin:0;padding:0;background:#f6f2f3;color:#332b2e;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI','Microsoft YaHei',Arial,sans-serif;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{preheader}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%;background:#f6f2f3;">
                <tr>
                  <td align="center" style="padding:32px 16px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:600px;border:1px solid #eadde1;border-radius:16px;background:#ffffff;overflow:hidden;">
                      <tr>
                        <td style="padding:24px 32px 12px;color:#b15873;font-size:13px;font-weight:600;letter-spacing:.08em;">{encodedBrand}</td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 14px;color:#332b2e;font-size:22px;font-weight:700;line-height:1.45;">{encodedTitle}</td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 26px;color:#665b5f;font-size:15px;line-height:1.8;">{htmlBody}</td>
                      </tr>
                      {action}
                      <tr>
                        <td style="border-top:1px solid #f0e6e9;padding:18px 32px 22px;color:#95878c;font-size:12px;line-height:1.7;">
                          这是一封来自 {encodedBrand} 的自动通知。{settings}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        var textParts = new List<string> { title, string.Empty, body };

        if (url is not null) {
            textParts.Add(string.Empty);
            textParts.Add($"查看详情：{url}");
        }

        textParts.Add(string.Empty);
        textParts.Add($"这是一封来自 {brand} 的自动通知。");
        if (settingsUrl is not null) {
            textParts.Add($"通知设置：{settingsUrl}");
        }

        mail.Body = new BodyBuilder {
            TextBody = string.Join(Environment.NewLine, textParts),
            HtmlBody = html
        }.ToMessageBody();
        return mail;
    }

    private static SecureSocketOptions ToSocketOptions(EmailSecurity security) => security switch {
        EmailSecurity.None => SecureSocketOptions.None,
        EmailSecurity.SslTls => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls
    };

    private static string Clamp(string? value, int limit) {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= limit) {
            return text;
        }

        var cut = char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
        return text[..cut] + "…";
    }
}
