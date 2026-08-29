// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Cycles;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Cycles;

/// <summary>
/// 花信如期页面
/// </summary>
[Authorize]
public sealed class IndexModel(ICycleService cycles, SiteClock clock) : PageModel {
    private const int PageSize = PageNumbers.CycleHistoryPageSize;

    /// <summary>
    /// 获取整页要用的聚合数据
    /// </summary>
    public CycleDashboard Dashboard { get; private set; } = null!;

    /// <summary>
    /// 获取用于「今天开始」操作的请求幂等键
    /// </summary>
    public string NewRequestKey { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 获取服务端认定的今天
    /// </summary>
    public DateOnly Today => clock.Today;

    /// <summary>
    /// 获取操作成功后的提示
    /// </summary>
    public string? Flash { get; private set; }

    /// <summary>
    /// 获取操作失败后的提示
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 获取需要二次确认时的说明
    /// </summary>
    public string? Warning { get; private set; }

    /// <summary>
    /// 获取待确认的动作名，取值为 <c>start</c> 或 <c>create</c>
    /// </summary>
    public string? PendingAction { get; private set; }

    /// <summary>
    /// 获取待确认的完整记录提交内容
    /// </summary>
    public CycleRecordSubmission? PendingRecord { get; private set; }

    /// <summary>
    /// 获取待确认动作携带的幂等键
    /// </summary>
    public string? PendingRequestKey { get; private set; }

    /// <summary>
    /// 获取历史时间轴的翻页配置
    /// </summary>
    public PaginationModel HistoryPagination => new(
        Dashboard.History.Page,
        Dashboard.History.TotalPages,
        $"/cycles?year={Dashboard.Calendar.Year}&month={Dashboard.Calendar.Month}",
        Fragment: "history");

    /// <summary>
    /// 处理页面加载请求
    /// </summary>
    /// <param name="year">月历年份</param>
    /// <param name="month">月历月份</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnGetAsync(int? year, int? month, CancellationToken cancellationToken) {
        if (!IsCoupleAccount(out var userId)) {
            return Forbid();
        }

        Flash = TempData["CycleFlash"] as string;
        Error = TempData["CycleError"] as string;
        return await LoadAsync(userId, year, month, cancellationToken);
    }

    /// <summary>
    /// 返回指定月份的日历数据，用于无刷新切换月份
    /// </summary>
    /// <param name="year">月历年份</param>
    /// <param name="month">月历月份</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>月历数据</returns>
    public async Task<IActionResult> OnGetCalendarAsync(int? year, int? month, CancellationToken cancellationToken) {
        if (!IsCoupleAccount(out var userId)) {
            return Forbid();
        }

        try {
            var calendar = await cycles.GetCalendarAsync(
                userId,
                year ?? clock.Today.Year,
                month ?? clock.Today.Month,
                cancellationToken);
            return new JsonResult(CycleCalendarPayload.From(calendar));
        } catch (UnauthorizedAccessException) {
            return Forbid();
        }
    }

