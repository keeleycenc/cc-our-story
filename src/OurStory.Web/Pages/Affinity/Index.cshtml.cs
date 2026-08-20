// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Affinity;

/// <summary>
/// 获取心有灵犀页面模型
/// </summary>
[Authorize]
public class IndexModel(IAffinityService affinity, ISettingsService settings) : PageModel {
    private const int PageSize = 8;

    /// <summary>
    /// 获取心有灵犀仪表盘数据
    /// </summary>
    public AffinityDashboard Dashboard { get; private set; } = new(
        null,
        new AffinityStats(0, 0),
        PagedList<AffinityHistoryItem>.Empty(PageSize));

    /// <summary>
    /// 获取站点设置
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 获取提示信息
    /// </summary>
    public string? Flash { get; private set; }

    /// <summary>
    /// 获取错误信息
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 获取当前用户名称
    /// </summary>
    public string MyName => Site.RoleName(User.Role());

    /// <summary>
    /// 获取伴侣用户名称
    /// </summary>
    public string PartnerName => Site.RoleName(User.Role() == UserRole.Boy ? UserRole.Girl : UserRole.Boy);

    /// <summary>
    /// 获取心有灵犀页面数据
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) {
        if (User.UserId() is not { } userId || User.Role() is not (UserRole.Boy or UserRole.Girl)) {
            return Forbid();
        }

        Flash = TempData["AffinityFlash"] as string;
        Error = TempData["AffinityError"] as string;
        await LoadAsync(userId, cancellationToken);
        return Page();
    }

    /// <summary>
    /// 提交今日答案
    /// </summary>
    public async Task<IActionResult> OnPostAnswerAsync(
        int dailyQuestionId,
        int optionIndex,
        CancellationToken cancellationToken) {
        if (User.UserId() is not { } userId) {
            return Forbid();
        }

        var result = await affinity.SubmitAsync(dailyQuestionId, optionIndex, userId, User.Role(), cancellationToken);
        switch (result) {
            case AffinitySubmitResult.Accepted:
                TempData["AffinityFlash"] = "答案已经藏好啦，等两个人都完成后一起揭晓。";
                break;
            case AffinitySubmitResult.AlreadyAnswered:
                TempData["AffinityError"] = "今天已经回答过啦，每个人每天只能提交一次。";
                break;
            case AffinitySubmitResult.InvalidOption:
                TempData["AffinityError"] = "这个选项不存在，请重新选择。";
                break;
            case AffinitySubmitResult.InvalidQuestion:
                TempData["AffinityError"] = "今日题目已经更新，请重新打开页面。";
                break;
            default:
                return Forbid();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(int userId, CancellationToken cancellationToken) {
        Site = await settings.GetAsync(cancellationToken);
        Dashboard = await affinity.GetDashboardAsync(
            userId,
            User.Role(),
            Request.PageNumber(),
            PageSize,
            cancellationToken);
    }
}
