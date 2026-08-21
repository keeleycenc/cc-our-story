// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Areas.Admin.Pages.Affinity;

/// <summary>
/// 待启封题目与共同作答记录列表
/// </summary>
public class IndexModel(IAffinityService affinity) : PageModel {
    private const int PageSize = PageNumbers.AdminPageSize;

    /// <summary>
    /// 获取封存题目列表
    /// </summary>
    public PagedList<AffinityQuestionCard> Questions { get; private set; } = PagedList<AffinityQuestionCard>.Empty(PageSize);

    /// <summary>
    /// 获取双方均已完成的只读作答记录
    /// </summary>
    public PagedList<AffinityAnsweredQuestionCard> AnsweredQuestions { get; private set; } =
        PagedList<AffinityAnsweredQuestionCard>.Empty(PageSize);

    /// <summary>
    /// 异步加载封存题目列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        Questions = await affinity.GetSealedQuestionsAsync(Request.PageNumber(), PageSize, cancellationToken);
        AnsweredQuestions = await affinity.GetAnsweredQuestionsAsync(
            Request.PageNumber("answeredPage"),
            PageSize,
            cancellationToken);
    }

    /// <summary>
    /// 获取共同作答记录在前台答题足迹中的定位地址
    /// </summary>
    /// <param name="index">当前后台分页中的零基索引</param>
    /// <param name="dailyQuestionId">每日题目编号</param>
    /// <returns>带页码和锚点的前台地址</returns>
    public string HistoryUrl(int index, int dailyQuestionId) {
        var absoluteIndex = ((AnsweredQuestions.Page - 1) * AnsweredQuestions.PageSize) + index;
        var historyPage = (absoluteIndex / PageNumbers.AffinityHistoryPageSize) + 1;
        var pageQuery = historyPage > 1 ? $"?page={historyPage}" : string.Empty;
        return $"/affinity{pageQuery}#affinity-history-{dailyQuestionId}";
    }
}
