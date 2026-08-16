// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Globalization;

namespace OurStory.Core.Time;

/// <summary>
/// 中国农历日期；月份使用人们熟悉的一至十二月，而不是框架插入闰月后的月份序号
/// </summary>
/// <param name="Year">农历年</param>
/// <param name="Month">农历月（一至十二）</param>
/// <param name="Day">农历日</param>
/// <param name="IsLeapMonth">是否为闰月</param>
public readonly record struct ChineseLunarDate(int Year, int Month, int Day, bool IsLeapMonth = false) {
    /// <summary>
    /// 获取适合界面展示的完整农历日期
    /// </summary>
    public string DisplayText => $"农历 {Year} 年 {ChineseLunarCalendar.MonthName(Month, IsLeapMonth)}{ChineseLunarCalendar.DayName(Day)}";

    /// <summary>
    /// 获取不含年份的农历日期
    /// </summary>
    public string ShortText => $"农历{ChineseLunarCalendar.MonthName(Month, IsLeapMonth)}{ChineseLunarCalendar.DayName(Day)}";
}

/// <summary>
/// 公历和中国农历之间的统一转换规则
/// </summary>
public static class ChineseLunarCalendar {
    private static readonly string[] MonthNames = ["正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月"];
    private static readonly string[] DayNames = [
        "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
        "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
        "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"];

    /// <summary>
    /// 获取框架所支持的最小农历年
    /// </summary>
    public static int MinimumYear => new ChineseLunisolarCalendar().GetYear(new ChineseLunisolarCalendar().MinSupportedDateTime);

    /// <summary>
    /// 获取框架所支持的最大农历年
    /// </summary>
    public static int MaximumYear => new ChineseLunisolarCalendar().GetYear(new ChineseLunisolarCalendar().MaxSupportedDateTime);

    /// <summary>
    /// 把一个公历日期转换为中国农历
    /// </summary>
    public static ChineseLunarDate FromSolar(DateOnly date) {
        var calendar = new ChineseLunisolarCalendar();
        var value = date.ToDateTime(TimeOnly.MinValue);
        EnsureSupported(calendar, value);
        var year = calendar.GetYear(value);
        var frameworkMonth = calendar.GetMonth(value);
        var leapMonth = calendar.GetLeapMonth(year);
        var isLeapMonth = leapMonth > 0 && frameworkMonth == leapMonth;
        var month = leapMonth > 0 && frameworkMonth >= leapMonth ? frameworkMonth - 1 : frameworkMonth;
        return new ChineseLunarDate(year, month, calendar.GetDayOfMonth(value), isLeapMonth);
    }

    /// <summary>
    /// 把一个有效的中国农历日期转换为公历日期
    /// </summary>
    public static DateOnly ToSolar(ChineseLunarDate date) {
        var calendar = new ChineseLunisolarCalendar();
        var frameworkMonth = FrameworkMonth(calendar, date.Year, date.Month, date.IsLeapMonth);
        var days = calendar.GetDaysInMonth(date.Year, frameworkMonth);
        if (date.Day < 1 || date.Day > days) {
            throw new ArgumentOutOfRangeException(nameof(date), $"{MonthName(date.Month, date.IsLeapMonth)}没有{date.Day}日。");
        }

        return DateOnly.FromDateTime(calendar.ToDateTime(date.Year, frameworkMonth, date.Day, 0, 0, 0, 0));
    }

    /// <summary>
    /// 尝试把中国农历日期转换为公历日期
    /// </summary>
    public static bool TryToSolar(ChineseLunarDate date, out DateOnly solarDate) {
        try {
            solarDate = ToSolar(date);
            return true;
        } catch (ArgumentOutOfRangeException) {
            solarDate = default;
            return false;
        }
    }

    /// <summary>
    /// 获取同一个农历纪念日在目标农历年的公历日期
    /// </summary>
    public static DateOnly RecurrenceInYear(ChineseLunarDate original, int lunarYear) {
        var calendar = new ChineseLunisolarCalendar();
        var leapMonth = calendar.GetLeapMonth(lunarYear);
        var requestedLeap = original.IsLeapMonth && leapMonth == original.Month + 1;
        var frameworkMonth = FrameworkMonth(calendar, lunarYear, original.Month, requestedLeap);
        var day = Math.Min(original.Day, calendar.GetDaysInMonth(lunarYear, frameworkMonth));
        return DateOnly.FromDateTime(calendar.ToDateTime(lunarYear, frameworkMonth, day, 0, 0, 0, 0));
    }

    /// <summary>
    /// 获取一个农历月的天数
    /// </summary>
    public static int DaysInMonth(int lunarYear, int month, bool isLeapMonth = false) {
        var calendar = new ChineseLunisolarCalendar();
        return calendar.GetDaysInMonth(lunarYear, FrameworkMonth(calendar, lunarYear, month, isLeapMonth));
    }

    /// <summary>
    /// 获取目标农历年闰的是哪个月；没有闰月时返回 null
    /// </summary>
    public static int? LeapMonth(int lunarYear) {
        var leapMonth = new ChineseLunisolarCalendar().GetLeapMonth(lunarYear);
        return leapMonth == 0 ? null : leapMonth - 1;
    }

    /// <summary>
    /// 获取农历月份名称
    /// </summary>
    public static string MonthName(int month, bool isLeapMonth = false) {
        if (month is < 1 or > 12) {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        return (isLeapMonth ? "闰" : string.Empty) + MonthNames[month - 1];
    }

    /// <summary>
    /// 获取农历日期名称
    /// </summary>
    public static string DayName(int day) {
        if (day is < 1 or > 30) {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        return DayNames[day - 1];
    }

    private static int FrameworkMonth(ChineseLunisolarCalendar calendar, int year, int month, bool isLeapMonth) {
        if (year < MinimumYear || year > MaximumYear) {
            throw new ArgumentOutOfRangeException(nameof(year), $"农历年份需要在 {MinimumYear} 至 {MaximumYear} 之间。");
        }

        if (month is < 1 or > 12) {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var leapMonth = calendar.GetLeapMonth(year);
        if (isLeapMonth) {
            if (leapMonth == 0 || leapMonth - 1 != month) {
                throw new ArgumentOutOfRangeException(nameof(isLeapMonth), $"农历 {year} 年没有{MonthName(month, true)}。");
            }

            return leapMonth;
        }

        return leapMonth > 0 && month >= leapMonth ? month + 1 : month;
    }

    private static void EnsureSupported(Calendar calendar, DateTime value) {
        if (value < calendar.MinSupportedDateTime || value > calendar.MaxSupportedDateTime) {
            throw new ArgumentOutOfRangeException(nameof(value), $"日期需要在 {calendar.MinSupportedDateTime:yyyy-MM-dd} 至 {calendar.MaxSupportedDateTime:yyyy-MM-dd} 之间。");
        }
    }
}
