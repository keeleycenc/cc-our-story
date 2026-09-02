// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.Cycles;
using OurStory.Web.Infrastructure;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 验证花信事实写入、并发控制及动态生成的统计、阶段与标签
/// </summary>
public sealed class CycleServiceTests {
    [Fact]
    public async Task 创建一年记录后动态统计预测日历和分页保持一致() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var firstStart = today.AddDays(-368);

        for (var index = 0; index < 14; index++) {
            var start = firstStart.AddDays(index * 28);
            var result = await service.CreateAsync(boyId, new CycleRecordSubmission(
                start,
                start.AddDays(4),
                $"年度校验第 {index + 1} 次",
                Guid.NewGuid().ToString()));
            Assert.Equal(CycleWriteStatus.Saved, result.Status);
        }

        var dashboard = await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month);
        Assert.Equal(14, dashboard.Statistics.TotalRecords);
        Assert.Equal(14, dashboard.Statistics.CompletedRecords);
        Assert.Equal(28, dashboard.Statistics.AverageCycleDays);
        Assert.Equal(5, dashboard.Statistics.AveragePeriodDays);
        Assert.Equal(28, dashboard.Statistics.ShortestCycleDays);
        Assert.Equal(28, dashboard.Statistics.LongestCycleDays);
        Assert.Equal(today.AddDays(24), dashboard.Statistics.NextPrediction!.ExpectedStart);
        Assert.Equal(14, dashboard.History.TotalCount);
        Assert.Equal(2, dashboard.History.TotalPages);
        Assert.Equal(10, dashboard.History.Items.Count);
        Assert.Contains(dashboard.Calendar.Days, day => day.Phase == CyclePhase.Period);

    }

    [Fact]
    public async Task 规律记录会缩小预测窗口() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;

        for (var index = 6; index >= 1; index--) {
            var start = today.AddDays(-index * 28);
            _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        }

        var steady = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).Statistics.NextPrediction!;
        Assert.Equal(0, steady.WindowDays - 3);
        Assert.True(steady.Confidence >= 80, $"规律记录的可信度应不低于 80%，实际为 {steady.Confidence}%");

        // 加入一条间隔 20 天的记录后，周期波动增大，预测窗口随之放宽。
        _ = await service.CreateAsync(boyId, Submission(today.AddDays(-8), today.AddDays(-4)));
        var loosened = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).Statistics;
        Assert.True(loosened.CycleSwingDays > 0, "加入不规律记录后，周期波动应大于 0");
        Assert.True(
            loosened.NextPrediction!.WindowDays > steady.WindowDays,
            "记录变得不规律之后，预测窗口应该更宽");
    }

    [Fact]
    public async Task 月历会分出经期卵泡期易孕期排卵日黄体期和预测窗口() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;

        // 两条间隔 28 天的完整记录可用于推算下一次周期及排卵日。
        var first = today.AddDays(-56);
        _ = await service.CreateAsync(boyId, Submission(first, first.AddDays(4)));
        _ = await service.CreateAsync(boyId, Submission(first.AddDays(28), first.AddDays(32)));

        var month = await service.GetCalendarAsync(boyId, first.AddDays(40).Year, first.AddDays(40).Month);
        var byDate = month.Days.ToDictionary(day => day.Date);

        // 预计开始日期向前推算 14 天得到排卵日。
        var ovulation = first.AddDays(28 + 28 - 14);
        Assert.Equal(CyclePhase.Ovulation, byDate[ovulation].Phase);
        Assert.Equal(CyclePhase.Fertile, byDate[ovulation.AddDays(-2)].Phase);
        Assert.Equal(CyclePhase.Fertile, byDate[ovulation.AddDays(1)].Phase);
        Assert.Equal(CyclePhase.Follicular, byDate[ovulation.AddDays(-8)].Phase);
        Assert.Equal(CyclePhase.Luteal, byDate[ovulation.AddDays(3)].Phase);
        Assert.Equal(CyclePhase.Predicted, byDate[first.AddDays(54)].Phase);
        Assert.Equal(CyclePhase.Period, byDate[first.AddDays(30)].Phase);
        Assert.True(byDate[first.AddDays(28)].IsPeriodStart);
        Assert.True(byDate[first.AddDays(32)].IsPeriodEnd);
    }

    [Fact]
    public async Task 超出预测窗口且仍未开始时进入观察期() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-40);

        _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));

        var month = await service.GetCalendarAsync(boyId, today.Year, today.Month);
        var todayCell = Assert.Single(month.Days, day => day.Date == today);
        Assert.Equal(CyclePhase.Observation, todayCell.Phase);
    }

    [Fact]
    public async Task 历史条目会带上正常或需留意的标签() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var first = today.AddDays(-100);

        _ = await service.CreateAsync(boyId, Submission(first, first.AddDays(4)));
        _ = await service.CreateAsync(boyId, Submission(first.AddDays(28), first.AddDays(32)));
        _ = await service.CreateAsync(boyId, Submission(first.AddDays(56), first.AddDays(60)));
        // 该记录间隔 40 天，明显晚于此前 28 天的周期节奏。
        _ = await service.CreateAsync(boyId, Submission(first.AddDays(96), first.AddDays(100)));

        var history = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).History.Items;
        var late = history[0];
        var steady = history[1];

        Assert.Equal(CycleRhythm.Late, late.Rhythm);
        Assert.Equal(40, late.CycleDays);
        Assert.Equal("需留意", late.Tags[0].Text);
        Assert.Contains(late.Tags, tag => tag.Text.StartsWith("推迟", StringComparison.Ordinal));

        Assert.Equal(CycleRhythm.Normal, steady.Rhythm);
        Assert.Equal("正常", steady.Tags[0].Text);
        Assert.Equal(CycleTagTone.Normal, steady.Tags[0].Tone);
    }

    [Fact]
    public async Task 每日补充按次追加并汇总到周期记录() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-6);
        var created = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        Assert.Equal(CycleWriteStatus.Saved, created.Status);

        Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(girlId, new CycleDaySubmission(
            start,
            CycleFlow.Heavy,
            CycleMood.Tired,
            2,
            CycleSymptom.Cramps | CycleSymptom.Backache,
            "喝了红糖水"))).Status);
        Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(boyId, new CycleDaySubmission(
            start,
            CycleFlow.Unset,
            CycleMood.Unset,
            0,
            CycleSymptom.None,
            string.Empty,
            true,
            CycleIntimacyProtection.Condom,
            CycleIntimacyOutcome.Internal,
            3))).Status);

        var item = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).History.Items[0];
        Assert.Equal(2, item.LogCount);
        Assert.Equal(CycleFlow.Heavy, item.PeakFlow);
        Assert.Equal(CycleSymptom.Cramps | CycleSymptom.Backache, item.Symptoms);

        var calendar = await service.GetCalendarAsync(boyId, start.Year, start.Month);
        var day = Assert.Single(calendar.Days, value => value.Date == start);
        Assert.Equal(2, day.Logs.Count);
        Assert.Equal("喝了红糖水", day.Logs[0].Note);
        Assert.True(day.Logs[1].IsIntimate);
        Assert.Equal(3, day.Logs[1].IntimacyCount);
        Assert.Equal(CycleIntimacyProtection.Condom, day.Logs[1].IntimacyProtection);
        Assert.Equal(CycleIntimacyOutcome.Internal, day.Logs[1].IntimacyOutcome);
        Assert.True(CycleDayPayload.From(day).JointRecord);

        var defaultCount = await service.SaveDayAsync(boyId, new CycleDaySubmission(
            start.AddDays(1),
            CycleFlow.Unset,
            CycleMood.Unset,
            0,
            CycleSymptom.None,
            string.Empty,
            true,
            CycleIntimacyProtection.Unset,
            CycleIntimacyOutcome.Unset));
        Assert.Equal(CycleWriteStatus.Saved, defaultCount.Status);
        Assert.Equal(1, await harness.Db.CycleDailyLogs
            .OrderByDescending(log => log.Id)
            .Select(log => log.IntimacyCount)
            .FirstAsync());

        // 空提交不会生成没有内容的卡片，也不会删除已有记录。
        var empty = await service.SaveDayAsync(girlId, new CycleDaySubmission(
            start,
            CycleFlow.Unset,
            CycleMood.Unset,
            0,
            CycleSymptom.None,
            string.Empty));
        Assert.Equal(CycleWriteStatus.Invalid, empty.Status);
        Assert.Equal(3, await harness.Db.CycleDailyLogs.CountAsync());
    }

    [Fact]
    public async Task 补充每日状态不会修改周期日期() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-8);
        _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        var before = await harness.Db.CycleRecords.AsNoTracking().SingleAsync();

        foreach (var date in new[] { start.AddDays(1), today.AddDays(-1) }) {
            Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(girlId, new CycleDaySubmission(
                date,
                CycleFlow.Medium,
                CycleMood.Low,
                1,
                CycleSymptom.Cramps,
                "补充当天状态"))).Status);
        }

        var after = await harness.Db.CycleRecords.AsNoTracking().SingleAsync();
        Assert.Equal(before.StartDate, after.StartDate);
        Assert.Equal(before.EndDate, after.EndDate);
        Assert.Equal(2, await harness.Db.CycleDailyLogs.CountAsync());
    }

    [Fact]
    public async Task 双方并发开始时仅创建一条进行中记录() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        await using var secondDb = harness.CreateContext();
        var coordinator = new CycleWriteCoordinator();
        var boyService = Service(harness.Db, coordinator);
        var girlService = Service(secondDb, coordinator);

        var results = await Task.WhenAll(
            boyService.StartAsync(boyId, Guid.NewGuid().ToString(), false),
            girlService.StartAsync(girlId, Guid.NewGuid().ToString(), false));

        _ = Assert.Single(results, item => item.Status == CycleWriteStatus.Saved);
        _ = Assert.Single(results, item => item.Status == CycleWriteStatus.Conflict);
        Assert.Equal(1, await harness.Db.CycleRecords.CountAsync(item => item.EndDate == null));
    }

    [Fact]
    public async Task 可疑重复记录经确认后可保存且请求保持幂等() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-40);
        _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        _ = await service.CreateAsync(girlId, Submission(start.AddDays(28), start.AddDays(32)));

        var requestKey = Guid.NewGuid().ToString();
        var suspicious = new CycleRecordSubmission(start.AddDays(28), start.AddDays(32), "双方可能重复", requestKey);
        var warning = await service.CreateAsync(boyId, suspicious);
        Assert.Equal(CycleWriteStatus.RequiresConfirmation, warning.Status);
        Assert.Equal(2, await harness.Db.CycleRecords.CountAsync());

        var confirmed = await service.CreateAsync(boyId, suspicious with { ConfirmSuspicious = true });
        Assert.Equal(CycleWriteStatus.Saved, confirmed.Status);
        var replay = await service.CreateAsync(girlId, suspicious with { ConfirmSuspicious = true });
        Assert.Equal(CycleWriteStatus.AlreadyProcessed, replay.Status);
        Assert.Equal(3, await harness.Db.CycleRecords.CountAsync());
    }

    [Fact]
    public async Task 查询始终限制在服务端解析出的情侣关系内() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var otherRelationship = new CoupleRelationship();
        var outsider = new User {
            UserName = "outsider",
            PasswordHash = "test",
            Role = UserRole.Boy,
            CoupleRelationship = otherRelationship
        };
        harness.Db.AddRange(otherRelationship, outsider);
        _ = await harness.Db.SaveChangesAsync();
        var service = Service(harness.Db);
        var start = TestDoubles.Clock().Today.AddDays(-10);
        _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));

        var outsiderDashboard = await service.GetDashboardAsync(outsider.Id, 1, 10, start.Year, start.Month);
        Assert.Equal(0, outsiderDashboard.Statistics.TotalRecords);
    }

    [Fact]
    public async Task 开始记录后必须先结束才能再次创建() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);

        Assert.Equal(CycleWriteStatus.Saved, (await service.StartAsync(boyId, Guid.NewGuid().ToString(), false)).Status);
        var today = TestDoubles.Clock().Today;
        Assert.Equal(CycleWriteStatus.Conflict, (await service.CreateAsync(
            girlId,
            Submission(today.AddDays(-30), today.AddDays(-26)))).Status);
        Assert.Equal(CycleWriteStatus.Saved, (await service.EndAsync(girlId)).Status);
        Assert.Equal(CycleWriteStatus.Conflict, (await service.EndAsync(boyId)).Status);
        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync(item => item.EndDate == null));
    }

    [Fact]
    public async Task 同一个请求键重复提交只会记下一条() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var requestKey = Guid.NewGuid().ToString();

        Assert.Equal(CycleWriteStatus.Saved, (await service.StartAsync(boyId, requestKey, false)).Status);

        // 重复请求应优先按幂等规则处理，而不是返回已有进行中记录的冲突结果。
        Assert.Equal(CycleWriteStatus.AlreadyProcessed, (await service.StartAsync(boyId, requestKey, false)).Status);
        Assert.Equal(CycleWriteStatus.AlreadyProcessed, (await service.StartAsync(girlId, requestKey, false)).Status);
        Assert.Equal(1, await harness.Db.CycleRecords.CountAsync());
    }

    [Fact]
    public async Task 不是请求键的字符串会被当作失效请求拒绝() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);

        Assert.Equal(CycleWriteStatus.Invalid, (await service.StartAsync(boyId, "not-a-key", false)).Status);
        Assert.Equal(CycleWriteStatus.Invalid, (await service.StartAsync(boyId, string.Empty, false)).Status);
        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync());
    }

    [Fact]
    public async Task 同一天提交两次一样的补充会各自留下一条() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var submission = new CycleDaySubmission(
            today,
            CycleFlow.Medium,
            CycleMood.Calm,
            1,
            CycleSymptom.Cramps,
            "下午好一些了");

        // 每日补充按提交次数追加，同一天的相同内容应分别保留。
        Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(girlId, submission)).Status);
        Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(girlId, submission)).Status);

        var day = Assert.Single((await service.GetCalendarAsync(girlId, today.Year, today.Month)).Days, item => item.Date == today);
        Assert.Equal(2, day.Logs.Count);
        Assert.All(day.Logs, log => Assert.Equal("下午好一些了", log.Note));
    }

    [Fact]
    public async Task 超长备注会被拦下写满上限的备注原样保存() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var options = new CycleAnalysisOptions();
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-6);

        var tooLong = await service.CreateAsync(boyId, new CycleRecordSubmission(
            start,
            start.AddDays(4),
            new string('记', options.MaximumNoteLength + 1),
            Guid.NewGuid().ToString()));
        Assert.Equal(CycleWriteStatus.Invalid, tooLong.Status);
        Assert.Contains(options.MaximumNoteLength.ToString(System.Globalization.CultureInfo.InvariantCulture), tooLong.Message, StringComparison.Ordinal);
        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync());

        Assert.Equal(CycleWriteStatus.Saved, (await service.CreateAsync(boyId, new CycleRecordSubmission(
            start,
            start.AddDays(4),
            new string('记', options.MaximumNoteLength),
            Guid.NewGuid().ToString()))).Status);
        Assert.Equal(options.MaximumNoteLength, (await harness.Db.CycleRecords.SingleAsync()).Note.Length);
    }

    [Fact]
    public async Task 超长的每日补充说明会被拦下而不是悄悄截断() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var options = new CycleAnalysisOptions();
        var today = TestDoubles.Clock().Today;

        var tooLong = await service.SaveDayAsync(girlId, Day(today, new string('记', options.MaximumDayNoteLength + 1)));
        Assert.Equal(CycleWriteStatus.Invalid, tooLong.Status);
        Assert.Equal(0, await harness.Db.CycleDailyLogs.CountAsync());

        Assert.Equal(
            CycleWriteStatus.Saved,
            (await service.SaveDayAsync(girlId, Day(today, new string('记', options.MaximumDayNoteLength)))).Status);
        Assert.Equal(options.MaximumDayNoteLength, (await harness.Db.CycleDailyLogs.SingleAsync()).Note.Length);
    }

    [Fact]
    public async Task 越界的枚举取值不会写进每日补充() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;

        foreach (var crafted in new[] {
            new CycleDaySubmission(today, (CycleFlow)99, CycleMood.Unset, 0, CycleSymptom.None, string.Empty),
            new CycleDaySubmission(today, CycleFlow.Unset, (CycleMood)77, 0, CycleSymptom.None, string.Empty),
            new CycleDaySubmission(today, CycleFlow.Unset, CycleMood.Unset, 0, (CycleSymptom)(1 << 15), string.Empty)
        }) {
            Assert.Equal(CycleWriteStatus.Invalid, (await service.SaveDayAsync(girlId, crafted)).Status);
        }

        Assert.Equal(0, await harness.Db.CycleDailyLogs.CountAsync());
    }

    [Fact]
    public async Task 越界的亲密取值按未填写处理次数与不适程度按上限收口() {
        await using var harness = SqliteHarness.Create();
        var (_, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);

        Assert.Equal(CycleWriteStatus.Saved, (await service.SaveDayAsync(girlId, new CycleDaySubmission(
            TestDoubles.Clock().Today,
            CycleFlow.Unset,
            CycleMood.Unset,
            99,
            CycleSymptom.None,
            string.Empty,
            true,
            (CycleIntimacyProtection)9,
            (CycleIntimacyOutcome)9,
            999))).Status);

        var log = await harness.Db.CycleDailyLogs.SingleAsync();
        Assert.Equal(3, log.Pain);
        Assert.Equal(20, log.IntimacyCount);
        Assert.Equal(CycleIntimacyProtection.Unset, log.IntimacyProtection);
        Assert.Equal(CycleIntimacyOutcome.Unset, log.IntimacyOutcome);
    }

    [Fact]
    public async Task 结束日期早于开始或落在未来都会被拒绝() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var options = new CycleAnalysisOptions();
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-20);

        Assert.Equal(CycleWriteStatus.Invalid, (await service.CreateAsync(boyId, Submission(start, start.AddDays(-1)))).Status);
        Assert.Equal(CycleWriteStatus.Invalid, (await service.CreateAsync(boyId, Submission(today.AddDays(-2), today.AddDays(1)))).Status);
        Assert.Equal(CycleWriteStatus.Invalid, (await service.CreateAsync(boyId, Submission(today.AddDays(1), null))).Status);
        Assert.Equal(
            CycleWriteStatus.Invalid,
            (await service.CreateAsync(boyId, Submission(start, start.AddDays(options.MaximumPeriodDays)))).Status);
        Assert.Equal(CycleWriteStatus.Invalid, (await service.SaveDayAsync(boyId, Day(today.AddDays(1), "明天的事还没发生"))).Status);

        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync());
        Assert.Equal(0, await harness.Db.CycleDailyLogs.CountAsync());
    }

    [Fact]
    public async Task 拖了很久的进行中记录仍然可以结束但不算进平均经期() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var options = new CycleAnalysisOptions();

        // 构造一条持续时间超过单次记录最长时限的进行中记录。
        _ = await service.CreateAsync(boyId, Submission(today.AddDays(-40), null));

        var ended = await service.EndAsync(girlId);
        Assert.Equal(CycleWriteStatus.Saved, ended.Status);

        var record = await harness.Db.CycleRecords.AsNoTracking().SingleAsync();
        Assert.Equal(today, record.EndDate);
        Assert.True(
            record.EndDate!.Value.DayNumber - record.StartDate.DayNumber + 1 > options.MaximumPeriodDays,
            "测试记录的持续时间应超过单次记录允许的最长时限");

        // 结束日期应正常保存，但超出合理范围的持续时间不应计入平均经期。
        var statistics = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).Statistics;
        Assert.Equal(1, statistics.TotalRecords);
        Assert.Equal(1, statistics.CompletedRecords);
        Assert.Null(statistics.AveragePeriodDays);
    }

    #region 私有方法

    private static CycleDaySubmission Day(DateOnly date, string note) =>
        new(date, CycleFlow.Medium, CycleMood.Calm, 0, CycleSymptom.None, note);

    private static CycleRecordSubmission Submission(DateOnly start, DateOnly? end) =>
        new(start, end, string.Empty, Guid.NewGuid().ToString());

    private static CycleService Service(
        OurStory.Data.OurStoryDbContext db,
        CycleWriteCoordinator? coordinator = null,
        NotificationQueueSpy? notifications = null) {
        var options = new CycleAnalysisOptions();

        return new CycleService(
            db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub(),
            options,
            coordinator ?? new CycleWriteCoordinator(),
            notifications ?? TestDoubles.Notifications());
    }

    #endregion
}
