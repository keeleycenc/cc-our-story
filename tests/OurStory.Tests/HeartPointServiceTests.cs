// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.HeartPoints;
using Xunit;

namespace OurStory.Tests;

/// <summary>心意流水的记账规则</summary>
public class HeartPointServiceTests {
    [Fact]
    public async Task 同一天同一种奖励只发一次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);

        var first = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-15");
        var second = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-15");
        var nextDay = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-16");

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, nextDay);
        Assert.Equal(4, await service.GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 不同来头当天各发一次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);

        _ = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-15");
        _ = await service.AwardDailyAsync(boyId, HeartPointReason.MomentPublished, "2026-08-15");
        _ = await service.AwardDailyAsync(boyId, HeartPointReason.AnniversaryPublished, "2026-08-15");

        Assert.Equal(2 + 8 + 12, await service.GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 余额等于全部流水之和() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);

        _ = await service.AwardDailyAsync(boyId, HeartPointReason.AnniversaryPublished, "2026-08-15");
        _ = harness.Db.HeartPointEntries.Add(new HeartPointEntry {
            UserId = boyId,
            ChangeAmount = -5,
            Reason = HeartPointReason.Purchase,
            SourceKey = "purchase:1",
            Note = "兑换「洗碗券」"
        });
        _ = await harness.Db.SaveChangesAsync();

        var balance = (await service.GetBalancesAsync()).Single(item => item.Role == UserRole.Boy);

        Assert.Equal(7, balance.Balance);
        Assert.Equal(12, balance.Earned);
        Assert.Equal(5, balance.Spent);
    }

    [Fact]
    public async Task 初始心意按天补记历史记录() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();

        harness.Db.Heartbeats.AddRange(
            Beat(boyId, "2026-08-01"),
            Beat(boyId, "2026-08-01"),
            Beat(boyId, "2026-08-02"));

        // 同一天两条点点滴滴，补记时只算一次
        harness.Db.Moments.AddRange(
            Moment(girlId, new DateTimeOffset(2026, 8, 3, 1, 0, 0, TimeSpan.Zero)),
            Moment(girlId, new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero)));

        _ = harness.Db.Anniversaries.Add(new Anniversary {
            Title = "第一次见面",
            AnniversaryDate = new DateOnly(2026, 8, 4),
            AuthorId = girlId,
            CreatedAt = new DateTimeOffset(2026, 8, 4, 5, 0, 0, TimeSpan.Zero)
        });
        _ = await harness.Db.SaveChangesAsync();

        var service = Service(harness);
        var result = await service.BackfillAsync();

        Assert.False(result.AlreadyDone);
        Assert.Equal(4, result.Entries);              // 想你两天 + 点点滴滴一天 + 纪念日一天
        Assert.Equal(2 + 2 + 8 + 12, result.Total);
        Assert.Equal(4, await service.GetBalanceAsync(boyId));
        Assert.Equal(20, await service.GetBalanceAsync(girlId));
        Assert.True(await harness.Db.HeartPointEntries.AllAsync(entry => entry.IsBackfill));
    }

    [Fact]
    public async Task 初始心意不会算第二遍() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = harness.Db.Heartbeats.Add(Beat(boyId, "2026-08-01"));
        _ = await harness.Db.SaveChangesAsync();

        var service = Service(harness);
        _ = await service.BackfillAsync();
        var again = await service.BackfillAsync();

        Assert.True(again.AlreadyDone);
        Assert.Equal(0, again.Entries);
        Assert.Equal(2, await service.GetBalanceAsync(boyId));
        Assert.True(await service.IsBackfilledAsync());
    }

    [Fact]
    public async Task 补记过的日子不会被重复发放() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = harness.Db.Heartbeats.Add(Beat(boyId, "2026-08-01"));
        _ = await harness.Db.SaveChangesAsync();

        var service = Service(harness);
        _ = await service.BackfillAsync();
        var again = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-01");

        Assert.Equal(0, again);
        Assert.Equal(2, await service.GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 想你点满当天拿双倍() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);

        var first = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-15");
        var full = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeatFull, "2026-08-15");
        var again = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeatFull, "2026-08-15");

        Assert.Equal(2, first);
        Assert.Equal(2, full);
        Assert.Equal(0, again);
        Assert.Equal(4, await service.GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 初始心意也补记点满那一份() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();

        // 上限设成 3：8-01 点满了，8-02 只点了两下
        harness.Db.Heartbeats.AddRange(
            Beat(boyId, "2026-08-01"), Beat(boyId, "2026-08-01"), Beat(boyId, "2026-08-01"),
            Beat(boyId, "2026-08-02"), Beat(boyId, "2026-08-02"));
        _ = await harness.Db.SaveChangesAsync();

        var result = await Service(harness, new SiteSettings { HeartbeatDailyLimit = 3 }).BackfillAsync();

        // 8-01 给两份、8-02 给一份
        Assert.Equal(3, result.Entries);
        Assert.Equal(6, result.Total);
    }

    [Fact]
    public async Task 奖励配成零时不记流水() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness, new SiteSettings { RewardHeartbeat = 0 });

        var amount = await service.AwardDailyAsync(boyId, HeartPointReason.DailyHeartbeat, "2026-08-15");

        Assert.Equal(0, amount);
        Assert.Equal(0, await harness.Db.HeartPointEntries.CountAsync());
    }

    private static Heartbeat Beat(int userId, string day) => new() {
        Role = UserRole.Boy,
        UserId = userId,
        ClickDay = day,
        CreatedAt = DateTimeOffset.Parse(day + "T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
    };

    private static Moment Moment(int authorId, DateTimeOffset createdAt) => new() {
        Title = "一段回忆",
        Slug = "moment-" + Guid.NewGuid().ToString("n")[..8],
        Status = MomentStatus.Published,
        AuthorId = authorId,
        MomentDate = createdAt,
        CreatedAt = createdAt
    };

    private static HeartPointService Service(SqliteHarness harness, SiteSettings? settings = null) =>
        new(harness.Db, new SettingsStub(settings), TestDoubles.Clock());
}
