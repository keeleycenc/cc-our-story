// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Anniversaries;
using OurStory.Services.HeartPoints;
using Xunit;

namespace OurStory.Tests;

/// <summary>纪念日当天的心意发放规则</summary>
public class AnniversaryRewardServiceTests {
    private static readonly DateOnly Today = new(2026, 8, 25);

    [Fact]
    public async Task 当天的纪念日按分类给两个人各发一份() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);

        var result = await Service(harness).AwardForDayAsync(Today);

        Assert.Equal("2026-08-25", result.Day);
        Assert.Equal(1, result.Anniversaries);
        Assert.Equal(2, result.Entries);
        Assert.Equal(20, result.Total);
        Assert.Equal(10, await Points(harness).GetBalanceAsync(boyId));
        Assert.Equal(10, await Points(harness).GetBalanceAsync(girlId));
    }

    [Fact]
    public async Task 一天有几个纪念日就发几份() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);          // 10
        _ = await AddAsync(harness, "她的生日", new DateOnly(2000, 8, 25), AnniversaryKind.Birthday);    // 8
        _ = await AddAsync(harness, "第一次旅行", new DateOnly(2025, 8, 25), AnniversaryKind.Travel);    // 5

        var result = await Service(harness).AwardForDayAsync(Today);

        Assert.Equal(3, result.Anniversaries);
        Assert.Equal(6, result.Entries);
        Assert.Equal((10 + 8 + 5) * 2, result.Total);
        Assert.Equal(23, await Points(harness).GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 没配到的分类拿保底那一份() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "随手记的日子", new DateOnly(2025, 8, 25), AnniversaryKind.Custom);

        _ = await Service(harness).AwardForDayAsync(Today);

        Assert.Equal(3, await Points(harness).GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 同一天跑第二遍不会重复发放() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);
        var service = Service(harness);

        _ = await service.AwardForDayAsync(Today);
        var again = await service.AwardForDayAsync(Today);

        Assert.Equal(1, again.Anniversaries);
        Assert.Equal(0, again.Entries);
        Assert.Equal(0, again.Total);
        Assert.Equal(10, await Points(harness).GetBalanceAsync(boyId));
        Assert.Equal(2, await harness.Db.HeartPointEntries.CountAsync());
    }

    [Fact]
    public async Task 明年同一天再发一次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);
        var service = Service(harness);

        _ = await service.AwardForDayAsync(Today);
        var nextYear = await service.AwardForDayAsync(new DateOnly(2027, 8, 25));

        Assert.Equal(20, nextYear.Total);
        Assert.Equal(20, await Points(harness).GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 不是纪念日的那天什么都不发() {
        await using var harness = SqliteHarness.Create();
        _ = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);

        var result = await Service(harness).AwardForDayAsync(new DateOnly(2026, 8, 24));

        Assert.Equal(0, result.Anniversaries);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, await harness.Db.HeartPointEntries.CountAsync());
    }

    [Fact]
    public async Task 一次性纪念日只在当年那天发() {
        await using var harness = SqliteHarness.Create();
        _ = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "领证那天", new DateOnly(2026, 8, 25), AnniversaryKind.Wedding, repeatYearly: false);
        var service = Service(harness);

        var onDay = await service.AwardForDayAsync(Today);
        var nextYear = await service.AwardForDayAsync(new DateOnly(2027, 8, 25));

        Assert.Equal(20, onDay.Total);
        Assert.Equal(0, nextYear.Anniversaries);
    }

    [Fact]
    public async Task 纪念日开始那年之前不发() {
        await using var harness = SqliteHarness.Create();
        _ = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "在一起", new DateOnly(2027, 8, 25), AnniversaryKind.Love);

        var result = await Service(harness).AwardForDayAsync(Today);

        Assert.Equal(0, result.Anniversaries);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task 农历纪念日跟着农历那天发() {
        await using var harness = SqliteHarness.Create();
        _ = await harness.SeedCoupleAsync();
        _ = await AddAsync(
            harness,
            "第一次见面",
            ChineseLunarCalendar.ToSolar(new ChineseLunarDate(2024, 1, 1)),
            AnniversaryKind.FirstMeeting,
            calendarType: AnniversaryCalendarType.Lunar);
        var service = Service(harness);

        // 2026 年正月初一是 2026-02-17，原公历月日 2024-02-10 那天不算
        var wrongDay = await service.AwardForDayAsync(new DateOnly(2026, 2, 10));
        var lunarNewYear = await service.AwardForDayAsync(new DateOnly(2026, 2, 17));

        Assert.Equal(0, wrongDay.Anniversaries);
        Assert.Equal(1, lunarNewYear.Anniversaries);
        Assert.Equal(16, lunarNewYear.Total);
    }

    [Fact]
    public async Task 私密纪念日照样发() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        _ = await AddAsync(harness, "只有我们知道", new DateOnly(2024, 8, 25), AnniversaryKind.Promise, isPrivate: true);

        _ = await Service(harness).AwardForDayAsync(Today);

        Assert.Equal(5, await Points(harness).GetBalanceAsync(boyId));
    }

    [Fact]
    public async Task 还没有男女主时不发() {
        await using var harness = SqliteHarness.Create();
        _ = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);

        var result = await Service(harness).AwardForDayAsync(Today);

        Assert.Equal(0, result.Anniversaries);
        Assert.Equal(0, await harness.Db.HeartPointEntries.CountAsync());
    }

    [Fact]
    public async Task 流水记在纪念日这个来头上() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var id = await AddAsync(harness, "在一起", new DateOnly(2024, 8, 25), AnniversaryKind.Love);

        _ = await Service(harness).AwardForDayAsync(Today);

        var entry = await harness.Db.HeartPointEntries.SingleAsync(item => item.UserId == boyId);
        Assert.Equal(HeartPointReason.AnniversaryDay, entry.Reason);
        Assert.Equal($"anniversary-day:2026-08-25:{id}", entry.SourceKey);
        Assert.Equal("纪念日 · 在一起", entry.Note);
    }

    [Theory]
    [InlineData(AnniversaryKind.Love, 10)]
    [InlineData(AnniversaryKind.Wedding, 10)]
    [InlineData(AnniversaryKind.Birthday, 8)]
    [InlineData(AnniversaryKind.FirstMeeting, 8)]
    [InlineData(AnniversaryKind.Milestone, 8)]
    [InlineData(AnniversaryKind.Travel, 5)]
    [InlineData(AnniversaryKind.Festival, 5)]
    [InlineData(AnniversaryKind.Promise, 5)]
    [InlineData(AnniversaryKind.Family, 5)]
    [InlineData(AnniversaryKind.Custom, 3)]
    public void 分类决定基础奖励(AnniversaryKind kind, int expected) =>
        Assert.Equal(expected, HeartPointRules.AnniversaryReward(kind));

    private static async Task<int> AddAsync(
        SqliteHarness harness,
        string title,
        DateOnly date,
        AnniversaryKind kind,
        bool repeatYearly = true,
        bool isPrivate = false,
        AnniversaryCalendarType calendarType = AnniversaryCalendarType.Solar) {
        var item = new Anniversary {
            Title = title,
            AnniversaryDate = date,
            Kind = kind,
            CalendarType = calendarType,
            RepeatYearly = repeatYearly,
            IsPrivate = isPrivate
        };

        _ = harness.Db.Anniversaries.Add(item);
        _ = await harness.Db.SaveChangesAsync();
        return item.Id;
    }

    private static HeartPointService Points(SqliteHarness harness) =>
        new(harness.Db, new SettingsStub(), TestDoubles.Clock());

    private static AnniversaryRewardService Service(SqliteHarness harness) =>
        new(harness.Db, Points(harness));
}