    /// <summary>
    /// 登记当前日期为经期开始日期
    /// </summary>
    /// <param name="requestKey">幂等键</param>
    /// <param name="confirmSuspicious">是否已确认风险提示</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnPostStartAsync(
        string requestKey,
        bool confirmSuspicious,
        CancellationToken cancellationToken) {
        if (!IsCoupleAccount(out var userId)) {
            return Forbid();
        }

        var result = await cycles.StartAsync(userId, requestKey, confirmSuspicious, cancellationToken);
        if (result.Status != CycleWriteStatus.RequiresConfirmation) {
            return RedirectWith(result);
        }

        Warning = result.Message;
        PendingAction = "start";
        PendingRequestKey = requestKey;
        return await LoadAsync(userId, null, null, cancellationToken);
    }

    /// <summary>
    /// 结束当前进行中的记录
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnPostEndAsync(CancellationToken cancellationToken) =>
        IsCoupleAccount(out var userId)
            ? RedirectWith(await cycles.EndAsync(userId, cancellationToken))
            : Forbid();

    /// <summary>
    /// 补记一条完整或仍在进行的周期记录
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期；留空表示仍在进行</param>
    /// <param name="note">备注</param>
    /// <param name="requestKey">幂等键</param>
    /// <param name="confirmSuspicious">是否已确认风险提示</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnPostCreateAsync(
        DateOnly startDate,
        DateOnly? endDate,
        string? note,
        string requestKey,
        bool confirmSuspicious,
        CancellationToken cancellationToken) {
        if (!IsCoupleAccount(out var userId)) {
            return Forbid();
        }

        var submission = new CycleRecordSubmission(startDate, endDate, note ?? string.Empty, requestKey, confirmSuspicious);
        var result = await cycles.CreateAsync(userId, submission, cancellationToken);
        if (result.Status != CycleWriteStatus.RequiresConfirmation) {
            return RedirectWith(result);
        }

        Warning = result.Message;
        PendingAction = "create";
        PendingRecord = submission;
        return await LoadAsync(userId, startDate.Year, startDate.Month, cancellationToken);
    }

    /// <summary>
    /// 更新一条已有记录
    /// </summary>
    /// <param name="recordId">记录标识</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期；留空表示这条仍在进行</param>
    /// <param name="note">备注</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnPostUpdateAsync(
        int recordId,
        DateOnly startDate,
        DateOnly? endDate,
        string? note,
        CancellationToken cancellationToken) =>
        IsCoupleAccount(out var userId)
            ? RedirectWith(await cycles.UpdateAsync(userId, recordId, startDate, endDate, note ?? string.Empty, cancellationToken))
            : Forbid();

    /// <summary>
    /// 删除一条记录
    /// </summary>
    /// <param name="recordId">记录标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnPostDeleteAsync(int recordId, CancellationToken cancellationToken) =>
        IsCoupleAccount(out var userId)
            ? RedirectWith(await cycles.DeleteAsync(userId, recordId, cancellationToken))
            : Forbid();

    /// <summary>
    /// 补充某一天的身体状态
    /// </summary>
    /// <param name="date">记录日期</param>
    /// <param name="flow">经量</param>
    /// <param name="mood">心情</param>
    /// <param name="pain">不适程度</param>
    /// <param name="symptoms">勾选的不适</param>
    /// <param name="note">补充说明</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应</returns>
    public async Task<IActionResult> OnPostDayAsync(
        DateOnly date,
        CycleFlow flow,
        CycleMood mood,
        int pain,
        int[]? symptoms,
        string? note,
        CancellationToken cancellationToken) {
        if (!IsCoupleAccount(out var userId)) {
            return Forbid();
        }

        var submission = new CycleDaySubmission(
            date,
            flow,
            mood,
            pain,
            (symptoms ?? []).Aggregate(CycleSymptom.None, (all, value) => all | (CycleSymptom)value),
            note ?? string.Empty);

        return RedirectWith(await cycles.SaveDayAsync(userId, submission, cancellationToken), date.Year, date.Month);
    }

    #region 私有方法

    private async Task<IActionResult> LoadAsync(
        int userId,
        int? year,
        int? month,
        CancellationToken cancellationToken) {
        try {
            Dashboard = await cycles.GetDashboardAsync(
                userId,
                Request.PageNumber(),
                PageSize,
                year ?? clock.Today.Year,
                month ?? clock.Today.Month,
                cancellationToken);
            return Page();
        } catch (UnauthorizedAccessException) {
            return Forbid();
        }
    }

    private RedirectToPageResult RedirectWith(CycleWriteResult result, int? year = null, int? month = null) {
        TempData[result.IsSuccess ? "CycleFlash" : "CycleError"] = result.Message;
        return year is null || month is null
            ? RedirectToPage()
            : RedirectToPage(new { year, month });
    }

    private bool IsCoupleAccount(out int userId) {
        userId = User.UserId() ?? 0;
        return userId > 0 && User.Role() is UserRole.Boy or UserRole.Girl;
    }

    #endregion
}
