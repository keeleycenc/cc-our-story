// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Services.Cycles;
using OurStory.Services.LlmAtmosphere;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 验证周期小结的模型生成与站内规则回退行为
/// </summary>
public sealed class CycleInsightTests {
    [Fact]
    public async Task 未配置模型时使用站内规则生成小结() {
        var service = Insight(out var client, configured: false);

        var summary = await service.WriteAsync(Context());

        Assert.Empty(client.Requests);
        Assert.False(summary.FromModel);
        Assert.Equal(CycleSummarySource.Rule, summary.Source);
        Assert.Contains("持续 5 天", summary.Text, StringComparison.Ordinal);
        Assert.Contains("与既往节奏基本一致", summary.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 模型生成成功时清理排版符号并使用返回内容() {
        var service = Insight(
            out var client,
            configured: true,
            ResponsesResult.Success("## 小结\n- 本次与往常基本一致，今晚早点休息，我们一起照顾好状态。"));

        var summary = await service.WriteAsync(Context());

        Assert.True(summary.FromModel);
        Assert.Equal("小结本次与往常基本一致，今晚早点休息，我们一起照顾好状态。", summary.Text);

        var request = Assert.Single(client.Requests);
        Assert.Equal("花信小结", request.Endpoint.Label);
        Assert.Equal("cycle-model", request.Endpoint.Model);
        Assert.Contains("不提供医疗诊断", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("腹痛", request.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ResponsesFailure.Unreachable)]
    [InlineData(ResponsesFailure.Unauthorized)]
    [InlineData(ResponsesFailure.Empty)]
    public async Task 模型生成失败时回退到站内规则小结(ResponsesFailure failure) {
        var service = Insight(out _, configured: true, ResponsesResult.Failed(failure));

        var summary = await service.WriteAsync(Context());

        Assert.False(summary.FromModel);
        Assert.NotEmpty(summary.Text);
    }

    [Fact]
    public void 事实变化时摘要指纹同步变化() {
        var original = Context();
        var stamp = CycleNarrative.Stamp(original);

        Assert.Equal(stamp, CycleNarrative.Stamp(Context()));
        Assert.NotEqual(stamp, CycleNarrative.Stamp(original with { Note = "改了备注" }));
        Assert.NotEqual(stamp, CycleNarrative.Stamp(original with { EndDate = original.EndDate!.Value.AddDays(1) }));
        Assert.NotEqual(stamp, CycleNarrative.Stamp(original with { Days = [] }));

        Assert.NotEqual(stamp, CycleNarrative.Stamp(original with { History = [original.History[0]] }));
        Assert.NotEqual(
            stamp,
            CycleNarrative.Stamp(original with {
                History = [original.History[0], original.History[1] with { Note = "改了历史备注" }]
            }));
    }

    [Fact]
    public void 输入按分析目标与此前历史分段并只要求写目标周期() {
        var input = CycleNarrative.Input(Context());

        Assert.Contains("分析目标：第 3 个周期", input, StringComparison.Ordinal);
        Assert.Contains("携带范围：第 1 至第 3 个周期，共 3 个周期", input, StringComparison.Ordinal);
        Assert.Contains("此前历史", input, StringComparison.Ordinal);
        Assert.Contains("第 2 个周期：2026 年 4 月 6 日 至 2026 年 4 月 10 日", input, StringComparison.Ordinal);
        Assert.Contains("夜里疼醒过", input, StringComparison.Ordinal);
        Assert.Contains("既往平均周期（不含本次）：28 天", input, StringComparison.Ordinal);
        Assert.Contains("请仅为上面的“本次周期”撰写一段小结", input, StringComparison.Ordinal);

        Assert.Contains("正文中不要出现", input, StringComparison.Ordinal);
        Assert.Contains("正文中一律不得出现", CycleNarrative.Instructions(null), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 上下文只携带目标周期之前的记录且基线不含本次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var options = new CycleAnalysisOptions();
        var insight = new CycleInsightStub("模型写的那一段");
        var service = new CycleService(
            harness.Db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            insight,
            options,
            new CycleWriteCoordinator());

        var today = TestDoubles.Clock().Today;
        var first = today.AddDays(-28 * 15);

        for (var index = 0; index < 15; index++) {
            var start = first.AddDays(index * 28);
            _ = await service.CreateAsync(boyId, new CycleRecordSubmission(
                start,
                start.AddDays(4),
                $"第 {index + 1} 次",
                Guid.NewGuid().ToString()));
        }

        // 第 1 个周期的每日记录应随历史一并携带给后续周期。
        _ = await service.SaveDayAsync(boyId, new CycleDaySubmission(
            first,
            CycleFlow.Heavy,
            CycleMood.Low,
            3,
            CycleSymptom.Cramps,
            "第一次的补充记录"));

        Assert.Equal(15, await service.RefreshSummariesAsync(30));

        // 页面按同一份事实重算指纹，补写好的模型小结应当全部命中。
        var history = (await service.GetDashboardAsync(boyId, 1, 20, today.Year, today.Month)).History.Items;
        Assert.All(history, item => Assert.True(item.Summary.FromModel));

        var contexts = insight.Contexts.ToDictionary(item => item.Ordinal);
        Assert.Equal(15, contexts.Count);

        var firstCycle = contexts[1];
        Assert.Empty(firstCycle.History);
        Assert.Null(firstCycle.AverageCycleDays);
        Assert.Null(firstCycle.AveragePeriodDays);

        var third = contexts[3];
        Assert.Equal([1, 2], third.History.Select(item => item.Ordinal));
        Assert.Equal(28, third.AverageCycleDays);
        Assert.Equal(5, third.AveragePeriodDays);
        Assert.Contains(third.History[0].Days, day => day.Note == "第一次的补充记录");

        // 最新周期最多携带含自身在内的 12 个周期，且不含之后的任何记录。
        var latest = contexts[15];
        Assert.Equal(11, latest.History.Count);
        Assert.Equal(4, latest.WindowStartOrdinal);
        Assert.Equal([4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14], latest.History.Select(item => item.Ordinal));
        Assert.All(latest.History, item => Assert.True(item.StartDate < latest.StartDate));
    }

    [Fact]
    public async Task 目标周期之后新增记录不会让旧小结失效() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var options = new CycleAnalysisOptions();
        var service = new CycleService(
            harness.Db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub("模型写的那一段"),
            options,
            new CycleWriteCoordinator());

        var today = TestDoubles.Clock().Today;
        var first = today.AddDays(-100);

        _ = await service.CreateAsync(boyId, Submission(first, first.AddDays(4)));
        _ = await service.CreateAsync(boyId, Submission(first.AddDays(28), first.AddDays(32)));
        Assert.Equal(2, await service.RefreshSummariesAsync(10));

        _ = await service.CreateAsync(boyId, Submission(first.AddDays(56), first.AddDays(60)));

        // 只有新记录需要补写，此前两条的指纹不受之后数据影响。
        Assert.Equal(1, await service.RefreshSummariesAsync(10));
        Assert.Equal(0, await service.RefreshSummariesAsync(10));
    }

    [Fact]
    public async Task 改动携带窗口之外但仍影响基线的旧周期会让目标小结失效() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var options = new CycleAnalysisOptions();
        var service = new CycleService(
            harness.Db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub("模型写的那一段"),
            options,
            new CycleWriteCoordinator());

        var today = TestDoubles.Clock().Today;
        var first = today.AddDays(-28 * 15);

        for (var index = 0; index < 15; index++) {
            var start = first.AddDays(index * 28);
            _ = await service.CreateAsync(boyId, Submission(start, start.AddDays(4)));
        }

        Assert.Equal(15, await service.RefreshSummariesAsync(30));

        // 第 2 个周期落在最新周期携带的 12 个周期（第 4～15 个）之外，却仍参与既往平均周期的计算。
        var second = await harness.Db.CycleRecords
            .OrderBy(item => item.StartDate)
            .Skip(1)
            .FirstAsync();
        second.StartDate = first.AddDays(16);
        second.EndDate = first.AddDays(20);
        _ = await harness.Db.SaveChangesAsync();

        // 最新周期自身未被改动，但既往平均周期由 28 天变为 29 天，小结应随之失效。
        var history = (await service.GetDashboardAsync(boyId, 1, 20, today.Year, today.Month)).History.Items;
        Assert.Equal(28, history[0].CycleDays);
        Assert.Equal(-1, history[0].CycleDelta);
        Assert.False(history[0].Summary.FromModel);
    }

    [Fact]
    public async Task 已生成的小结在事实变化后自动失效() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var options = new CycleAnalysisOptions();
        var insight = new CycleInsightStub("模型写的那一段");
        var service = new CycleService(
            harness.Db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            insight,
            options,
            new CycleWriteCoordinator());

        var today = TestDoubles.Clock().Today;
        var start = today.AddDays(-10);
        var created = await service.CreateAsync(boyId, new CycleRecordSubmission(
            start,
            start.AddDays(4),
            "第一版备注",
            Guid.NewGuid().ToString()));

        Assert.Equal(1, await service.RefreshSummariesAsync(5));
        var summarized = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).History.Items[0];
        Assert.True(summarized.Summary.FromModel);
        Assert.Equal("模型写的那一段", summarized.Summary.Text);

        // 追加当天事实后，已保存的小结失效，页面立即回退到规则文案。
        _ = await service.SaveDayAsync(boyId, new CycleDaySubmission(
            start,
            CycleFlow.Medium,
            CycleMood.Calm,
            0,
            CycleSymptom.None,
            "改过的备注"));
        var stale = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).History.Items[0];
        Assert.False(stale.Summary.FromModel);

