// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 心愿状态和核销方式显示
/// </summary>
public static class ShopDisplay {
    /// <summary>
    /// 获取状态的中文名
    /// </summary>
    public static string StatusName(ShopItemStatus status) => status switch {
        ShopItemStatus.Listed => "上架中",
        ShopItemStatus.Redeemed => "可使用",
        ShopItemStatus.PendingConfirm => "待履约",
        ShopItemStatus.Used => "已使用",
        ShopItemStatus.ListingExpired => "未兑换且过期",
        ShopItemStatus.Expired => "已过期",
        _ => "未知"
    };

    /// <summary>
    /// 获取状态对应的图标
    /// </summary>
    public static string StatusIcon(ShopItemStatus status) => status switch {
        ShopItemStatus.Listed => "store",
        ShopItemStatus.Redeemed => "ticket",
        ShopItemStatus.PendingConfirm => "hourglass",
        ShopItemStatus.Used => "circle-check",
        _ => "ban"
    };

    /// <summary>
    /// 获取状态标签的样式后缀，配色写在样式表里
    /// </summary>
    public static string StatusTone(ShopItemStatus status) => status switch {
        ShopItemStatus.Listed => "listed",
        ShopItemStatus.Redeemed => "redeemed",
        ShopItemStatus.PendingConfirm => "pending",
        ShopItemStatus.Used => "used",
        _ => "dead"
    };

    /// <summary>
    /// 获取核销方式的中文名
    /// </summary>
    public static string RedeemName(ShopRedeemMode mode) =>
        mode == ShopRedeemMode.Instant ? "立即使用" : "双方确认";

    /// <summary>
    /// 获取核销方式的说明
    /// </summary>
    public static string RedeemHint(ShopRedeemMode mode) => mode == ShopRedeemMode.Instant
        ? "使用后立即核销，无需对方确认"
        : "提交使用后，由对方确认完成核销";

    /// <summary>
    /// 获取一条心意流水的名称
    /// </summary>
    public static string ReasonName(HeartPointReason reason) => reason switch {
        HeartPointReason.DailyHeartbeat => "想你",
        HeartPointReason.MomentPublished => "点点滴滴",
        HeartPointReason.AnniversaryPublished => "纪念日",
        HeartPointReason.Purchase => "兑换心愿",
        _ => "心意"
    };

    /// <summary>
    /// 获取一条心意流水的图标
    /// </summary>
    public static string ReasonIcon(HeartPointReason reason) => reason switch {
        HeartPointReason.DailyHeartbeat => "heart",
        HeartPointReason.MomentPublished => "camera",
        HeartPointReason.AnniversaryPublished => "calendar-heart",
        HeartPointReason.Purchase => "hand-heart",
        _ => "coins"
    };

    /// <summary>
    /// 没有封面时，按标题挑一格底色，同一件心愿每次看到的都一样
    /// </summary>
    public static string CoverTone(int id) => (id % 6) switch {
        0 => "peach",
        1 => "mint",
        2 => "blue",
        3 => "rose",
        4 => "lilac",
        _ => "gold"
    };
}
