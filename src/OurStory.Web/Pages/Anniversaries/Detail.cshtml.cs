// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Anniversaries;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Anniversaries;

/// <summary>
/// 纪念日完整故事页面
/// </summary>
public class DetailModel(IAnniversaryService anniversaries, ArticleMedia articleMedia, SiteClock clock) : PageModel {
    /// <summary>
    /// 获取当前纪念日
    /// </summary>
    public AnniversaryOccurrence Item { get; private set; } = null!;

    /// <summary>
    /// 获取改写过图片的故事正文，小图先显示、点开才去取原图
    /// </summary>
    public IHtmlContent Story { get; private set; } = HtmlString.Empty;

    /// <summary>
    /// 获取当前排序中的上一篇纪念日
    /// </summary>
    public AnniversaryOccurrence? Previous { get; private set; }

    /// <summary>
    /// 获取当前排序中的下一篇纪念日
    /// </summary>
    public AnniversaryOccurrence? Next { get; private set; }

    /// <summary>
    /// 获取站点时区下的今天
    /// </summary>
    public DateOnly Today => clock.Today;

    /// <summary>
    /// 读取公开内容，或向已登录的情侣双方开放私密内容
    /// </summary>
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken) {
        var sequence = (await anniversaries.GetForViewerAsync(User.IsOwner(), cancellationToken))
            .OrderByDescending(item => item.OriginalDate)
            .ThenByDescending(item => item.Id)
            .ToArray();
        var index = Array.FindIndex(sequence, item => item.Id == id);
        if (index < 0) {
            return NotFound();
        }

        Item = sequence[index];
        Story = await articleMedia.RenderAsync(Item.NoteHtml, $"anniversary-{Item.Id}", cancellationToken);
        Previous = index > 0 ? sequence[index - 1] : null;
        Next = index + 1 < sequence.Length ? sequence[index + 1] : null;
        return Page();
    }
}
