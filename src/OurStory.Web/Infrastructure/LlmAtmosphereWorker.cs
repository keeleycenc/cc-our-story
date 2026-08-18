// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Services.LlmAtmosphere;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 负责处理氛围组的延迟互动任务，并在任务到期后触发对应角色发表评论或回复
/// </summary>
/// <remarks>
/// 调度精度按分钟计算，每 30 秒检查一次待执行任务即可满足需求。
/// 整个处理循环统一捕获异常，模型服务异常、API Key 失效或网络故障均不会影响站点正常运行，
/// 最多只会导致当前氛围组互动未能执行。
/// </remarks>
internal sealed class LlmAtmosphereWorker(
    ILlmAtmosphereScheduler scheduler,
    ActiveConfiguration configuration,
    IServiceScopeFactory scopes,
    ILogger<LlmAtmosphereWorker> logger) : BackgroundService {
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 执行后台循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try {
                await TickAsync(stoppingToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogError(exception, "处理氛围组待办时出错");
            }
        }
    }

    #region 私有方法

    private async Task TickAsync(CancellationToken cancellationToken) {
        if (!configuration.LlmAtmosphere.Enabled) {
            return;
        }

        var due = scheduler.TakeDue();
        if (due.Count == 0) {
            return;
        }

        foreach (var trigger in due) {
            await using var scope = scopes.CreateAsyncScope();
            var atmosphere = scope.ServiceProvider.GetRequiredService<ILlmAtmosphereService>();

            try {
                _ = await atmosphere.RunAsync(trigger, cancellationToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogError(
                    exception,
                    "氛围组角色 {Member} 在记录 {MomentId} 上互动时发生异常。",
                    trigger.MemberId,
                    trigger.MomentId);
            }
        }
    }

    #endregion
}