        // 后续巡检重新生成模型小结。
        Assert.Equal(1, await service.RefreshSummariesAsync(5));
        Assert.Equal(0, await service.RefreshSummariesAsync(5));
    }

    [Fact]
    public async Task 进行中的记录不生成模型小结() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var options = new CycleAnalysisOptions();
        var service = new CycleService(
            harness.Db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub("模型写的那一段"),
            options,
            new CycleWriteCoordinator());

        _ = await service.StartAsync(boyId, Guid.NewGuid().ToString(), false);

        Assert.Equal(0, await service.RefreshSummariesAsync(5));
        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync(item => item.Summary != string.Empty));
    }

    [Fact]
    public async Task 后台试写取最新一次记录且不写入站点() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var options = new CycleAnalysisOptions();
        var service = new CycleService(
            harness.Db,
            TestDoubles.Clock(),
            new SettingsStub(),
            new RuleBasedCycleAnalysisService(options),
            new CycleInsightStub(),
            options,
            new CycleWriteCoordinator());

        var today = TestDoubles.Clock().Today;
        var first = today.AddDays(-60);
        _ = await service.CreateAsync(boyId, Submission(first, first.AddDays(4)));
        _ = await service.CreateAsync(boyId, Submission(first.AddDays(28), first.AddDays(32)));

        // 试写复用页面与补写的同一份投影，因此看到的事实与正式生成时一致。
        var narrative = await service.LatestNarrativeAsync(boyId);
        Assert.NotNull(narrative);
        Assert.Equal(2, narrative.Ordinal);
        Assert.Equal(first.AddDays(28), narrative.StartDate);
        Assert.Equal([1], narrative.History.Select(item => item.Ordinal));

        var probe = await Insight(out var client, configured: true, ResponsesResult.Success("试写的一段")).ProbeAsync(narrative);
        Assert.True(probe.Ok);
        Assert.Equal("试写的一段", probe.Text);
        Assert.Contains("最新一次花信记录", probe.Message, StringComparison.Ordinal);
        Assert.Contains("分析目标：第 2 个周期", Assert.Single(client.Requests).Text, StringComparison.Ordinal);

        // 试写不落库，正式小结仍由后台任务补写。
        Assert.Equal(0, await harness.Db.CycleRecords.CountAsync(item => item.Summary != string.Empty));

        // 不属于这段关系的用户拿不到记录，试写回退到内置示例。
        Assert.Null(await service.LatestNarrativeAsync(9999));
        var sample = await Insight(out _, configured: true, ResponsesResult.Success("示例小结")).ProbeAsync();
        Assert.Equal("示例小结", sample.Text);
        Assert.Contains("示例小结", sample.Message, StringComparison.Ordinal);
    }

    #region 私有方法

    private static CycleRecordSubmission Submission(DateOnly start, DateOnly? end) =>
        new(start, end, string.Empty, Guid.NewGuid().ToString());

    private static CycleNarrativeContext Context() => new(
        new DateOnly(2026, 5, 4),
        new DateOnly(2026, 5, 8),
        5,
        28,
        0,
        CycleRhythm.Normal,
        "本次状态较为平稳",
        28,
        5,
        [
            new CycleDayFact(new DateOnly(2026, 5, 4), CycleFlow.Medium, CycleMood.Tired, 2, CycleSymptom.Cramps, string.Empty),
            new CycleDayFact(new DateOnly(2026, 5, 6), CycleFlow.Light, CycleMood.Calm, 0, CycleSymptom.None, "状态已有改善")
        ],
        3,
        [
            new CyclePastFact(
                1,
                new DateOnly(2026, 3, 9),
                new DateOnly(2026, 3, 13),
                5,
                null,
                string.Empty,
                []),
            new CyclePastFact(
                2,
                new DateOnly(2026, 4, 6),
                new DateOnly(2026, 4, 10),
                5,
                28,
                "上次一直用着暖宝宝",
                [new CycleDayFact(new DateOnly(2026, 4, 6), CycleFlow.Heavy, CycleMood.Low, 3, CycleSymptom.Cramps, "夜里疼醒过")])
        ]);

    private static CycleInsightService Insight(
        out ResponsesClientStub client,
        bool configured,
        params ResponsesResult[] answers) {
        client = new ResponsesClientStub(answers);
        var options = configured
            ? new CycleInsightOptions {
                Enabled = true,
                BaseUrl = "https://example.com/v1",
                Model = "cycle-model",
                ApiKey = "sk-test"
            }
            : new CycleInsightOptions();

        var configuration = new ActiveConfiguration(
            new ConfigurationStore("."),
            new OurStoryConfiguration { CycleInsight = options });

        return new CycleInsightService(client, configuration, NullLogger<CycleInsightService>.Instance);
    }

    #endregion
}
