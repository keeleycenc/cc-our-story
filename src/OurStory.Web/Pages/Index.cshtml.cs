// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Heartbeats;
using OurStory.Services.Affinity;
using OurStory.Services.Anniversaries;
using OurStory.Services.Cycles;
using OurStory.Services.Moments;
using OurStory.Services.Settings;
using OurStory.Services.Shop;
using OurStory.Web.Infrastructure;
using System.Security.Cryptography;

namespace OurStory.Web.Pages;

/// <summary>
/// 表示 IndexModel
/// </summary>
public class IndexModel(
    ISettingsService settingsService,
    IMomentService moments,
    IAnniversaryService anniversaries,
    IHeartbeatService heartbeats,
    IShopService shop,
    IAffinityService affinity,
    ICycleService cycles,
    VisitorIdentityAccessor visitors,
    HeartbeatTokenService tokens,
    MomentUnlockStore unlockStore) : PageModel {
    /// <summary>
    /// 执行 Site 操作
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 获取或设置 LatestMoments
    /// </summary>
    public IReadOnlyList<MomentCard> LatestMoments { get; private set; } = [];

    /// <summary>
    /// 获取或设置 MomentsCount
    /// </summary>
    public int MomentsCount { get; private set; }

    /// <summary>
    /// 获取或设置可见纪念日数量
    /// </summary>
    public int AnniversariesCount { get; private set; }

    /// <summary>
    /// 获取或设置商城里上架中的心愿数量
    /// </summary>
    public int ShopCount { get; private set; }

    /// <summary>
    /// 获取或设置首页展示的最新心愿
    /// </summary>
    public IReadOnlyList<ShopItemCard> LatestShopItems { get; private set; } = [];

    /// <summary>
    /// 执行 Heartbeat 操作
    /// </summary>
    public HeartbeatSummary Heartbeat { get; private set; } = new();

    /// <summary>
    /// 获取或设置 HeartbeatToken
    /// </summary>
    public string HeartbeatToken { get; private set; } = string.Empty;

    /// <summary>
    /// 每次打开首页随机挑一句
    /// </summary>
    public string LoveLetter { get; private set; } = string.Empty;

    /// <summary>
    /// 首页心有灵犀入口的今日状态
    /// </summary>
    public string AffinityStatus { get; private set; } = "今日待回答";

    /// <summary>
    /// 首页花信如期入口状态
    /// </summary>
    public string CycleStatus { get; private set; } = "登录后查看 · 仅限情侣";

    /// <summary>
    /// 获取 IsGuest
    /// </summary>
    public bool IsGuest => Heartbeat.Role == "guest";

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        Site = await settingsService.GetAsync(cancellationToken);

        var viewer = new MomentViewer(User.IsOwner(), unlockStore.UnlockedIds());
        LatestMoments = await moments.GetLatestAsync(3, viewer, cancellationToken);
        MomentsCount = await moments.CountPublishedAsync(cancellationToken);
        AnniversariesCount = await anniversaries.CountForViewerAsync(User.IsOwner(), cancellationToken);

        var shopViewer = new ShopViewer(User.Role(), User.UserId());
        var latestShop = await shop.GetPageAsync(
            new ShopQuery(PageSize: 3, Status: ShopItemStatus.Listed),
            shopViewer,
            cancellationToken);
        LatestShopItems = latestShop.Items;
        ShopCount = latestShop.TotalCount;

        var who = await visitors.GetAsync(cancellationToken);
        Heartbeat = await heartbeats.GetSummaryAsync(who, cancellationToken);
        HeartbeatToken = tokens.Issue(who);

        AffinityStatus = User.UserId() is { } userId
            ? await affinity.GetTodayStatusAsync(userId, User.Role(), cancellationToken)
            : "登录后参与 · 仅限情侣";

        CycleStatus = User.UserId() is { } cycleUserId
            ? await cycles.GetHomeStatusAsync(cycleUserId, cancellationToken)
            : "登录后查看 · 仅限情侣";

        LoveLetter = Site.LoveLetters.Count == 0
            ? string.Empty
            : Site.LoveLetters[RandomNumberGenerator.GetInt32(Site.LoveLetters.Count)];
    }

    /// <summary>
    /// 获取当前身份在页面上的显示名称
    /// </summary>
    public string RoleName() => Heartbeat.Role switch {
        "boy" => Site.BoyName,
        "girl" => Site.GirlName,
        _ => "路过的朋友"
    };
}
