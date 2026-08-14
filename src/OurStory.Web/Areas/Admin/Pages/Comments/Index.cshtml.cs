// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Comments;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Areas.Admin.Pages.Comments;

/// <summary>
/// 表示 IndexModel
/// </summary>
public class IndexModel(ICommentService comments, SiteClock clock) : PageModel {
    private const int PageSize = PageNumbers.AdminPageSize;

    /// <summary>
    /// 获取或设置 PageNumber
    /// </summary>
    /// <remarks>GET 的页码由 <see cref="PageNumbers.PageNumber"/> 从查询串里取，这里只接表单里的隐藏字段。</remarks>
    [BindProperty(Name = "page")]
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// 执行 Items 操作
    /// </summary>
    public PagedList<Comment> Items { get; private set; } = PagedList<Comment>.Empty(PageSize);

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        PageNumber = Request.PageNumber();
        Items = await comments.ListForAdminAsync(PageNumber, PageSize, cancellationToken);
    }

    /// <summary>
    /// 处理 ToggleAsync(int, bool, CancellationToken) 的 POST 请求
    /// </summary>
    /// <param name="id"></param>
    /// <param name="approved"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnPostToggleAsync(int id, bool approved, CancellationToken cancellationToken) {
        _ = await comments.SetApprovedAsync(id, approved, cancellationToken);
        TempData["Flash"] = approved ? "这条留言已放出（可见）" : "这条留言已被压下（不可见）";
        return BackToList();
    }

    /// <summary>
    /// 处理 DeleteAsync(int, CancellationToken) 的 POST 请求
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken) {
        _ = await comments.DeleteAsync(id, cancellationToken);
        TempData["Flash"] = "留言已经删掉了。";
        return BackToList();
    }

    /// <summary>
    /// 转换Local
    /// </summary>
    public DateTime ToLocal(DateTimeOffset instant) => clock.ToLocal(instant);

    /// <summary>
    /// 操作完回到原来那一页
    /// </summary>
    /// <remarks>
    /// 不能用 RedirectToPage(new { page = ... })：page 是 Razor Pages 的保留路由键，
    /// 会被当成「要跳去哪个页面」，结果是一句 No page named '' 的 500
    /// </remarks>
    private RedirectResult BackToList() => Redirect($"/admin/comments?page={PageNumber}");
}
