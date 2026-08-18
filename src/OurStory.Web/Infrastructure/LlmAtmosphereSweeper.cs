// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Services.LlmAtmosphere;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 定期检查近期记录，并补充触发符合条件的氛围组互动
/// </summary>
/// <remarks>
/// 延迟任务仅保存在内存中，站点重启后可能丢失。
/// 此检查既用于补偿遗漏任务，也允许氛围组在记录发布较长时间后继续产生自然互动。
/// </remarks>
internal sealed class LlmAtmosphereSweeper(
    ILlmAtmosphereScheduler scheduler,
    ActiveConfiguration configuration,
    IServiceScopeFactory scopes,
    ILogger<LlmAtmosphereSweeper> logger) : BackgroundService {
    private static readonly TimeSpan Warmup = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 执行后台循环
    /// </summary>
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
                logger.LogError(exception, "巡检氛围组互动时出错");
            }

            var minutes = Math.Clamp(configuration.LlmAtmosphere.SweepMinutes, 1, 60 * 12);

            try {
                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    #region 私有方法

    private async Task TickAsync(CancellationToken cancellationToken) {
        if (!configuration.LlmAtmosphere.Enabled) {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var atmosphere = scope.ServiceProvider.GetRequiredService<ILlmAtmosphereService>();

        var planned = await atmosphere.SweepAsync(cancellationToken);
        var scheduled = planned.Count(scheduler.Schedule);

        if (scheduled > 0) {
            logger.LogInformation("氛围组巡检排了 {Count} 件待办。", scheduled);
        }
    }

    #endregion
}
