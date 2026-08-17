// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.Notifications;
using System.Buffers.Text;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 通知发给谁、什么时候不发，以及设备记录怎么进怎么出
/// </summary>
public class NotificationServiceTests {
    [Fact]
    public async Task 同一台设备重新授权只更新不新增() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        const string endpoint = "https://push.example.com/send/abc";
        _ = await notifications.RegisterDeviceAsync(boyId, Registration(endpoint, "Mozilla/5.0 (iPhone) Safari"));
        var again = await notifications.RegisterDeviceAsync(boyId, Registration(endpoint, "Mozilla/5.0 (iPhone) Safari"));

        Assert.Equal(1, await harness.Db.PushDevices.CountAsync());
        Assert.Equal("iPhone · Safari", again.DeviceName);
    }

    [Fact]
    public async Task 撤销权限再重新授权不会多出一台设备() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        const string key = "same-browser";
        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/a", deviceKey: key));
        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/b", deviceKey: key));
        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/c", deviceKey: key));

        var device = await harness.Db.PushDevices.SingleAsync();
        Assert.Equal("https://push.example.com/c", device.Endpoint);
    }

    [Fact]
    public async Task 不同浏览器各算一台设备() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/chrome", deviceKey: "chrome"));
        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/edge", deviceKey: "edge"));

        Assert.Equal(2, await harness.Db.PushDevices.CountAsync());
    }

    [Fact]
    public async Task 浏览器换发订阅时靠老地址认回同一台() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/old", deviceKey: "browser"));

        _ = await notifications.RegisterDeviceAsync(
            boyId,
            new PushDeviceRegistration(
                "https://push.example.com/new",
                P256dh,
                Auth,
                PreviousEndpoint: "https://push.example.com/old"));

        var device = await harness.Db.PushDevices.SingleAsync();
        Assert.Equal("https://push.example.com/new", device.Endpoint);
    }

    [Fact]
    public async Task 没带设备编号时会补一个() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        // 浏览器存不了本地数据时不能让编号是空的，否则唯一索引会把第二台挡在门外
        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/one"));
        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/two"));

        var keys = await harness.Db.PushDevices.Select(device => device.DeviceKey).ToListAsync();
        Assert.Equal(2, keys.Count);
        Assert.All(keys, key => Assert.NotEmpty(key));
        Assert.Equal(2, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task 对方开着通知并且有设备才算准备好() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        // 一次都没进过通知设置：还没开通
        Assert.False((await notifications.GetPartnerReadinessAsync(boyId)).CanReceive);

        // 开了总开关，但一台设备都没授权，照样收不到
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });
        var noDevice = await notifications.GetPartnerReadinessAsync(boyId);
        Assert.True(noDevice.Enabled);
        Assert.Equal(0, noDevice.Devices);
        Assert.False(noDevice.CanReceive);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/girl"));
        var ready = await notifications.GetPartnerReadinessAsync(boyId);
        Assert.True(ready.CanReceive);
        Assert.Equal(1, ready.Devices);

        // 对方把总开关关掉，立刻就不算准备好了
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = false });
        Assert.False((await notifications.GetPartnerReadinessAsync(boyId)).CanReceive);
    }

    [Fact]
    public async Task 换人登录时设备跟着改归属() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        const string endpoint = "https://push.example.com/send/shared";
        _ = await notifications.RegisterDeviceAsync(boyId, Registration(endpoint));
        _ = await notifications.RegisterDeviceAsync(girlId, Registration(endpoint));

        var device = await harness.Db.PushDevices.SingleAsync();
        Assert.Equal(girlId, device.UserId);
    }

    [Fact]
    public async Task 没开通知的人收不到() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));

        // 偏好那一行还没建过，等于还没开通
        var result = await notifications.SendAsync(
            NotificationRequest.ToPartner(NotificationTopic.Moment, boyId, Message()));

        Assert.Equal(0, result.Total);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task 关掉某一项之后那一类就不再打扰() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences {
            Enabled = true,
            Moments = false,
            Shop = true
        });

        var muted = await notifications.SendAsync(
            NotificationRequest.ToPartner(NotificationTopic.Moment, boyId, Message()));
        var kept = await notifications.SendAsync(
            NotificationRequest.ToPartner(NotificationTopic.Shop, boyId, Message()));

        Assert.Equal(0, muted.Sent);
        Assert.Equal(1, kept.Sent);
    }

    [Fact]
    public async Task 总开关关掉之后连对方发的话也收不到() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = false });

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        Assert.Equal(0, result.Sent);
    }

    [Fact]
    public async Task 通知测试不看那四个勾() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        _ = await notifications.RegisterDeviceAsync(boyId, Registration("https://push.example.com/send/boy"));

        // 一次都没进过通知设置，测试通知照样得能发出去，否则没法排查
        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Test, boyId, Message()));

        Assert.Equal(1, result.Sent);
    }

    [Fact]
    public async Task 自己做的事不会通知自己() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        foreach (var userId in new[] { boyId, girlId }) {
            _ = await notifications.RegisterDeviceAsync(userId, Registration($"https://push.example.com/send/{userId}"));
            await notifications.SaveSettingAsync(userId, new NotificationPreferences { Enabled = true });
        }

        var result = await notifications.SendAsync(
            NotificationRequest.ToPartner(NotificationTopic.Moment, boyId, Message()));

        Assert.Equal(1, result.Sent);
        Assert.Equal($"https://push.example.com/send/{girlId}", Assert.Single(sender.Sent).Endpoint);
    }

    [Fact]
    public async Task 推送服务说订阅没了就把设备清掉() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });
        sender.Outcome = PushSendOutcome.Gone;

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        Assert.Equal(1, result.Dropped);
        Assert.Equal(0, await harness.Db.PushDevices.CountAsync());
    }

    [Fact]
    public async Task 一次失败不会立刻判掉一台设备() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });
        sender.Outcome = PushSendOutcome.Failed;

        var result = await notifications.SendAsync(
            NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        // 手机关机、断网都会失败，这时候删掉订阅就再也回不来了
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, (await harness.Db.PushDevices.SingleAsync()).FailureCount);
    }

    [Fact]
    public async Task 一直发不出去的设备最终会被清掉() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });
        sender.Outcome = PushSendOutcome.Failed;

        for (var attempt = 0; attempt < 8; attempt++) {
            _ = await notifications.SendAsync(
                NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));
        }

        Assert.Equal(0, await harness.Db.PushDevices.CountAsync());
    }

    [Fact]
    public async Task 成功送达之后记下时间并清零失败计数() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });

        sender.Outcome = PushSendOutcome.Failed;
        _ = await notifications.SendAsync(NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        sender.Outcome = PushSendOutcome.Delivered;
        _ = await notifications.SendAsync(NotificationRequest.ToUser(NotificationTopic.Direct, girlId, Message()));

        var device = await harness.Db.PushDevices.SingleAsync();
        Assert.Equal(0, device.FailureCount);
        _ = Assert.NotNull(device.LastPushedAt);
    }

    [Fact]
    public async Task 太长的通知会被剪短而不是发不出去() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });

        // 一条通知只装得进一个记录，正文再长也得先剪短，不能让加密那一步炸掉
        var result = await notifications.SendAsync(NotificationRequest.ToUser(
            NotificationTopic.Direct,
            girlId,
            new PushMessage("标题", new string('长', 5000))));

        Assert.Equal(1, result.Sent);
        Assert.True(Assert.Single(sender.Sent).Payload.Length < 1000);
    }

    [Fact]
    public async Task 剪短时不会把表情切成两半() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out var sender);

        _ = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));
        await notifications.SaveSettingAsync(girlId, new NotificationPreferences { Enabled = true });

        // 每个表情占两个位置，正好切在第 300 位上就会剩下半个字符
        _ = await notifications.SendAsync(NotificationRequest.ToUser(
            NotificationTopic.Direct,
            girlId,
            new PushMessage("标题", string.Concat(Enumerable.Repeat("😘", 400)))));

        // 切坏了的话，序列化成 JSON 时那半个字符会变成 U+FFFD
        var payload = Assert.Single(sender.Sent).Payload;
        Assert.DoesNotContain('�', payload);
        Assert.Contains('…', payload);
    }

    [Fact]
    public async Task 提醒时间超出一天会被拉回范围内() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        await notifications.SaveSettingAsync(boyId, new NotificationPreferences { RemindMinutes = 5000 });

        Assert.Equal(1439, (await notifications.GetSettingAsync(boyId)).RemindMinutes);
    }

    [Fact]
    public async Task 只能注销自己的设备() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        var device = await notifications.RegisterDeviceAsync(girlId, Registration("https://push.example.com/send/girl"));

        Assert.False(await notifications.RemoveDeviceAsync(boyId, device.Id));
        Assert.True(await notifications.RemoveDeviceAsync(girlId, device.Id));
    }

    [Fact]
    public async Task 对方就是另一个账号() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        Assert.Equal(girlId, await notifications.GetPartnerIdAsync(boyId));
        Assert.Equal(boyId, await notifications.GetPartnerIdAsync(girlId));
    }

    [Fact]
    public async Task 订阅里的密钥不合法就不收() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var notifications = Service(harness, out _);

        _ = await Assert.ThrowsAsync<FormatException>(() => notifications.RegisterDeviceAsync(
            boyId,
            new PushDeviceRegistration("https://push.example.com/send/x", "太短了", Auth, null)));

        _ = await Assert.ThrowsAsync<ArgumentException>(() => notifications.RegisterDeviceAsync(
            boyId,
            new PushDeviceRegistration("不是网址", P256dh, Auth, null)));
    }

    #region 私有方法

    /// <summary>一份格式合法的订阅公钥：65 字节的未压缩 P-256 点。</summary>
    private static readonly string P256dh = VapidKeys.Create().PublicKey;

    /// <summary>订阅里的 auth 是 16 字节随机串，测试里内容无所谓，长度得对。</summary>
    private static readonly string Auth = Base64Url.EncodeToString(new byte[16]);

    private static PushDeviceRegistration Registration(
        string endpoint,
        string? userAgent = null,
        string? deviceKey = null) =>
        new(endpoint, P256dh, Auth, DeviceKey: deviceKey, UserAgent: userAgent);

    private static PushMessage Message() => new("标题", "正文");

    private static NotificationService Service(SqliteHarness harness, out SenderSpy sender) {
        sender = new SenderSpy();

        return new NotificationService(
            harness.Db,
            sender,
            new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration()),
            TestDoubles.Clock(),
            NullLogger<NotificationService>.Instance);
    }

    #endregion
}

/// <summary>记下每条要发的通知，结果由测试指定。</summary>
internal sealed class SenderSpy : IWebPushSender {
    /// <summary>接下来每次投递都返回这个结果。</summary>
    public PushSendOutcome Outcome { get; set; } = PushSendOutcome.Delivered;

    /// <summary>收到过的每一条投递。</summary>
    public List<(string Endpoint, string Payload)> Sent { get; } = [];

    public bool IsConfigured => true;

    public string PublicKey => "test-key";

    public Task<PushSendOutcome> SendAsync(PushDevice device, string payload, CancellationToken cancellationToken = default) {
        Sent.Add((device.Endpoint, payload));
        return Task.FromResult(Outcome);
    }
}
