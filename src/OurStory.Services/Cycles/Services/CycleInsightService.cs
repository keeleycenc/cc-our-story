// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.LlmAtmosphere;

namespace OurStory.Services.Cycles;

internal sealed class CycleInsightService(
    IResponsesClient client,
    ActiveConfiguration configuration,
    ILogger<CycleInsightService> logger) : ICycleInsightService {

    // 后台测试用的示例：第 3 个周期为分析目标，前两个周期作为比较基线。
    private static readonly CycleNarrativeContext Sample = new(
        new DateOnly(2026, 3, 2),
        new DateOnly(2026, 3, 6),
        5,
        29,
        1,
        CycleRhythm.Normal,
        "本次较少使用暖宝宝",
        28,
        5,
        [
            new CycleDayFact(new DateOnly(2026, 3, 2), CycleFlow.Medium, CycleMood.Tired, 2, CycleSymptom.Cramps | CycleSymptom.Backache, string.Empty),
            new CycleDayFact(new DateOnly(2026, 3, 3), CycleFlow.Heavy, CycleMood.Low, 2, CycleSymptom.Cramps, "下午休息后有所缓解"),
            new CycleDayFact(new DateOnly(2026, 3, 5), CycleFlow.Light, CycleMood.Calm, 0, CycleSymptom.None, string.Empty)
        ],
        3,
        [
            new CyclePastFact(
                1,
                new DateOnly(2026, 1, 4),
                new DateOnly(2026, 1, 8),
                5,
                null,
                string.Empty,
                [
                    new CycleDayFact(new DateOnly(2026, 1, 4), CycleFlow.Medium, CycleMood.Calm, 1, CycleSymptom.Cramps, string.Empty),
                    new CycleDayFact(new DateOnly(2026, 1, 6), CycleFlow.Light, CycleMood.Calm, 0, CycleSymptom.None, string.Empty)
                ]),
            new CyclePastFact(
                2,
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 2, 5),
                5,
                28,
                "这次一直用着暖宝宝",
                [
                    new CycleDayFact(new DateOnly(2026, 2, 1), CycleFlow.Heavy, CycleMood.Tired, 2, CycleSymptom.Cramps | CycleSymptom.Backache, string.Empty),
                    new CycleDayFact(new DateOnly(2026, 2, 3), CycleFlow.Medium, CycleMood.Low, 1, CycleSymptom.Cramps, string.Empty)
                ])
        ]);

    public bool UsesModel => configuration.CycleInsight.IsUsable;

    public async Task<CycleSummaryText> WriteAsync(
        CycleNarrativeContext context,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);

        var fallback = new CycleSummaryText(CycleNarrative.Compose(context), CycleSummarySource.Rule, null);
        if (!UsesModel) {
            return fallback;
        }

        var text = await AskAsync(context, cancellationToken);
        return text.Length == 0
            ? fallback
            : new CycleSummaryText(text, CycleSummarySource.Model, SiteClock.UtcNow);
    }

    public async Task<CycleInsightProbe> ProbeAsync(
        CycleNarrativeContext? context = null,
        CancellationToken cancellationToken = default) {
        var options = configuration.CycleInsight;
        if (!options.IsConfigured) {
            return CycleInsightProbe.Failed("请先完整填写服务地址、模型名称和 API Key。");
        }

        var result = await client.CompleteAsync(Request(context ?? Sample), cancellationToken);
        if (!result.IsSuccess) {
            return CycleInsightProbe.Failed(result.Failure.Describe());
        }

        var text = CycleNarrative.Clean(result.Text);
        return text.Length == 0
            ? CycleInsightProbe.Failed("模型返回的内容经清理后为空，请检查模型与提示词配置。")
            : CycleInsightProbe.Success(text, context is not null);
    }

    #region 私有方法

    private async Task<string> AskAsync(CycleNarrativeContext context, CancellationToken cancellationToken) {
        try {
            var result = await client.CompleteAsync(Request(context), cancellationToken);
            if (result.IsSuccess) {
                return CycleNarrative.Clean(result.Text);
            }

            logger.LogInformation("花信小结模型调用未成功（{Failure}），已使用站内规则小结。", result.Failure);
            return string.Empty;
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            logger.LogWarning(exception, "花信小结模型调用发生异常，已使用站内规则小结。");
            return string.Empty;
        }
    }

    private ResponsesRequest Request(CycleNarrativeContext context) {
        var options = configuration.CycleInsight;

        return new ResponsesRequest(
            new ResponsesEndpoint(
                "花信小结",
                options.BaseUrl,
                options.Model,
                options.ApiKey,
                options.MaxOutputTokens,
                options.TimeoutSeconds),
            CycleNarrative.Instructions(options.Tone),
            CycleNarrative.Input(context));
    }

    #endregion
}
