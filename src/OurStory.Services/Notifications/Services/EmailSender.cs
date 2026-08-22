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
        var title = Clamp(message.Title, SubjectLimit);
        var body = (message.Body ?? string.Empty).Trim();
        var url = EmailLinks.Resolve(message.Url, siteOrigin, configuration);

        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(options.SenderName.Trim(), options.SenderEmail.Trim()));
        mail.To.Add(MailboxAddress.Parse(recipient));
        mail.Subject = title;

        var encoder = HtmlEncoder.Default;
        var htmlBody = string.Join(
            "<br>",
            body.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(encoder.Encode));
        var html = $"<p>{htmlBody}</p>";
        var text = body;

        if (url is not null) {
            var encodedUrl = encoder.Encode(url);
            html += $"<p><a href=\"{encodedUrl}\">查看详情</a></p>";
            text += $"{Environment.NewLine}{Environment.NewLine}查看详情：{url}";
        }

        mail.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();
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
