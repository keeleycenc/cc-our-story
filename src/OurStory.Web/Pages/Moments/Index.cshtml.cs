// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Moments;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Moments;

/// <summary>
/// 表示 IndexModel
/// </summary>
public class IndexModel(
    ISettingsService settingsService,
    IMomentService moments,
    MomentUnlockStore unlockStore,
    SiteClock clock) : PageModel {
    /// <summary>
    /// 获取或设置 PageNumber
    /// </summary>
    /// <remarks>页码由 <see cref="PageNumbers.PageNumber"/> 从查询串里取，不走模型绑定。</remarks>
    public int PageNumber { get; private set; } = 1;

    /// <summary>
    /// 执行 Site 操作
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 执行 Items 操作
    /// </summary>
    public PagedList<MomentCard> Items { get; private set; } = PagedList<MomentCard>.Empty(10);

    /// <summary>标题下面那两个小标签用的数字。</summary>
    public int TotalCount => Items.TotalCount;

    /// <summary>
    /// 获取或设置 LoveDays
    /// </summary>
    public int LoveDays { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        Site = await settingsService.GetAsync(cancellationToken);
        PageNumber = Request.PageNumber();

        var viewer = new MomentViewer(User.IsOwner(), unlockStore.UnlockedIds());
        Items = await moments.GetPageAsync(PageNumber, viewer, cancellationToken);

        LoveDays = LoveTimeline.DayNumber(clock.LocalNow, Site.LoveStartedAt);
    }
}
