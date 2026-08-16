// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Time;
using Xunit;

namespace OurStory.Tests;

/// <summary>纪念日日期计算测试。</summary>
public class AnniversaryTimelineTests {
    /// <summary>用已知春节日期锁住公历与农历转换口径。</summary>
    [Fact]
    public void 公历与农历转换符合已知日期() {
        var springFestival = ChineseLunarCalendar.ToSolar(new ChineseLunarDate(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 2, 17), springFestival);
        Assert.Equal(new ChineseLunarDate(2026, 1, 1), ChineseLunarCalendar.FromSolar(springFestival));
    }

    /// <summary>今年未到的年度纪念日仍落在今年。</summary>
    [Fact]
    public void 年度纪念日优先取今年() {
        var item = New(new DateOnly(2024, 10, 1));

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2026, 8, 14));

        Assert.Equal(new DateOnly(2026, 10, 1), result.NextDate);
        Assert.Equal(48, result.DaysUntil);
        Assert.Equal(2, result.Years);
    }

    /// <summary>今年已经过去时滚动到明年。</summary>
    [Fact]
    public void 年度纪念日过去后取明年() {
        var item = New(new DateOnly(2024, 5, 20));

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2026, 8, 14));

        Assert.Equal(new DateOnly(2027, 5, 20), result.NextDate);
        Assert.Equal(3, result.Years);
    }

    /// <summary>日期命中今天时 D-day 为零。</summary>
    [Fact]
    public void 今天就是纪念日() {
        var item = New(new DateOnly(2020, 8, 14));

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2026, 8, 14));

        Assert.True(result.IsToday);
        Assert.Equal(6, result.Years);
    }

    /// <summary>闰日纪念日在普通年份落到二月最后一天。</summary>
    [Fact]
    public void 闰日纪念日在平年使用二月最后一天() {
        var item = New(new DateOnly(2024, 2, 29));

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2025, 1, 1));

        Assert.Equal(new DateOnly(2025, 2, 28), result.NextDate);
    }

    /// <summary>未来才开始的年度纪念日不会回算到今年。</summary>
    [Fact]
    public void 未来首次发生日保持原日期() {
        var item = New(new DateOnly(2028, 10, 1));

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2026, 8, 14));

        Assert.Equal(new DateOnly(2028, 10, 1), result.NextDate);
        Assert.Equal(0, result.Years);
    }

    /// <summary>已过去的一次性日期进入归档而不循环。</summary>
    [Fact]
    public void 一次性日期过去后归档() {
        var item = New(new DateOnly(2025, 3, 8));
        item.RepeatYearly = false;

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2026, 8, 14));

        Assert.True(result.IsArchived);
        Assert.Null(result.NextDate);
        Assert.Null(result.DaysUntil);
    }

    /// <summary>农历纪念日会跟随农历日期，而不是固定在原公历月日。</summary>
    [Fact]
    public void 农历年度纪念日按农历滚动() {
        var original = ChineseLunarCalendar.ToSolar(new ChineseLunarDate(2024, 1, 1));
        var item = New(original);
        item.CalendarType = AnniversaryCalendarType.Lunar;

        var result = AnniversaryTimeline.Calculate(item, new DateOnly(2026, 2, 16));

        Assert.Equal(ChineseLunarCalendar.ToSolar(new ChineseLunarDate(2026, 1, 1)), result.NextDate);
        Assert.Equal(2, result.Years);
        Assert.True(AnniversaryTimeline.OccursOn(result, result.NextDate!.Value));
    }

    /// <summary>闰月在普通年份不存在时，会回落到同名普通月。</summary>
    [Fact]
    public void 农历闰月在非闰年回落到普通月() {
        var leapMonth = ChineseLunarCalendar.LeapMonth(2025);
        _ = Assert.NotNull(leapMonth);
        var originalLunar = new ChineseLunarDate(2025, leapMonth.Value, 1, true);

        var result = ChineseLunarCalendar.RecurrenceInYear(originalLunar, 2026);

        var converted = ChineseLunarCalendar.FromSolar(result);
        Assert.Equal(originalLunar.Month, converted.Month);
        Assert.False(converted.IsLeapMonth);
    }

    private static Anniversary New(DateOnly date) => new() {
        Id = 7,
        Title = "我们的日子",
        AnniversaryDate = date,
        RepeatYearly = true,
        IsPrivate = false
    };
}
