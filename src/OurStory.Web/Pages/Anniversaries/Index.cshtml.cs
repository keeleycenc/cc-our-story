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

/// <summary>纪念日前台页面。</summary>
public class IndexModel(IAnniversaryService anniversaries, SiteClock clock) : PageModel {
    /// <summary>获取按下一次发生日期排序的纪念日。</summary>
    public IReadOnlyList<AnniversaryOccurrence> Items { get; private set; } = [];

    /// <summary>获取站点时区下的今天。</summary>
    public DateOnly Today { get; private set; }

    /// <summary>处理 GET 请求。</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        Today = clock.Today;
        Items = await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken);
    }

    /// <summary>按批次返回时间轴回忆，用于滚动加载。</summary>
    public async Task<JsonResult> OnGetTimelineAsync(int skip = 0, int take = 10, CancellationToken cancellationToken = default) {
        var today = clock.Today;
        var all = (await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken))
            .OrderByDescending(item => item.OriginalDate)
            .ThenByDescending(item => item.Id)
            .ToArray();
        return Page(all, today, skip, take);
    }

    /// <summary>按剩余天数返回年度提醒，用于滚动加载。</summary>
    public async Task<JsonResult> OnGetUpcomingAsync(int skip = 0, int take = 10, CancellationToken cancellationToken = default) {
        var today = clock.Today;
        var all = (await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken))
            .Where(item => item.RepeatYearly && !item.IsArchived)
            .OrderBy(item => item.DaysUntil)
            .ThenBy(item => item.Title)
            .ToArray();
        return Page(all, today, skip, take);
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

    /// <summary>获取分类的中文名称。</summary>
    public static string KindName(AnniversaryKind kind) => AnniversaryKinds.Name(kind);

    /// <summary>获取分类对应图标。</summary>
    public static string KindIcon(AnniversaryKind kind) => AnniversaryKinds.Icon(kind);
}
