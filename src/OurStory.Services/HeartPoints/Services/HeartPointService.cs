// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Settings;

namespace OurStory.Services.HeartPoints;

internal class HeartPointService(
    OurStoryDbContext db,
    ISettingsService settings,
    SiteClock clock) : IHeartPointService {
    /// <summary>
    /// 算过「初始心意」的时间，存在设置表里，有值就不会再算第二遍
    /// </summary>
    public const string BackfilledAtKey = "heart.backfilledAt";

    public async Task<int> GetBalanceAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.HeartPointEntries
            .Where(entry => entry.UserId == userId)
            .SumAsync(entry => entry.ChangeAmount, cancellationToken);

    public async Task<IReadOnlyList<HeartPointBalance>> GetBalancesAsync(CancellationToken cancellationToken = default) {
        var site = await settings.GetAsync(cancellationToken);

        var users = await db.Users
            .Where(user => user.Role == UserRole.Boy || user.Role == UserRole.Girl)
            .Select(user => new { user.Id, user.Role })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sums = await db.HeartPointEntries
            .GroupBy(entry => entry.UserId)
            .Select(group => new {
                UserId = group.Key,
                Earned = group.Where(entry => entry.ChangeAmount > 0).Sum(entry => entry.ChangeAmount),
                Spent = group.Where(entry => entry.ChangeAmount < 0).Sum(entry => entry.ChangeAmount)
            })
            .ToListAsync(cancellationToken);

        return [.. users
            .OrderBy(user => user.Role)
            .Select(user => {
                var sum = sums.FirstOrDefault(item => item.UserId == user.Id);
                var earned = sum?.Earned ?? 0;
                var spent = -(sum?.Spent ?? 0);
                return new HeartPointBalance(user.Role, site.RoleName(user.Role), earned - spent, earned, spent);
            })];
    }

    public async Task<PagedList<HeartPointRecord>> GetRecordsAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default) {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = db.HeartPointEntries.Where(entry => entry.UserId == userId);
        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var records = rows
            .Select(entry => new HeartPointRecord(
                entry.Id,
                entry.ChangeAmount,
                entry.Reason,
                entry.Note,
                entry.IsBackfill,
                clock.ToLocal(entry.CreatedAt)))
            .ToList();

        return new PagedList<HeartPointRecord>(records, page, pageSize, total);
    }

    public async Task<int> AwardDailyAsync(int userId, HeartPointReason reason, string day, CancellationToken cancellationToken = default) {
        var amount = await AmountOfAsync(reason, cancellationToken);
        if (amount <= 0) {
            return 0;
        }

        var entry = new HeartPointEntry {
            UserId = userId,
            ChangeAmount = amount,
            Reason = reason,
            SourceKey = SourceKeyOf(reason, day),
            Note = NoteOf(reason),
            CreatedAt = SiteClock.UtcNow
        };

        return await TryInsertAsync(entry, cancellationToken) ? amount : 0;
    }

    public async Task<bool> IsBackfilledAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrWhiteSpace(await settings.GetRawAsync(BackfilledAtKey, cancellationToken));

    public async Task<HeartPointBackfillResult> BackfillAsync(CancellationToken cancellationToken = default) {
        if (await IsBackfilledAsync(cancellationToken)) {
            return new HeartPointBackfillResult(true, 0, 0);
        }

        var site = await settings.GetAsync(cancellationToken);
        var owners = await db.Users
            .Where(user => user.Role == UserRole.Boy || user.Role == UserRole.Girl)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var days = new List<(int UserId, HeartPointReason Reason, string Day)>();
        days.AddRange(await HeartbeatDaysAsync(owners, site.HeartbeatDailyLimit, cancellationToken));
        days.AddRange(await MomentDaysAsync(owners, cancellationToken));
        days.AddRange(await AnniversaryDaysAsync(owners, cancellationToken));

        var now = SiteClock.UtcNow;
        var inserted = 0;
        var total = 0;

        foreach (var (userId, reason, day) in days) {
            var amount = AmountOf(site, reason);
            if (amount <= 0) {
                continue;
            }

            var entry = new HeartPointEntry {
                UserId = userId,
                ChangeAmount = amount,
                Reason = reason,
                SourceKey = SourceKeyOf(reason, day),
                Note = $"初始心意 · {NoteOf(reason)}",
                IsBackfill = true,
                CreatedAt = now
            };

            if (await TryInsertAsync(entry, cancellationToken)) {
                inserted++;
                total += amount;
            }
        }

        await settings.SetRawAsync(BackfilledAtKey, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        return new HeartPointBackfillResult(false, inserted, total);
    }

    #region 私有方法

    private async Task<bool> TryInsertAsync(HeartPointEntry entry, CancellationToken cancellationToken) {
        _ = db.HeartPointEntries.Add(entry);

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
            return true;
        } catch (DbUpdateException) {
            db.Entry(entry).State = EntityState.Detached;
            return false;
        }
    }

    private async Task<int> AmountOfAsync(HeartPointReason reason, CancellationToken cancellationToken) =>
        AmountOf(await settings.GetAsync(cancellationToken), reason);

    private static int AmountOf(SiteSettings site, HeartPointReason reason) => reason switch {
        HeartPointReason.DailyHeartbeat => site.RewardHeartbeat,
        HeartPointReason.DailyHeartbeatFull => site.RewardHeartbeat,
        HeartPointReason.MomentPublished => site.RewardMoment,
        HeartPointReason.AnniversaryPublished => site.RewardAnniversary,
        _ => 0
    };

    private static string SourceKeyOf(HeartPointReason reason, string day) => reason switch {
        HeartPointReason.DailyHeartbeat => $"heartbeat:{day}",
        HeartPointReason.DailyHeartbeatFull => $"heartbeat-full:{day}",
        HeartPointReason.MomentPublished => $"moment:{day}",
        HeartPointReason.AnniversaryPublished => $"anniversary:{day}",
        _ => $"other:{day}"
    };

    private static string NoteOf(HeartPointReason reason) => reason switch {
        HeartPointReason.DailyHeartbeat => "今天想你了",
        HeartPointReason.DailyHeartbeatFull => "今天想了你好多次",
        HeartPointReason.MomentPublished => "写下一条点点滴滴",
        HeartPointReason.AnniversaryPublished => "记下一个纪念日",
        _ => "心意变动"
    };

    private async Task<List<(int, HeartPointReason, string)>> HeartbeatDaysAsync(
        List<int> owners,
        int dailyLimit,
        CancellationToken cancellationToken) {
        var rows = await db.Heartbeats
            .Where(beat => beat.UserId != null && owners.Contains(beat.UserId.Value))
            .GroupBy(beat => new { UserId = beat.UserId!.Value, beat.ClickDay })
            .Select(group => new { group.Key.UserId, group.Key.ClickDay, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var days = new List<(int, HeartPointReason, string)>(rows.Count);
        foreach (var row in rows) {
            days.Add((row.UserId, HeartPointReason.DailyHeartbeat, row.ClickDay));
            if (row.Count >= dailyLimit) {
                days.Add((row.UserId, HeartPointReason.DailyHeartbeatFull, row.ClickDay));
            }
        }

        return days;
    }

    private async Task<List<(int, HeartPointReason, string)>> MomentDaysAsync(List<int> owners, CancellationToken cancellationToken) {
        var rows = await db.Moments
            .Where(moment => moment.Status == MomentStatus.Published && owners.Contains(moment.AuthorId))
            .Select(moment => new { moment.AuthorId, moment.CreatedAt })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(row => (row.AuthorId, HeartPointReason.MomentPublished, clock.DayKey(row.CreatedAt)))
            .Distinct()];
    }

    private async Task<List<(int, HeartPointReason, string)>> AnniversaryDaysAsync(List<int> owners, CancellationToken cancellationToken) {
        var rows = await db.Anniversaries
            .Where(item => item.AuthorId != null && owners.Contains(item.AuthorId.Value))
            .Select(item => new { AuthorId = item.AuthorId!.Value, item.CreatedAt })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(row => (row.AuthorId, HeartPointReason.AnniversaryPublished, clock.DayKey(row.CreatedAt)))
            .Distinct()];
    }

    #endregion
}
