// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Anniversaries;
using OurStory.Services.Comments;
using OurStory.Services.Heartbeats;
using OurStory.Services.Moments;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Areas.Admin.Pages;

/// <summary>
/// 表示 IndexModel
/// </summary>
public class IndexModel(
    ISettingsService settingsService,
    IMomentService moments,
    IAnniversaryService anniversaries,
    ICommentService comments,
    IHeartbeatService heartbeats,
    VisitorIdentityAccessor visitors,
    SiteClock clock) : PageModel {
    /// <summary>
    /// 总览上一次列几个纪念日
    /// </summary>
    private const int UpcomingSize = 5;

    /// <summary>
    /// 执行 Site 操作
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 获取或设置 PublishedCount
    /// </summary>
    public int PublishedCount { get; private set; }

    /// <summary>
    /// 获取或设置 TotalCount
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// 获取或设置 CommentCount
    /// </summary>
    public int CommentCount { get; private set; }

    /// <summary>
    /// 获取或设置纪念日数量
    /// </summary>
    public int AnniversaryCount { get; private set; }

    /// <summary>
    /// 执行 Heartbeat 操作
    /// </summary>
    public HeartbeatSummary Heartbeat { get; private set; } = new();

    /// <summary>
    /// 获取或设置 Recent
    /// </summary>
    public IReadOnlyList<Moment> Recent { get; private set; } = [];

    /// <summary>
    /// 获取或设置总览上要提醒的纪念日
    /// </summary>
    /// <remarks>
    /// 只留还会再来的那几个：已经过去的一次性纪念日在这里提醒不了什么。
    /// </remarks>
    public IReadOnlyList<AnniversaryOccurrence> Upcoming { get; private set; } = [];

    /// <summary>
    /// 获取或设置 LoveDays
    /// </summary>
    public int LoveDays { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        Site = await settingsService.GetAsync(cancellationToken);

        Recent = (await moments.ListForAdminAsync(1, 6, User.UserId(), cancellationToken)).Items;
        TotalCount = await moments.CountAsync(cancellationToken);
        PublishedCount = await moments.CountPublishedAsync(cancellationToken);
        CommentCount = await comments.CountAsync(cancellationToken);

        // 服务里已经按「下一次在什么时候」排好序了，取前几个就是最近要到的
        var allAnniversaries = await anniversaries.GetAllAsync(cancellationToken);
        AnniversaryCount = allAnniversaries.Count;
        Upcoming = [.. allAnniversaries.Where(item => !item.IsArchived).Take(UpcomingSize)];

        Heartbeat = await heartbeats.GetSummaryAsync(await visitors.GetAsync(cancellationToken), cancellationToken);
        LoveDays = LoveTimeline.DayNumber(clock.LocalNow, Site.LoveStartedAt);
    }

    /// <summary>
    /// 转换Local
    /// </summary>
    public DateTime ToLocal(DateTimeOffset instant) => clock.ToLocal(instant);
}
