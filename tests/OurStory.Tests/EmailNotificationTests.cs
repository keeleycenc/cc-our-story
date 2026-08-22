// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Services.Notifications;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 邮件内容、渠道组合与失败隔离。
/// </summary>
public class EmailNotificationTests {
    [Theory]
    [InlineData(true, false, 1, 0)]
    [InlineData(false, true, 0, 1)]
    [InlineData(true, true, 1, 1)]
    [InlineData(false, false, 0, 0)]
    public async Task WebPush与Email可以独立或同时启用(
        bool webPushEnabled,
        bool emailEnabled,
        int expectedPush,
        int expectedEmail) {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy();
        var notifications = Service(harness, email, out var push);

        AddDevice(harness, girlId);
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = webPushEnabled,
            EmailEnabled = emailEnabled,
            EmailAddress = "girl@example.com"
        });

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        Assert.Equal(expectedPush, result.WebPush.Sent);
        Assert.Equal(expectedEmail, result.Email.Sent);
        Assert.Equal(expectedPush, push.Sent.Count);
        Assert.Equal(expectedEmail, email.Sent.Count);
    }

    [Fact]
    public async Task Topic偏好会同时拦住两个渠道() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy();
        var notifications = Service(harness, email, out var push);

        AddDevice(harness, girlId);
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = true,
            EmailEnabled = true,
            EmailAddress = "girl@example.com",
            Moments = false
        });

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Moment, girlId, Message()));

        Assert.Equal(0, result.Total);
        Assert.Empty(push.Sent);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task 男女主各自使用自己的个人邮箱() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy();
        var notifications = Service(harness, email, out _);

        await notifications.SaveSettingAsync(boyId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = false,
            EmailEnabled = true,
            EmailAddress = "boy-personal@example.com"
        });
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = false,
            EmailEnabled = true,
            EmailAddress = "girl-personal@example.com"
        });

        var result = await notifications.SendAsync(new NotificationRequest(NotificationTopic.Direct, Message()));

        Assert.Equal(2, result.Email.Sent);
        Assert.Equal(
            ["boy-personal@example.com", "girl-personal@example.com"],
            [.. email.Sent.Select(item => item.Address).Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task WebPush异常不会阻止Email继续发送() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy();
        var notifications = Service(harness, email, out var push);
        push.Exception = new InvalidOperationException("push broke");

        AddDevice(harness, girlId);
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = true,
            EmailEnabled = true,
            EmailAddress = "girl@example.com"
        });

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        Assert.Equal(1, result.WebPush.Failed);
        Assert.Equal(1, result.Email.Sent);
        _ = Assert.Single(email.Sent);
    }

    [Fact]
    public async Task Email失败不会影响WebPush结果() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy {
            Result = new EmailDeliveryResult(0, 1, EmailFailureReason.ConnectionFailed)
        };
        var notifications = Service(harness, email, out _);

        AddDevice(harness, girlId);
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = true,
            EmailEnabled = true,
            EmailAddress = "girl@example.com"
        });

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        Assert.Equal(1, result.WebPush.Sent);
        Assert.Equal(1, result.Email.Failed);
        Assert.Equal(EmailFailureReason.ConnectionFailed, result.Email.Reason);
    }

    [Fact]
    public async Task 没填写个人邮箱时只跳过Email() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy();
        var notifications = Service(harness, email, out _);

        AddDevice(harness, girlId);
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            WebPushEnabled = true,
            EmailEnabled = true
        });

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        Assert.Equal(1, result.WebPush.Sent);
        Assert.Equal(0, result.Email.Total);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task 后台测试邮件不受个人开关影响且使用当前填写的地址() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var email = new EmailSenderSpy();
        var notifications = Service(harness, email, out _);

        var result = await notifications.SendTestEmailAsync(
            boyId,
            "personal@example.com",
            "https://request.example.com");

        Assert.Equal(1, result.Sent);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("personal@example.com", sent.Address);
        Assert.Equal("https://request.example.com", sent.Origin);
    }

    [Fact]
    public void 邮件使用统一品牌模板并包含纯文本与绝对链接() {
        var configuration = Configuration();
        var sender = new EmailSender(configuration, NullLogger<EmailSender>.Instance);

        var mail = sender.BuildMessage(
            configuration.Email,
            "girl@example.com",
            new PushMessage("新的点点滴滴", "第一行\n第二行", "/moments/summer"),
            null);

        Assert.Equal("【Our Story】新的点点滴滴", mail.Subject);
        Assert.Contains("第一行", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://love.example.com/moments/summer", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://love.example.com/admin/notifications", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("<!doctype html>", mail.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<br>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://love.example.com/moments/summer\"", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://love.example.com/admin/notifications\"", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("这是一封来自 Our Story 的自动通知", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void 邮件模板会编码用户可控内容() {
        var configuration = Configuration();
        var sender = new EmailSender(configuration, NullLogger<EmailSender>.Instance);

        var mail = sender.BuildMessage(
            configuration.Email,
            "girl@example.com",
            new PushMessage("<script>标题</script>", "你好 <img src=x onerror=alert(1)>", "/"),
            null);

        Assert.DoesNotContain("<script>", mail.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", mail.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void 绝对通知链接不会被站点地址改写() {
        var configuration = Configuration();

        Assert.Equal(
            "https://outside.example/path",
            EmailLinks.Resolve("https://outside.example/path", null, configuration));
    }

    [Fact]
    public void 密码留空时保留原值() {
        var options = new EmailOptions { Password = "existing-secret" };

        options.SetPasswordIfProvided(string.Empty);
        Assert.Equal("existing-secret", options.Password);

        options.SetPasswordIfProvided("new-secret");
        Assert.Equal("new-secret", options.Password);
    }

    [Fact]
    public void Email配置可随ourstoryJson完整往返() {
        var directory = Path.Combine(Path.GetTempPath(), "ourstory-email-tests", Guid.NewGuid().ToString("n"));
        var store = new ConfigurationStore(directory);
        var source = new OurStoryConfiguration { Email = Configuration().Email };

        Assert.True(store.TrySave(source, out var error), error);
        var loaded = store.Load().Configuration.Email;

        Assert.True(loaded.Enabled);
        Assert.Equal("smtp.example.com", loaded.Host);
        Assert.Equal(EmailSecurity.StartTls, loaded.Security);
        Assert.Equal("secret", loaded.Password);
        Assert.Equal("https://love.example.com", loaded.SiteBaseUrl);
    }

    private static NotificationService Service(
        SqliteHarness harness,
        IEmailSender email,
        out SenderSpy push,
        ActiveConfiguration? configuration = null) {
        push = new SenderSpy();
        var active = configuration ?? Configuration();

        return new NotificationService(
            harness.Db,
            push,
            [
                new WebPushNotificationChannel(harness.Db, push),
                new EmailNotificationChannel(email)
            ],
            active,
            TestDoubles.Clock(),
            NullLogger<NotificationService>.Instance);
    }

    private static ActiveConfiguration Configuration() =>
        new(new ConfigurationStore("."), new OurStoryConfiguration {
            Email = new EmailOptions {
                Enabled = true,
                Host = "smtp.example.com",
                Port = 587,
                Security = EmailSecurity.StartTls,
                Username = "notify@example.com",
                Password = "secret",
                SenderEmail = "notify@example.com",
                SenderName = "Our Story",
                SiteBaseUrl = "https://love.example.com"
            }
        });

    private static void AddDevice(SqliteHarness harness, int userId) {
        _ = harness.Db.PushDevices.Add(new PushDevice {
            UserId = userId,
            DeviceKey = Guid.NewGuid().ToString("n"),
            Endpoint = "https://push.example.com/send/girl",
            P256dh = "key",
            Auth = "auth",
            DeviceName = "test"
        });
        _ = harness.Db.SaveChanges();
    }

    private static PushMessage Message() => new("标题", "正文", "/detail");
}

/// <summary>
/// 不连接 SMTP，只记下邮件渠道交来的内容
/// </summary>
internal sealed class EmailSenderSpy : IEmailSender {
    public bool IsConfigured { get; set; } = true;

    public EmailDeliveryResult Result { get; set; } = new(1, 0);

    public List<(string Address, PushMessage Message, string? Origin)> Sent { get; } = [];

    public Task<EmailDeliveryResult> SendAsync(
        string recipientEmail,
        PushMessage message,
        string? siteOrigin = null,
        CancellationToken cancellationToken = default) {
        Sent.Add((recipientEmail, message, siteOrigin));
        return Task.FromResult(Result);
    }
}
