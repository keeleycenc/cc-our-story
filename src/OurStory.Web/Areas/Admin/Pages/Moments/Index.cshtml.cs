// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Moments;

namespace OurStory.Web.Areas.Admin.Pages.Moments;

/// <summary>
/// 表示 IndexModel
/// </summary>
public class IndexModel(IMomentService moments, SiteClock clock) : PageModel {
    private const int PageSize = 20;

    /// <summary>
    /// 获取或设置 PageNumber
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// 执行 Items 操作
    /// </summary>
    public PagedList<Moment> Items { get; private set; } = PagedList<Moment>.Empty(PageSize);

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        // 这一页管的是全站的记录，两个人的都列出来
        Items = await moments.ListForAdminAsync(PageNumber, PageSize, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 处理 DeleteAsync(int, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken) {
        _ = await moments.DeleteAsync(id, cancellationToken);

        TempData["Flash"] = "这条记录已经删掉了。";

        return Redirect($"/admin/moments?page={PageNumber}");
    }

    /// <summary>
    /// 转换Local
    /// </summary>
    public DateTime ToLocal(DateTimeOffset instant) => clock.ToLocal(instant);
}
