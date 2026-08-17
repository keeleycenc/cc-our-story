// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Anniversaries;
using OurStory.Services.Notifications;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 到点提醒：今天或者明天有要过的日子
/// </summary>
internal sealed class NotificationScheduler(
    IServiceScopeFactory scopes,
    ILogger<NotificationScheduler> logger) : BackgroundService {
    /// <summary>
    /// 检查间隔。提醒时间精确到分钟，一分钟一趟刚好够用
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 迟到多久就不再补发。站点停了一整天再开机，昨晚那条提醒已经没意义了
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
                logger.LogError(exception, "检查纪念日提醒时出错");
            }
        }
    }

    #region 私有方法

    private async Task TickAsync(CancellationToken cancellationToken) {
        await using var scope = scopes.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var notifications = provider.GetRequiredService<INotificationService>();
        if (!notifications.IsConfigured) {
            return;
        }

        var db = provider.GetRequiredService<OurStoryDbContext>();
        var clock = provider.GetRequiredService<SiteClock>();

        var today = clock.TodayKey;
        var minutes = (clock.LocalNow.Hour * 60) + clock.LocalNow.Minute;

        var due = await db.NotificationSettings
            .Where(setting => setting.Enabled
                && setting.Anniversaries
                && setting.LastAnniversaryOn != today
                && setting.RemindMinutes <= minutes
                && setting.RemindMinutes >= minutes - GraceMinutes)
            .ToListAsync(cancellationToken);

        if (due.Count == 0) {
            return;
        }

        var anniversaries = await provider.GetRequiredService<IAnniversaryService>().GetAllAsync(cancellationToken);
        var message = Upcoming(anniversaries);
        var queue = provider.GetRequiredService<INotificationQueue>();

        foreach (var setting in due) {
            setting.LastAnniversaryOn = today;

            if (message is not null) {
                _ = queue.Enqueue(NotificationRequest.ToUser(NotificationTopic.Anniversary, setting.UserId, message));
            }
        }

        _ = await db.SaveChangesAsync(cancellationToken);
    }

    private static PushMessage? Upcoming(IReadOnlyList<AnniversaryOccurrence> anniversaries) {
        var soon = anniversaries
            .Where(item => item.DaysUntil is 0 or 1)
            .OrderBy(item => item.DaysUntil)
            .ToList();

        if (soon.Count == 0) {
            return null;
        }

        var first = soon[0];
        var title = first.DaysUntil == 0 ? "今天是个特别的日子" : "明天是个特别的日子";

        var body = first.Years > 0
            ? $"{first.Title} · 第 {first.Years} 年"
            : first.Title;

        if (soon.Count > 1) {
            body += $"，还有另外 {soon.Count - 1} 个日子";
        }

        return new PushMessage(title, body, first.Url, $"anniversary-due-{first.Id}");
    }

    #endregion
}
