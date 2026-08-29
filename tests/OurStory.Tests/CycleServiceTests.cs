// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.Cycles;
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

        var latest = dashboard.History.Items[1];
        Assert.Equal(CycleWriteStatus.Saved, (await service.UpdateAsync(
            boyId,
            latest.Id,
            latest.StartDate,
            latest.StartDate.AddDays(11),
            "修改后动态重算")).Status);
        var refreshed = await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month);
        Assert.Equal(6, refreshed.Statistics.AveragePeriodDays);
        Assert.Equal("修改后动态重算", refreshed.History.Items[1].Note);
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
    public async Task 月历会分出经期易孕期排卵日和安全期() {
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
        Assert.Equal(CyclePhase.Safe, byDate[ovulation.AddDays(-8)].Phase);
        Assert.Equal(CyclePhase.Period, byDate[first.AddDays(30)].Phase);
        Assert.True(byDate[first.AddDays(28)].IsPeriodStart);
        Assert.True(byDate[first.AddDays(32)].IsPeriodEnd);
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
    public async Task 每日补充支持新增更新清空并汇总到周期记录() {
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
            start.AddDays(1),
            CycleFlow.Light,
            CycleMood.Calm,
            0,
            CycleSymptom.None,
            string.Empty))).Status);

        var item = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).History.Items[0];
        Assert.Equal(2, item.LogCount);
        Assert.Equal(CycleFlow.Heavy, item.PeakFlow);
        Assert.Equal(CycleSymptom.Cramps | CycleSymptom.Backache, item.Symptoms);

        // 所有字段为空时删除该日记录，数据库中不保留空记录。
        _ = await service.SaveDayAsync(girlId, new CycleDaySubmission(
            start,
            CycleFlow.Unset,
            CycleMood.Unset,
            0,
            CycleSymptom.None,
            string.Empty));
        Assert.Equal(1, await harness.Db.CycleDailyLogs.CountAsync());
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
    public async Task 删除周期记录不会删除对应日期的补充记录() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-8);
        var created = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        _ = await service.SaveDayAsync(boyId, new CycleDaySubmission(
            start,
            CycleFlow.Medium,
            CycleMood.Low,
            1,
            CycleSymptom.Headache,
            "有点头痛"));

        Assert.Equal(CycleWriteStatus.Saved, (await service.DeleteAsync(boyId, created.RecordId!.Value)).Status);
        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync());
        Assert.Equal(1, await harness.Db.CycleDailyLogs.CountAsync());
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
    public async Task 查询和编辑始终限制在服务端解析出的情侣关系内() {
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
        var created = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));

        var outsiderDashboard = await service.GetDashboardAsync(outsider.Id, 1, 10, start.Year, start.Month);
        Assert.Equal(0, outsiderDashboard.Statistics.TotalRecords);
        var update = await service.UpdateAsync(outsider.Id, created.RecordId!.Value, start, start.AddDays(5), "越权修改");
        Assert.Equal(CycleWriteStatus.NotFound, update.Status);
        Assert.Equal(CycleWriteStatus.NotFound, (await service.DeleteAsync(outsider.Id, created.RecordId!.Value)).Status);
        Assert.DoesNotContain("越权修改", await harness.Db.CycleRecords.Select(item => item.Note).ToListAsync());
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
    public async Task 改动日期不能与另一条记录重叠() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness.Db);
        var today = TestDoubles.Clock().Today;
        var first = today.AddDays(-40);
        _ = await service.CreateAsync(boyId, Submission(first, first.AddDays(4)));
        var second = await service.CreateAsync(boyId, Submission(first.AddDays(28), first.AddDays(32)));

        var clash = await service.UpdateAsync(
            boyId,
            second.RecordId!.Value,
            first.AddDays(2),
            first.AddDays(6),
            string.Empty);
        Assert.Equal(CycleWriteStatus.Conflict, clash.Status);
    }

    #region 私有方法

    private static CycleRecordSubmission Submission(DateOnly start, DateOnly? end) =>
        new(start, end, string.Empty, Guid.NewGuid().ToString());

    private static CycleService Service(
        OurStory.Data.OurStoryDbContext db,
        CycleWriteCoordinator? coordinator = null) {
        var options = new CycleAnalysisOptions();

        return new CycleService(
            db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub(),
            options,
            coordinator ?? new CycleWriteCoordinator());
    }

    #endregion
}
