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

        // 事实变化后，已保存的小结失效，页面立即回退到规则文案。
        _ = await service.UpdateAsync(boyId, created.RecordId!.Value, start, start.AddDays(4), "改过的备注");
        var stale = (await service.GetDashboardAsync(boyId, 1, 10, today.Year, today.Month)).History.Items[0];
        Assert.False(stale.Summary.FromModel);
        Assert.Contains("改过的备注", stale.Summary.Text, StringComparison.Ordinal);

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

    #region 私有方法

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
