// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Data;
using OurStory.Services.Cycles;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 验证花信如期记录动态与定时提醒的生成规则
/// </summary>
/// <remarks>
/// 测试范围包括通知是否入队、接收对象与通知内容，以及定时提醒的触发日期。
/// 实际发送由通知服务负责，测试中使用通知队列替身记录入队请求。
/// </remarks>
public sealed class CycleNotificationTests {
    [Fact]
    public async Task 记下开始日期会通知对方() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var service = Service(harness.Db, queue);

        Assert.Equal(
            CycleWriteStatus.Saved,
            (await service.StartAsync(boyId, Guid.NewGuid().ToString(), false)).Status);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.Cycle, request.Topic);
        Assert.Equal(boyId, request.ExceptUserId);
        Assert.Equal("新的花信记录已开始", request.Message.Title);
        Assert.Equal("/cycles", request.Message.Url);
        Assert.Contains("男主", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 结束记录会通知对方并说清持续了几天() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var service = Service(harness.Db, queue);
        var start = TestDoubles.Clock().Today.AddDays(-4);

        _ = await service.CreateAsync(boyId, Submission(start, null));
        Assert.Equal(CycleWriteStatus.Saved, (await service.EndAsync(girlId)).Status);

        Assert.Equal(2, queue.Sent.Count);
        var ending = queue.Sent[1];
        Assert.Equal(NotificationTopic.Cycle, ending.Topic);
        Assert.Equal(girlId, ending.ExceptUserId);
        Assert.Equal("本次花信已结束", ending.Message.Title);
        Assert.Contains("共 5 天", ending.Message.Body, StringComparison.Ordinal);
        Assert.Contains("女主", ending.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 每日补充只说多了一条不把内容摊在锁屏上() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var service = Service(harness.Db, queue);

        Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(girlId, new CycleDaySubmission(
            TestDoubles.Clock().Today,
            CycleFlow.Heavy,
            CycleMood.Low,
            2,
            CycleSymptom.Cramps,
            "肚子疼得厉害",
            true,
            CycleIntimacyProtection.Condom,
            CycleIntimacyOutcome.External))).Status);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.Cycle, request.Topic);
        Assert.Equal(girlId, request.ExceptUserId);
        Assert.Equal("花信记录有新补充", request.Message.Title);
        Assert.DoesNotContain("肚子疼得厉害", request.Message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("亲密", request.Message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("经量", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 需要二次确认时不会先把通知发出去() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var service = Service(harness.Db, queue);
        var start = TestDoubles.Clock().Today.AddDays(-30);

        _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        queue.Sent.Clear();

        // 与上一条记录的开始日期仅相隔两天，应判定为可能重复登记。
        var suspicious = await service.CreateAsync(girlId, Submission(start.AddDays(2), start.AddDays(5)));

        Assert.Equal(CycleWriteStatus.RequiresConfirmation, suspicious.Status);
        Assert.Empty(queue.Sent);
    }

    [Fact]
    public async Task 预测窗口开始前第三天提醒一次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db, TestDoubles.Notifications());
        var today = TestDoubles.Clock().Today;

        // 两条完整记录间隔 28 天，因此预计日期为最后一次开始日期后的第 28 天；
        // 预测窗口在预计日期前后各延伸 2 天，窗口将于 3 天后开始。
        await CompleteAsync(service, boyId, today.AddDays(-51));
        await CompleteAsync(service, boyId, today.AddDays(-23));

        var reminder = Assert.Single(await service.GetDueRemindersAsync(boyId));
        Assert.Equal(CycleReminderKind.PredictionNear, reminder.Kind);
        Assert.Contains("3 天后", reminder.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 预测窗口开始当天再提醒一次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db, TestDoubles.Notifications());
        var today = TestDoubles.Clock().Today;

        await CompleteAsync(service, boyId, today.AddDays(-54));
        await CompleteAsync(service, boyId, today.AddDays(-26));

        var reminder = Assert.Single(await service.GetDueRemindersAsync(boyId));
        Assert.Equal(CycleReminderKind.PredictionStart, reminder.Kind);
    }

    [Fact]
    public async Task 窗口还远的日子里一条提醒也不发() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db, TestDoubles.Notifications());
        var today = TestDoubles.Clock().Today;

        await CompleteAsync(service, boyId, today.AddDays(-40));
        await CompleteAsync(service, boyId, today.AddDays(-12));

        Assert.Empty(await service.GetDueRemindersAsync(boyId));
    }

    [Fact]
    public async Task 进行中的记录超过正常时长后提醒核对结束日期() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db, TestDoubles.Notifications());

        // 第 10 天已超过经期偏长的判定阈值，但尚未达到单次记录的最长时限。
        _ = await service.CreateAsync(boyId, Submission(TestDoubles.Clock().Today.AddDays(-9), null));

        var reminder = Assert.Single(await service.GetDueRemindersAsync(boyId));
        Assert.Equal(CycleReminderKind.ActiveTooLong, reminder.Kind);
        Assert.Contains("第 10 天", reminder.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 进行中的记录还没超过正常时长时不提醒() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db, TestDoubles.Notifications());

        _ = await service.StartAsync(boyId, Guid.NewGuid().ToString(), false);

        Assert.Empty(await service.GetDueRemindersAsync(boyId));
    }

    [Fact]
    public async Task 进行中的记录超过单次上限后不再每天提醒() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db, TestDoubles.Notifications());

        // 第 21 天已超过单次记录的最长时限，不再继续生成每日提醒。
        _ = await service.CreateAsync(boyId, Submission(TestDoubles.Clock().Today.AddDays(-20), null));

        Assert.Empty(await service.GetDueRemindersAsync(boyId));
    }

    [Fact]
    public async Task 关系之外的账号既收不到通知也算不出提醒() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var outsider = new User { UserName = "outsider", PasswordHash = "test", Role = UserRole.Boy };
        _ = harness.Db.Users.Add(outsider);
        _ = await harness.Db.SaveChangesAsync();

        var queue = TestDoubles.Notifications();
        var service = Service(harness.Db, queue);
        _ = await service.StartAsync(boyId, Guid.NewGuid().ToString(), false);
        queue.Sent.Clear();

        Assert.Equal(CycleWriteStatus.Forbidden, (await service.EndAsync(outsider.Id)).Status);
        Assert.Empty(queue.Sent);
        Assert.Empty(await service.GetDueRemindersAsync(outsider.Id));
    }

    [Fact]
    public void 关掉花信开关之后这一类通知就不再送达() {
        var setting = new NotificationSetting { Enabled = true, Cycle = true };
        Assert.True(setting.Allows(NotificationTopic.Cycle));

        setting.Cycle = false;
        Assert.False(setting.Allows(NotificationTopic.Cycle));

        setting.Cycle = true;
        setting.Enabled = false;
        Assert.False(setting.Allows(NotificationTopic.Cycle));
    }

    #region 私有方法

    private static CycleRecordSubmission Submission(DateOnly start, DateOnly? end) =>
        new(start, end, string.Empty, Guid.NewGuid().ToString());

    private static async Task CompleteAsync(CycleService service, int userId, DateOnly start) {
        var result = await service.CreateAsync(userId, Submission(start, start.AddDays(4)));
        Assert.Equal(CycleWriteStatus.Saved, result.Status);
    }

    private static CycleService Service(OurStoryDbContext db, NotificationQueueSpy notifications) {
        var options = new CycleAnalysisOptions();

        return new CycleService(
            db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub(),
            options,
            new CycleWriteCoordinator(),
            notifications);
    }

    #endregion
}
