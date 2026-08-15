// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.HeartPoints;
using OurStory.Services.Settings;

namespace OurStory.Services.Heartbeats;

internal class HeartbeatService(
    OurStoryDbContext db,
    ISettingsService settings,
    IHeartPointService heartPoints,
    SiteClock clock) : IHeartbeatService {
    private const int BatchLimit = 20;  // 一次请求最多接受多少个点击
    private const int MaxAgeMs = 120_000;   // 客户端说「多少毫秒之前点的」，最多认到这个值，再早的按这个算
    private const int DayWindow = 3000; // 算连续天数时最多回溯多少天

    public async Task<HeartbeatSummary> GetSummaryAsync(VisitorIdentity who, CancellationToken cancellationToken = default) {
        var site = await settings.GetAsync(cancellationToken);
        var today = clock.TodayKey;

        var totals = await TotalsAsync(cancellationToken);
        var daily = await SelfDailyCountsAsync(who, cancellationToken);

        var self = new HeartbeatStats {
            Total = daily.Values.Sum(),
            Today = daily.GetValueOrDefault(today),
            Streak = HeartbeatHelper.CalculateStreak([.. daily.Keys], today),
            Best = daily.Count == 0 ? 0 : daily.Values.Max()
        };

        HeartbeatStats display;
        if (who.Role == UserRole.Guest) {
            // 访客看到的是「所有路过的人」加起来的样子，而不是自己那一份
            var guestDaily = await RoleDailyCountsAsync(UserRole.Guest, cancellationToken);
            display = new HeartbeatStats {
                Total = totals.GetValueOrDefault("guest"),
                Today = guestDaily.GetValueOrDefault(today),
                Streak = await GuestPeopleCountAsync(cancellationToken),
                Best = guestDaily.Count == 0 ? 0 : guestDaily.Values.Max()
            };
        } else {
            // 双方账号的第三项展示「心动天数」，也就是历史上有过点击的去重日期数
            display = new HeartbeatStats {
                Total = self.Total,
                Today = self.Today,
                Streak = self.Streak,
                Best = daily.Count
            };
        }

        return new HeartbeatSummary {
            Role = HeartbeatHelper.RoleKey(who.Role),
            Today = today,
            DailyLimit = site.HeartbeatDailyLimit,
            Totals = totals,
            Self = self,
            Display = display
        };
    }

    public async Task<HeartbeatRecordResult> RecordAsync(VisitorIdentity who, IReadOnlyList<int> agesMs, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(agesMs);

        var now = DateTimeOffset.UtcNow;
        var times = agesMs
            .Take(BatchLimit)
            .Select(age => now.AddMilliseconds(-Math.Clamp(age, 0, MaxAgeMs)))
            .OrderBy(time => time)
            .ToList();

        if (times.Count == 0) {
            return new HeartbeatRecordResult(0, false);
        }

        var site = await settings.GetAsync(cancellationToken);
        var quotaByDay = new Dictionary<string, int>(StringComparer.Ordinal);
        var accepted = new List<DateTimeOffset>();

        foreach (var time in times) {
            var day = clock.DayKey(time);
            if (!quotaByDay.TryGetValue(day, out var remaining)) {
                remaining = Math.Max(site.HeartbeatDailyLimit - await UsedTodayAsync(who, day, cancellationToken), 0);
                quotaByDay[day] = remaining;
            }

            if (remaining <= 0) {
                continue;
            }

            accepted.Add(time);
            quotaByDay[day] = remaining - 1;
        }

        if (accepted.Count > 0) {
            db.Heartbeats.AddRange(accepted.Select(time => new Heartbeat {
                Role = who.Role,
                UserId = who.UserId,
                VisitorHash = who.VisitorHash ?? string.Empty,
                CreatedAt = time,
                ClickDay = clock.DayKey(time)
            }));

            _ = await db.SaveChangesAsync(cancellationToken);
            await AwardAsync(who, accepted, cancellationToken);
        }

        return new HeartbeatRecordResult(accepted.Count, accepted.Count < times.Count);
    }

    #region 私有方法

    private async Task AwardAsync(VisitorIdentity who, List<DateTimeOffset> accepted, CancellationToken cancellationToken) {
        if (who.UserId is not { } userId || who.Role is not (UserRole.Boy or UserRole.Girl)) {
            return;
        }

        foreach (var day in accepted.Select(clock.DayKey).Distinct(StringComparer.Ordinal)) {
            _ = await heartPoints.AwardDailyAsync(userId, HeartPointReason.DailyHeartbeat, day, cancellationToken);
        }
    }

    private async Task<Dictionary<string, int>> TotalsAsync(CancellationToken cancellationToken) {
        var rows = await db.Heartbeats
            .GroupBy(beat => beat.Role)
            .Select(group => new { Role = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<string, int>(StringComparer.Ordinal) {
            ["boy"] = 0,
            ["girl"] = 0,
            ["guest"] = 0
        };

        foreach (var row in rows) {
            totals[HeartbeatHelper.RoleKey(row.Role)] = row.Count;
        }

        return totals;
    }

    private async Task<Dictionary<string, int>> SelfDailyCountsAsync(VisitorIdentity who, CancellationToken cancellationToken) {
        var rows = await Own(who)
            .GroupBy(beat => beat.ClickDay)
            .Select(group => new { Day = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Day)
            .Take(DayWindow)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Day, row => row.Count, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, int>> RoleDailyCountsAsync(UserRole role, CancellationToken cancellationToken) {
        var rows = await db.Heartbeats
            .Where(beat => beat.Role == role)
            .GroupBy(beat => beat.ClickDay)
            .Select(group => new { Day = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Day)
            .Take(DayWindow)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Day, row => row.Count, StringComparer.Ordinal);
    }

    private Task<int> GuestPeopleCountAsync(CancellationToken cancellationToken) =>
        db.Heartbeats
            .Where(beat => beat.Role == UserRole.Guest)
            .Select(beat => new { beat.UserId, beat.VisitorHash })
            .Distinct()
            .CountAsync(cancellationToken);

    private Task<int> UsedTodayAsync(VisitorIdentity who, string day, CancellationToken cancellationToken) =>
        Own(who).Where(beat => beat.ClickDay == day).CountAsync(cancellationToken);

    private IQueryable<Heartbeat> Own(VisitorIdentity who) =>
        who.UserId is { } userId
            ? db.Heartbeats.Where(beat => beat.UserId == userId)
            : db.Heartbeats.Where(beat => beat.VisitorHash == who.VisitorHash);

    #endregion
}
