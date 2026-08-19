// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Anniversaries;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Anniversaries;

/// <summary>
/// 纪念日前台页面
/// </summary>
public class IndexModel(IAnniversaryService anniversaries, SiteClock clock) : PageModel {
    /// <summary>
    /// 获取按下一次发生日期排序的纪念日
    /// </summary>
    public IReadOnlyList<AnniversaryOccurrence> Items { get; private set; } = [];

    /// <summary>
    /// 获取站点时区下的今天
    /// </summary>
    public DateOnly Today { get; private set; }

    /// <summary>
    /// 获取私密纪念日的数量
    /// </summary>
    public int PrivateCount { get; private set; }

    private static readonly string[] stringArray = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        Today = clock.Today;
        Items = await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken);
        PrivateCount = await anniversaries.CountPrivateAsync(cancellationToken);
    }

    /// <summary>
    /// 按批次返回时间轴回忆，用于滚动加载
    /// </summary>
    public async Task<JsonResult> OnGetTimelineAsync(int skip = 0, int take = 10, CancellationToken cancellationToken = default) {
        var today = clock.Today;
        var all = (await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken))
            .OrderByDescending(item => item.OriginalDate)
            .ThenByDescending(item => item.Id)
            .ToArray();
        return Page(all, today, skip, take);
    }

    /// <summary>
    /// 按剩余天数返回年度提醒，用于滚动加载
    /// </summary>
    public async Task<JsonResult> OnGetUpcomingAsync(int skip = 0, int take = 10, CancellationToken cancellationToken = default) {
        var today = clock.Today;
        var all = (await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken))
            .Where(item => item.RepeatYearly && !item.IsArchived)
            .OrderBy(item => item.DaysUntil)
            .ThenBy(item => item.Title)
            .ToArray();
        return Page(all, today, skip, take);
    }

    /// <summary>
    /// 返回一个真实的公历或农历月份，及其中发生的纪念日
    /// </summary>
    public async Task<JsonResult> OnGetCalendarAsync(
        string calendar = "solar",
        int? year = null,
        int? month = null,
        bool leap = false,
        CancellationToken cancellationToken = default) {
        var items = await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken);
        var today = clock.Today;
        var lunarMode = string.Equals(calendar, "lunar", StringComparison.OrdinalIgnoreCase);
        var todayLunar = ChineseLunarCalendar.FromSolar(today);

        var viewYear = year ?? (lunarMode ? todayLunar.Year : today.Year);
        var viewMonth = month ?? (lunarMode ? todayLunar.Month : today.Month);
        DateOnly firstDate;
        int days;
        if (lunarMode) {
            viewYear = Math.Clamp(viewYear, ChineseLunarCalendar.MinimumYear, ChineseLunarCalendar.MaximumYear);
            viewMonth = Math.Clamp(viewMonth, 1, 12);
            if (ChineseLunarCalendar.LeapMonth(viewYear) != viewMonth) {
                leap = false;
            }

            firstDate = ChineseLunarCalendar.ToSolar(new ChineseLunarDate(viewYear, viewMonth, 1, leap));
            days = ChineseLunarCalendar.DaysInMonth(viewYear, viewMonth, leap);
        } else {
            viewYear = Math.Clamp(viewYear, ChineseLunarCalendar.MinimumYear, ChineseLunarCalendar.MaximumYear);
            viewMonth = Math.Clamp(viewMonth, 1, 12);
            leap = false;
            firstDate = new DateOnly(viewYear, viewMonth, 1);
            days = DateTime.DaysInMonth(viewYear, viewMonth);
        }

        var leading = ((int)firstDate.DayOfWeek + 6) % 7;
        var rows = Math.Max(5, (int)Math.Ceiling((leading + days) / 7d));
        var cells = Enumerable.Range(0, rows * 7).Select(index => {
            var solarDate = firstDate.AddDays(index - leading);
            var lunarDate = ChineseLunarCalendar.FromSolar(solarDate);
            var outside = lunarMode
                ? lunarDate.Year != viewYear || lunarDate.Month != viewMonth || lunarDate.IsLeapMonth != leap
                : solarDate.Year != viewYear || solarDate.Month != viewMonth;
            var records = items.Where(item => AnniversaryTimeline.OccursOn(item, solarDate))
                .OrderBy(item => item.Title)
                .Select(CalendarRecord)
                .ToArray();
            return new {
                solarDate = solarDate.ToString("yyyy-MM-dd"),
                solarYear = solarDate.Year,
                solarMonth = solarDate.Month,
                lunarYear = lunarDate.Year,
                lunarMonth = lunarDate.Month,
                lunarLeap = lunarDate.IsLeapMonth,
                primary = lunarMode ? ChineseLunarCalendar.DayName(lunarDate.Day) : solarDate.Day.ToString(System.Globalization.CultureInfo.InvariantCulture),
                secondary = lunarMode
                    ? $"{solarDate.Month}/{solarDate.Day}"
                    : lunarDate.Day == 1 ? ChineseLunarCalendar.MonthName(lunarDate.Month, lunarDate.IsLeapMonth) : ChineseLunarCalendar.DayName(lunarDate.Day),
                solarLabel = solarDate.ToString("yyyy 年 M 月 d 日"),
                lunarLabel = lunarDate.DisplayText,
                weekday = stringArray[(int)solarDate.DayOfWeek],
                weekend = solarDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                outside,
                isToday = solarDate == today,
                records
            };
        }).ToArray();

        var months = lunarMode ? LunarMonthOptions(viewYear) : SolarMonthOptions();
        var (Previous, Next) = lunarMode
            ? LunarNavigation(viewYear, viewMonth, leap)
            : SolarNavigation(viewYear, viewMonth);
        var originalYears = items.Select(item => item.OriginalDate.Year).DefaultIfEmpty(today.Year);
        var minimumYear = lunarMode
            ? Math.Max(ChineseLunarCalendar.MinimumYear, items.Select(item => ChineseLunarCalendar.FromSolar(item.OriginalDate).Year).DefaultIfEmpty(todayLunar.Year).Min())
            : Math.Max(ChineseLunarCalendar.MinimumYear, originalYears.Min());
        var maximumYear = lunarMode
            ? Math.Min(ChineseLunarCalendar.MaximumYear, Math.Max(todayLunar.Year + 10, viewYear))
            : Math.Min(ChineseLunarCalendar.MaximumYear, Math.Max(today.Year + 10, viewYear));

        return new JsonResult(new {
            calendar = lunarMode ? "lunar" : "solar",
            year = viewYear,
            month = viewMonth,
            leap,
            monthKey = MonthKey(viewMonth, leap),
            minimumYear,
            maximumYear,
            rows,
            months,
            previous = Previous,
            next = Next,
            cells
        });
    }

    private static JsonResult Page(AnniversaryOccurrence[] source, DateOnly today, int skip, int take) {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 10);
        var items = source.Skip(skip).Take(take).Select(item => new {
            id = item.Id,
            url = item.Url,
            title = item.Title,
            summary = item.Summary,
            authorName = item.AuthorName,
            coverUrl = item.CoverUrl,
            kind = item.Kind.ToString().ToLowerInvariant(),
            kindName = KindName(item.Kind),
            calendarType = item.CalendarType.ToString().ToLowerInvariant(),
            calendarName = item.CalendarName,
            lunarDate = item.CalendarType == AnniversaryCalendarType.Lunar ? item.LunarDate.ShortText : string.Empty,
            originalDate = item.OriginalDate.ToString("yyyy-MM-dd"),
            nextDate = item.NextDate?.ToString("yyyy-MM-dd"),
            daysUntil = item.DaysUntil,
            dayNumber = Math.Max(1, today.DayNumber - item.OriginalDate.DayNumber + 1),
            repeatYearly = item.RepeatYearly,
            isPrivate = item.IsPrivate,
            isArchived = item.IsArchived
        }).ToArray();
        return new JsonResult(new {
            items,
            hasMore = skip + items.Length < source.Length
        });
    }

    /// <summary>
    /// 获取分类的中文名称
    /// </summary>
    public static string KindName(AnniversaryKind kind) => AnniversaryKinds.Name(kind);

    /// <summary>
    /// 获取分类对应图标
    /// </summary>
    public static string KindIcon(AnniversaryKind kind) => AnniversaryKinds.Icon(kind);

    private static object CalendarRecord(AnniversaryOccurrence item) => new {
        item.Id,
        item.Title,
        item.Url,
        kind = item.Kind.ToString().ToLowerInvariant(),
        kindName = KindName(item.Kind),
        kindIcon = KindIcon(item.Kind),
        item.RepeatYearly,
        item.IsPrivate,
        item.AuthorName,
        calendarName = item.CalendarName
    };

    private static CalendarMonthOption[] SolarMonthOptions() => [.. Enumerable.Range(1, 12).Select(month =>
        new CalendarMonthOption(MonthKey(month, false), $"{month} 月"))];

    private static CalendarMonthOption[] LunarMonthOptions(int year) {
        var leapMonth = ChineseLunarCalendar.LeapMonth(year);
        var result = new List<CalendarMonthOption>(13);
        for (var month = 1; month <= 12; month++) {
            result.Add(new CalendarMonthOption(MonthKey(month, false), ChineseLunarCalendar.MonthName(month)));
            if (leapMonth == month) {
                result.Add(new CalendarMonthOption(MonthKey(month, true), ChineseLunarCalendar.MonthName(month, true)));
            }
        }

        return [.. result];
    }

    private static (object Previous, object Next) SolarNavigation(int year, int month) {
        var current = new DateOnly(year, month, 1);
        return (CalendarTarget(current.AddMonths(-1).Year, current.AddMonths(-1).Month, false),
            CalendarTarget(current.AddMonths(1).Year, current.AddMonths(1).Month, false));
    }

    private static (object Previous, object Next) LunarNavigation(int year, int month, bool leap) {
        var values = LunarMonthOptions(year)
            .Select(option => option.Value)
            .ToArray();
        var key = MonthKey(month, leap);
        var index = Array.IndexOf(values, key);
        if (index > 0 && index + 1 < values.Length) {
            return (CalendarTarget(year, values[index - 1]), CalendarTarget(year, values[index + 1]));
        }

        if (index == 0) {
            var previousYear = Math.Max(ChineseLunarCalendar.MinimumYear, year - 1);
            var previousValues = LunarMonthOptions(previousYear)
                .Select(option => option.Value)
                .ToArray();
            return (CalendarTarget(previousYear, previousValues[^1]), CalendarTarget(year, values[1]));
        }

        var nextYear = Math.Min(ChineseLunarCalendar.MaximumYear, year + 1);
        var nextValues = LunarMonthOptions(nextYear)
            .Select(option => option.Value)
            .ToArray();
        return (CalendarTarget(year, values[^2]), CalendarTarget(nextYear, nextValues[0]));
    }

    private static object CalendarTarget(int year, int month, bool leap) => new { year, month, leap };

    private static object CalendarTarget(int year, string key) {
        var values = key.Split('-');
        return CalendarTarget(year, int.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture), values.Length == 2);
    }

    private static string MonthKey(int month, bool leap) => $"{month}{(leap ? "-leap" : string.Empty)}";

    private sealed record CalendarMonthOption(string Value, string Label);
}
