// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Cycles;
using OurStory.Services.Notifications;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 定时生成花信预测窗口与进行中记录的状态提醒
/// </summary>
/// <remarks>
/// 花信提醒与纪念日提醒共用用户设置的发送时刻，每日最多处理一次。
/// 对方新增或更新记录时产生的即时通知不由此调度器处理。
/// </remarks>
internal sealed class CycleReminderScheduler(
    IServiceScopeFactory scopes,
    ILogger<CycleReminderScheduler> logger) : BackgroundService {
    /// <summary>
    /// 获取调度检查间隔；提醒时刻精确到分钟
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 获取允许延迟补发的最大分钟数，超过该时限的提醒不再补发
    /// </summary>
    private const int GraceMinutes = 30;

    /// <summary>
    /// 执行后台循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try {
                await TickAsync(stoppingToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogError(exception, "花信如期定时提醒检查失败");
            }
        }
    }

    #region 私有方法

    private async Task TickAsync(CancellationToken cancellationToken) {
        await using var scope = scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var notifications = provider.GetRequiredService<INotificationService>();
        if (!notifications.HasConfiguredChannel) {
            return;
        }

        var db = provider.GetRequiredService<OurStoryDbContext>();
        var clock = provider.GetRequiredService<SiteClock>();

        var today = clock.TodayKey;
        var minutes = (clock.LocalNow.Hour * 60) + clock.LocalNow.Minute;

        var due = await db.NotificationSettings
            .Where(setting => setting.Enabled
                && setting.Cycle
                && setting.LastCycleOn != today
                && setting.RemindMinutes <= minutes
                && setting.RemindMinutes >= minutes - GraceMinutes)
            .ToListAsync(cancellationToken);

        if (due.Count == 0) {
            return;
        }

        var cycles = provider.GetRequiredService<ICycleService>();
        var queue = provider.GetRequiredService<INotificationQueue>();

        foreach (var setting in due) {
            // 标记当天已完成检查，避免在无待发送提醒时重复计算。
            setting.LastCycleOn = today;

            foreach (var reminder in await cycles.GetDueRemindersAsync(setting.UserId, cancellationToken)) {
                _ = queue.Enqueue(NotificationRequest.ToUser(
                    NotificationTopic.Cycle,
                    setting.UserId,
                    new PushMessage(reminder.Title, reminder.Body, "/cycles", reminder.Tag)));
            }
        }

        _ = await db.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
