// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Services.Cycles;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 在后台补充缺失或已失效的周期小结
/// </summary>
/// <remarks>
/// 页面请求不直接调用模型，以免模型响应时间影响页面加载。
/// 页面优先显示站内规则小结，模型小结生成后将在后续刷新时展示。
/// </remarks>
internal sealed class CycleInsightWorker(
    ActiveConfiguration configuration,
    IServiceScopeFactory scopes,
    ILogger<CycleInsightWorker> logger) : BackgroundService {
    /// <summary>
    /// 站点启动后等待一段时间，避免与迁移和种子数据初始化并发执行
    /// </summary>
    private static readonly TimeSpan Warmup = TimeSpan.FromMinutes(3);

    /// <summary>
    /// 单轮补写数量上限，用于控制模型调用频率
    /// </summary>
    private const int Batch = 4;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await Task.Delay(Warmup, stoppingToken);
        } catch (OperationCanceledException) {
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await TickAsync(stoppingToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogError(exception, "补写花信小结时发生异常");
            }

            var hours = Math.Clamp(configuration.CycleInsight.RefreshHours, 1, 168);

            try {
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    #region 私有方法

    private async Task TickAsync(CancellationToken cancellationToken) {
        if (!configuration.CycleInsight.IsUsable) {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var cycles = scope.ServiceProvider.GetRequiredService<ICycleService>();
        var written = await cycles.RefreshSummariesAsync(Batch, cancellationToken);

        if (written > 0) {
            logger.LogInformation("花信小结补写了 {Count} 条。", written);
        }
    }

    #endregion
}
