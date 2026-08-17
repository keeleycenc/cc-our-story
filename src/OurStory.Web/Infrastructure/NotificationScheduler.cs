// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Anniversaries;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 到点提醒：每天的「想你」，以及今天 / 明天有纪念日
/// </summary>
/// <remarks>
/// 每分钟醒一次，看看有没有人的提醒时间刚好到了。两个人的站点，这点开销可以忽略。
///
/// 「今天发过没有」记在各自的偏好那一行里，不是记在内存里：
/// 站点半夜重启、或者刚好在提醒时间前后重启，都不会漏发，也不会重复打扰
/// </remarks>
public sealed class NotificationScheduler(
    IServiceScopeFactory scopes,
    ILogger<NotificationScheduler> logger) : BackgroundService {
    /// <summary>
    /// 检查间隔。提醒时间精确到分钟，一分钟一趟刚好够用
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 迟到多久就不再补发。站点停了一整天再开机，昨晚那条「想你」已经没意义了
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
                // 定时这一趟出错不能把循环带走，否则之后所有提醒都没了
                logger.LogError(exception, "检查每日提醒时出错。");
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
                && setting.RemindMinutes <= minutes
                && setting.RemindMinutes >= minutes - GraceMinutes
                && (setting.DailyMiss || setting.Anniversaries))
            .ToListAsync(cancellationToken);

        if (due.Count == 0) {
            return;
        }

        var site = await provider.GetRequiredService<ISettingsService>().GetAsync(cancellationToken);
        var queue = provider.GetRequiredService<INotificationQueue>();

        // 纪念日对两个人是同一份，最多查一次
        IReadOnlyList<AnniversaryOccurrence>? anniversaries = null;

        foreach (var setting in due) {
            if (setting.DailyMiss && setting.LastDailyMissOn != today) {
                setting.LastDailyMissOn = today;
                _ = queue.Enqueue(await DailyMissAsync(db, site, setting.UserId, cancellationToken));
            }

            if (!setting.Anniversaries || setting.LastAnniversaryOn == today) {
                continue;
            }

            setting.LastAnniversaryOn = today;
            anniversaries ??= await provider.GetRequiredService<IAnniversaryService>().GetAllAsync(cancellationToken);

            if (Upcoming(anniversaries) is { } message) {
                _ = queue.Enqueue(NotificationRequest.ToUser(NotificationTopic.Anniversary, setting.UserId, message));
            }
        }

        _ = await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<NotificationRequest> DailyMissAsync(
        OurStoryDbContext db,
        SiteSettings site,
        int userId,
        CancellationToken cancellationToken) {
        var role = await db.Users
            .Where(user => user.Id != userId && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => user.Role)
            .FirstOrDefaultAsync(cancellationToken);

        var partner = role == UserRole.Guest ? "对方" : site.RoleName(role);

        return NotificationRequest.ToUser(
            NotificationTopic.DailyMiss,
            userId,
            new PushMessage(
                $"今天想{partner}了吗",
                "回首页点一下想你，让这一天也留下记号",
                "/",
                "daily-miss"));
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
