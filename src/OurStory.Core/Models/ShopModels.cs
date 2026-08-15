// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Models;

/// <summary>
/// 商城和仓库列表里的一张卡片
/// </summary>
/// <remarks>
/// 时间已经换算到站点时区
/// </remarks>
public sealed record ShopItemCard(
    int Id,
    string Title,
    string Description,
    string CoverUrl,
    int Price,
    bool IsPrivate,
    ShopRedeemMode RedeemMode,
    ShopItemStatus Status,
    UserRole SellerRole,
    string SellerName,
    UserRole? BuyerRole,
    string? BuyerName,
    DateTime ListedAt,
    DateTime? Deadline,
    int? DaysLeft,
    DateTime? UsedAt) {

    /// <summary>
    /// 获取这件心愿是不是还能被兑换
    /// </summary>
    public bool IsOnSale => Status == ShopItemStatus.Listed;

    /// <summary>
    /// 获取这件心愿是不是已经走到头了
    /// </summary>
    public bool IsFinal => Status is ShopItemStatus.Used or ShopItemStatus.ListingExpired or ShopItemStatus.Expired;

    /// <summary>
    /// 获取有效期是不是快到了，页面上给个提醒
    /// </summary>
    public bool IsExpiringSoon => !IsFinal && DaysLeft is >= 0 and <= 3;
}

/// <summary>
/// 一个人的心意余额
/// </summary>
/// <param name="Role">男主还是女主</param>
/// <param name="Name">页面上的称呼</param>
/// <param name="Balance">当前余额，等于全部流水之和</param>
/// <param name="Earned">累计获得</param>
/// <param name="Spent">累计花掉（正数）</param>
public sealed record HeartPointBalance(UserRole Role, string Name, int Balance, int Earned, int Spent);

/// <summary>
/// 心意账单上的一行
/// </summary>
public sealed record HeartPointRecord(
    long Id,
    int ChangeAmount,
    HeartPointReason Reason,
    string Note,
    bool IsBackfill,
    DateTime CreatedAt);

/// <summary>
/// 「初始心意」这一步的结果
/// </summary>
/// <param name="AlreadyDone">之前就算过了，这次什么都没做</param>
/// <param name="Entries">补记了多少条流水</param>
/// <param name="Total">补记的心意合计</param>
public sealed record HeartPointBackfillResult(bool AlreadyDone, int Entries, int Total);

/// <summary>
/// 一条自带的心愿预设
/// </summary>
/// <param name="Title">心愿名称</param>
/// <param name="Description">心愿描述</param>
/// <param name="RedeemMode">建议的核销方式</param>
public sealed record ShopPresetSeed(string Title, string Description, ShopRedeemMode RedeemMode);

/// <summary>
/// 心愿商品的默认预设
/// 
/// 预设随代码发布，并在首次初始化时写入数据库。初始化后作为普通数据管理，可自由编辑、停用或删除，后续升级不会重复写入
/// </summary>
public static class DefaultShopPresets {
    /// <summary>
    /// 获取全部默认预设，顺序即为展示顺序
    /// </summary>
    public static IReadOnlyList<ShopPresetSeed> All { get; } = [
        new("立刻听话", "可以让对方答应你一件小事，这次听你的", ShopRedeemMode.MutualConfirm),
        new("按摩十五分钟", "哪里累就告诉我，至少认真按满十五分钟", ShopRedeemMode.MutualConfirm),
        new("小跑腿", "有件懒得出门办的事，就交给我", ShopRedeemMode.MutualConfirm),
        new("一份小惊喜", "不用提前知道是什么，给我一次偷偷准备惊喜的机会", ShopRedeemMode.MutualConfirm),
        new("奶茶券", "想喝奶茶的时候用，这一杯我请", ShopRedeemMode.MutualConfirm),
        new("甜品券", "想吃点甜的时候用，这一份我来安排", ShopRedeemMode.MutualConfirm),
        new("小红包", "给你一个收红包的理由，金额看我心情", ShopRedeemMode.MutualConfirm),
        new("电影之夜", "今晚挑一部想看的电影，一起安安静静看完", ShopRedeemMode.MutualConfirm),
        new("游戏陪玩", "陪你打一会你想玩的游戏，认真上分不摆烂", ShopRedeemMode.MutualConfirm),
        new("发张现在的你", "不用特意准备，给我看看此刻的你", ShopRedeemMode.Instant),
        new("分享今天的小事", "挑一件今天发生的小事，认真讲给我听", ShopRedeemMode.Instant),
    ];
}

/// <summary>
/// 表示商城的一次操作结果
/// </summary>
/// <param name="Success">成没成</param>
/// <param name="Message">给人看的一句话</param>
public sealed record ShopActionResult(bool Success, string Message) {
    /// <summary>
    /// 成功
    /// </summary>
    public static ShopActionResult Ok(string message) => new(true, message);

    /// <summary>
    /// 失败
    /// </summary>
    public static ShopActionResult Fail(string message) => new(false, message);
}
