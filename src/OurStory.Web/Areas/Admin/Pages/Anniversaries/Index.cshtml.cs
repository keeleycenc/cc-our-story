// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Anniversaries;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Areas.Admin.Pages.Anniversaries;

/// <summary>
/// 纪念日后台列表
/// </summary>
public class IndexModel(IAnniversaryService anniversaries) : PageModel {
    private const int PageSize = 10;

    /// <summary>
    /// 获取当前这一页的纪念日
    /// </summary>
    public PagedList<AnniversaryOccurrence> Items { get; private set; } = PagedList<AnniversaryOccurrence>.Empty(PageSize);

    /// <summary>
    /// 获取或设置删除后要回到的页码
    /// </summary>
    /// <remarks>
    /// GET 的页码由 <see cref="PageNumbers.PageNumber"/> 从查询串里取，这里只接表单里的隐藏字段
    /// </remarks>
    [BindProperty(Name = "page")]
    public int PageNumber { get; set; } = 1;

    /// <summary>处理 GET 请求</summary>
    /// <remarks>
    /// 排序要先算出每条的下一次发生日期，没法交给数据库，所以照旧一次性取回来再切页。
    /// 纪念日本来就是几十条的量级，这点内存换掉一屏拉不到底的长列表是划算的。
    /// </remarks>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        var all = await anniversaries.GetAllAsync(cancellationToken);
        var lastPage = Math.Max(1, (all.Count + PageSize - 1) / PageSize);

        PageNumber = Math.Min(Request.PageNumber(), lastPage);
        Items = new PagedList<AnniversaryOccurrence>(
            [.. all.Skip((PageNumber - 1) * PageSize).Take(PageSize)],
            PageNumber,
            PageSize,
            all.Count);
    }

    /// <summary>
    /// 删除指定纪念日
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken) {
        _ = await anniversaries.DeleteAsync(id, cancellationToken);
        TempData["Flash"] = "这个纪念日已经删掉了。";
        return Redirect($"/admin/anniversaries?page={PageNumber}");
    }

    /// <summary>
    /// 获取分类名称
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static string KindName(AnniversaryKind kind) => AnniversaryKinds.Name(kind);
}
