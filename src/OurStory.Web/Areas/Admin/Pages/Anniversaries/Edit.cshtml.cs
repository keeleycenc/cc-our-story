// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Time;
using OurStory.Services.Anniversaries;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Anniversaries;

/// <summary>
/// 新建或编辑纪念日
/// </summary>
public class EditModel(IAnniversaryService anniversaries) : PageModel {
    /// <summary>
    /// 获取或设置表单输入
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = InputModel.Today();

    /// <summary>
    /// 获取当前纪念日编号
    /// </summary>
    public int? AnniversaryId { get; private set; }

    /// <summary>
    /// 获取是否正在创建
    /// </summary>
    public bool IsNew => AnniversaryId is null;

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken) {
        AnniversaryId = id;
        if (id is null) {
            return Page();
        }

        var item = await anniversaries.GetByIdAsync(id.Value, cancellationToken);
        if (item is null) {
            return NotFound();
        }

        Input = new InputModel {
            Title = item.Title,
            AnniversaryDate = item.AnniversaryDate,
            CalendarType = item.CalendarType,
            LunarYear = ChineseLunarCalendar.FromSolar(item.AnniversaryDate).Year,
            LunarMonthKey = LunarMonthKey(ChineseLunarCalendar.FromSolar(item.AnniversaryDate)),
            LunarDay = ChineseLunarCalendar.FromSolar(item.AnniversaryDate).Day,
            Note = item.Note,
            CoverUrl = item.CoverUrl,
            Kind = item.Kind,
            RepeatYearly = item.RepeatYearly,
            IsPrivate = item.IsPrivate
        };
        return Page();
    }

    /// <summary>
    /// 保存表单
    /// </summary>
    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken) {
        AnniversaryId = id;
        var anniversaryDate = Input.AnniversaryDate;
        if (Input.CalendarType == AnniversaryCalendarType.Lunar) {
            if (!TryReadLunarDate(Input, out anniversaryDate, out var error)) {
                ModelState.AddModelError("Input.LunarDay", error);
            }
        }

        if (!ModelState.IsValid) {
            return Page();
        }

        var model = new AnniversaryEditModel {
            Title = Input.Title,
            AnniversaryDate = anniversaryDate,
            CalendarType = Input.CalendarType,
            Note = Input.Note,
            CoverUrl = Input.CoverUrl,
            Kind = Input.Kind,
            RepeatYearly = Input.RepeatYearly,
            IsPrivate = Input.IsPrivate
        };

        if (id is null) {
            _ = await anniversaries.CreateAsync(model, User.UserId(), cancellationToken);
            TempData["Flash"] = "纪念日已创建。";
            return Redirect("/admin/anniversaries");
        }

        if (!await anniversaries.UpdateAsync(id.Value, model, cancellationToken)) {
            return NotFound();
        }

        TempData["Flash"] = "纪念日已更新。";
        return Redirect("/admin/anniversaries");
    }

    /// <summary>
    /// 返回指定农历年的月份和天数，供表单在切换年份时更新下拉框
    /// </summary>
    public JsonResult OnGetLunarYear(int year) {
        if (year < ChineseLunarCalendar.MinimumYear || year > ChineseLunarCalendar.MaximumYear) {
            return new JsonResult(new { months = Array.Empty<LunarMonthOption>() });
        }

        return new JsonResult(new { months = LunarMonths(year) });
    }

    /// <summary>
    /// 获取一个农历年的可选月份
    /// </summary>
    public static IReadOnlyList<LunarMonthOption> LunarMonths(int year) {
        var leapMonth = ChineseLunarCalendar.LeapMonth(year);
        var result = new List<LunarMonthOption>(13);
        for (var month = 1; month <= 12; month++) {
            result.Add(new LunarMonthOption(
                month.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ChineseLunarCalendar.MonthName(month),
                ChineseLunarCalendar.DaysInMonth(year, month)));
            if (leapMonth == month) {
                result.Add(new LunarMonthOption(
                    $"{month}-leap",
                    ChineseLunarCalendar.MonthName(month, true),
                    ChineseLunarCalendar.DaysInMonth(year, month, true)));
            }
        }

        return result;
    }

    private static bool TryReadLunarDate(InputModel input, out DateOnly date, out string error) {
        var parts = input.LunarMonthKey.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out var month)) {
            date = default;
            error = "请选择有效的农历月份";
            return false;
        }

        var lunar = new ChineseLunarDate(input.LunarYear, month, input.LunarDay, parts.Length == 2 && parts[1] == "leap");
        if (!ChineseLunarCalendar.TryToSolar(lunar, out date)) {
            error = "这个农历日期不存在，请重新选择";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string LunarMonthKey(ChineseLunarDate date) => $"{date.Month}{(date.IsLeapMonth ? "-leap" : string.Empty)}";

    /// <summary>
    /// 农历月份下拉框选项
    /// </summary>
    public sealed record LunarMonthOption(string Value, string Label, int Days);

    /// <summary>
    /// 纪念日表单模型
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength(80)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置日期
        /// </summary>
        [DataType(DataType.Date)]
        public DateOnly AnniversaryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>
        /// 获取或设置纪念日遵循的历法
        /// </summary>
        public AnniversaryCalendarType CalendarType { get; set; } = AnniversaryCalendarType.Solar;

        /// <summary>
        /// 获取或设置农历年
        /// </summary>
        public int LunarYear { get; set; }

        /// <summary>
        /// 获取或设置农历月份键；闰月使用“月份-leap”
        /// </summary>
        public string LunarMonthKey { get; set; } = "1";

        /// <summary>
        /// 获取或设置农历日
        /// </summary>
        public int LunarDay { get; set; } = 1;

        /// <summary>
        /// 获取或设置简短故事
        /// </summary>
        [StringLength(8000)]
        public string? Note { get; set; }

        /// <summary>
        /// 获取或设置封面图地址
        /// </summary>
        [StringLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>
        /// 获取或设置分类
        /// </summary>
        public AnniversaryKind Kind { get; set; } = AnniversaryKind.Love;

        /// <summary>
        /// 获取或设置是否每年重复
        /// </summary>
        public bool RepeatYearly { get; set; } = true;

        /// <summary>
        /// 获取或设置是否仅情侣双方可见
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        /// 建立以今天为默认日期的输入模型
        /// </summary>
        public static InputModel Today() {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var lunar = ChineseLunarCalendar.FromSolar(today);
            return new InputModel {
                AnniversaryDate = today,
                LunarYear = lunar.Year,
                LunarMonthKey = LunarMonthKey(lunar),
                LunarDay = lunar.Day
            };
        }
    }
}
