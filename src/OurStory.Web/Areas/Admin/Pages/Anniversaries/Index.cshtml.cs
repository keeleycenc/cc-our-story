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

/// <summary>纪念日后台列表。</summary>
public class IndexModel(IAnniversaryService anniversaries) : PageModel {
    /// <summary>获取全部纪念日。</summary>
    public IReadOnlyList<AnniversaryOccurrence> Items { get; private set; } = [];

    /// <summary>处理 GET 请求。</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Items = await anniversaries.GetAllAsync(cancellationToken);

    /// <summary>删除指定纪念日。</summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken) {
        _ = await anniversaries.DeleteAsync(id, cancellationToken);
        TempData["Flash"] = "这个纪念日已经删掉了。";
        return Redirect("/admin/anniversaries");
    }

    /// <summary>获取分类名称。</summary>
    public static string KindName(AnniversaryKind kind) => AnniversaryKinds.Name(kind);
}
