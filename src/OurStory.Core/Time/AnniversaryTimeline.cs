// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;
using OurStory.Core.Models;

namespace OurStory.Core.Time;

/// <summary>
/// 纪念日日期规则；页面、后台和测试共用同一个口径
/// </summary>
public static class AnniversaryTimeline {
    /// <summary>
    /// 计算指定纪念日相对于今天的下一次发生信息
    /// </summary>
    /// <remarks>
    /// 对于每年重复的纪念日，按月份和日期计算下一次发生日期；
    /// 如果纪念日为 2 月 29 日，则在非闰年按 2 月最后一天处理。
    /// 对于不重复的纪念日，如果日期已经过去，则不再返回下一次发生日期
    /// </remarks>
    /// <param name="anniversary">要计算的纪念日</param>
    /// <param name="today">作为计算基准的当前日期</param>
    /// <param name="authorName">纪念日展示使用的作者名称，默认为“我们”</param>
    /// <returns>包含下一次发生日期、剩余天数和周年数等信息的纪念日发生结果</returns>
    public static AnniversaryOccurrence Calculate(Anniversary anniversary, DateOnly today, string authorName = "我们") {
        ArgumentNullException.ThrowIfNull(anniversary);

        DateOnly? nextDate;
        if (!anniversary.RepeatYearly) {
            nextDate = anniversary.AnniversaryDate >= today ? anniversary.AnniversaryDate : null;
        } else if (anniversary.AnniversaryDate >= today) {
            // 首次发生日还没到时，不能为了“每年重复”把它回算到更早的年份。
            nextDate = anniversary.AnniversaryDate;
        } else if (anniversary.CalendarType == AnniversaryCalendarType.Lunar) {
            nextDate = NextLunarDate(anniversary.AnniversaryDate, today);
        } else {
            var candidate = InYear(anniversary.AnniversaryDate, today.Year);
            nextDate = candidate >= today ? candidate : InYear(anniversary.AnniversaryDate, today.Year + 1);
        }

        var years = YearsBetween(anniversary, today, nextDate);

        return new AnniversaryOccurrence(
            anniversary.Id,
            anniversary.Title,
            anniversary.Note?.Trim() ?? string.Empty,
            anniversary.NoteHtml,
            anniversary.CoverUrl ?? string.Empty,
            anniversary.Kind,
            anniversary.CalendarType,
            anniversary.AnniversaryDate,
            nextDate,
            nextDate is null ? null : nextDate.Value.DayNumber - today.DayNumber,
            years,
            anniversary.RepeatYearly,
            anniversary.IsPrivate,
            authorName);
    }

    /// <summary>
    /// 判断一条纪念日是否会在指定公历日期发生
    /// </summary>
    public static bool OccursOn(AnniversaryOccurrence item, DateOnly date) {
        ArgumentNullException.ThrowIfNull(item);
        if (date < item.OriginalDate) {
            return false;
        }

        if (!item.RepeatYearly) {
            return date == item.OriginalDate;
        }

        if (item.CalendarType == AnniversaryCalendarType.Solar) {
            return InYear(item.OriginalDate, date.Year) == date;
        }

        var originalLunar = ChineseLunarCalendar.FromSolar(item.OriginalDate);
        var dateLunar = ChineseLunarCalendar.FromSolar(date);
        return ChineseLunarCalendar.RecurrenceInYear(originalLunar, dateLunar.Year) == date;
    }

    private static DateOnly NextLunarDate(DateOnly originalDate, DateOnly today) {
        var originalLunar = ChineseLunarCalendar.FromSolar(originalDate);
        var todayLunarYear = ChineseLunarCalendar.FromSolar(today).Year;
        for (var year = todayLunarYear; year <= Math.Min(todayLunarYear + 2, ChineseLunarCalendar.MaximumYear); year++) {
            var candidate = ChineseLunarCalendar.RecurrenceInYear(originalLunar, year);
            if (candidate >= today) {
                return candidate;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(today), "已超出可计算的农历日期范围。");
    }

    private static int YearsBetween(Anniversary anniversary, DateOnly today, DateOnly? nextDate) {
        if (anniversary.CalendarType == AnniversaryCalendarType.Lunar) {
            var originalYear = ChineseLunarCalendar.FromSolar(anniversary.AnniversaryDate).Year;
            var comparison = nextDate ?? today;
            return Math.Max(0, ChineseLunarCalendar.FromSolar(comparison).Year - originalYear);
        }

        return nextDate is null
            ? Math.Max(0, today.Year - anniversary.AnniversaryDate.Year)
            : Math.Max(0, nextDate.Value.Year - anniversary.AnniversaryDate.Year);
    }

    private static DateOnly InYear(DateOnly date, int year) {
        var day = Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month));
        return new DateOnly(year, date.Month, day);
    }
}
