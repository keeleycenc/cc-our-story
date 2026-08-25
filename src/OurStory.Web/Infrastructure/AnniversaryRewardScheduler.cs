// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Time;
using OurStory.Services.Anniversaries;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 每天凌晨盘一遍当天的纪念日，按分类给两个人发心意
/// </summary>
internal sealed class AnniversaryRewardScheduler(
    SiteClock clock,
    IServiceScopeFactory scopes,
    ILogger<AnniversaryRewardScheduler> logger) : BackgroundService {
    private static readonly TimeSpan MaxDelay = TimeSpan.FromHours(6);

    private static readonly TimeSpan PastMidnight = TimeSpan.FromMinutes(1);

    private string _lastDay = string.Empty;

    /// <summary>
    /// 执行后台循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await TickAsync(stoppingToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogError(exception, "发放纪念日心意时出错");
            }

            try {
                await Task.Delay(NextDelay(), stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    #region 私有方法

    private async Task TickAsync(CancellationToken cancellationToken) {
        var today = clock.TodayKey;
        if (string.Equals(today, _lastDay, StringComparison.Ordinal)) {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var rewards = scope.ServiceProvider.GetRequiredService<IAnniversaryRewardService>();

        var result = await rewards.AwardForDayAsync(clock.Today, cancellationToken);
        _lastDay = today;

        if (result.Total > 0) {
            logger.LogInformation(
                "{Day} 有 {Count} 个纪念日，发出 {Entries} 笔共 {Total} 心意。",
                result.Day,
                result.Anniversaries,
                result.Entries,
                result.Total);
        }
    }

    private TimeSpan NextDelay() {
        var now = clock.LocalNow;
        var untilMidnight = now.Date.AddDays(1).Add(PastMidnight) - now;
        return untilMidnight > MaxDelay ? MaxDelay : untilMidnight;
    }

    #endregion
}
