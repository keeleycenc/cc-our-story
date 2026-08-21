// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Areas.Admin.Pages.Affinity;

/// <summary>
/// 封存题目元数据列表
/// </summary>
public class IndexModel(IAffinityService affinity) : PageModel {
    private const int PageSize = PageNumbers.AdminPageSize;

    /// <summary>
    /// 获取封存题目列表
    /// </summary>
    public PagedList<AffinityQuestionCard> Questions { get; private set; } = PagedList<AffinityQuestionCard>.Empty(PageSize);

    /// <summary>
    /// 异步加载封存题目列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Questions = await affinity.GetSealedQuestionsAsync(Request.PageNumber(), PageSize, cancellationToken);
}
