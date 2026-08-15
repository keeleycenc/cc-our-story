// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 表示心意商城里的一件心愿
/// </summary>
public class ShopItem {
    /// <summary>
    /// 获取或设置唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置心愿名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置心愿描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置封面图片地址；留空时前台摆一个图标加底色
    /// </summary>
    public string? CoverUrl { get; set; }

    /// <summary>
    /// 获取或设置兑换需要的心意
    /// </summary>
    public int Price { get; set; }

    /// <summary>
    /// 获取或设置是否仅情侣双方可见
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>
    /// 获取或设置核销方式
    /// </summary>
    public ShopRedeemMode RedeemMode { get; set; } = ShopRedeemMode.MutualConfirm;

    /// <summary>
    /// 获取或设置当前状态
    /// </summary>
    public ShopItemStatus Status { get; set; } = ShopItemStatus.Listed;

    /// <summary>
    /// 获取或设置发布者，也就是将来要去履约的那个人
    /// </summary>
    public int SellerId { get; set; }

    /// <summary>
    /// 获取或设置发布者
    /// </summary>
    public User? Seller { get; set; }

    /// <summary>
    /// 获取或设置兑换的人；还没被兑换时为 null
    /// </summary>
    public int? BuyerId { get; set; }

    /// <summary>
    /// 获取或设置兑换的人
    /// </summary>
    public User? Buyer { get; set; }

    /// <summary>
    /// 获取或设置发布时用的预设；预设后来被删掉也不影响这件心愿
    /// </summary>
    public int? PresetId { get; set; }

    /// <summary>
    /// 获取或设置发布时用的预设
    /// </summary>
    public ShopPreset? Preset { get; set; }

    /// <summary>
    /// 获取或设置上架有效期天数，留档用，实际到期看 <see cref="ListingExpiresAt"/>
    /// </summary>
    public int ListingDays { get; set; }

    /// <summary>
    /// 获取或设置兑换后的有效期天数，留档用，实际到期看 <see cref="ExpiresAt"/>
    /// </summary>
    public int ValidDays { get; set; }

    /// <summary>
    /// 获取或设置上架时间
    /// </summary>
    public DateTimeOffset ListedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置上架到期时间，过了还没人兑换就是 <see cref="ShopItemStatus.ListingExpired"/>
    /// </summary>
    public DateTimeOffset ListingExpiresAt { get; set; }

    /// <summary>
    /// 获取或设置兑换时间；没被兑换过为 null
    /// </summary>
    public DateTimeOffset? PurchasedAt { get; set; }

    /// <summary>
    /// 获取或设置兑换后的到期时间，从兑换那一刻起算
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// 获取或设置持有人发起核销的时间；发布者驳回或持有人撤回后清空
    /// </summary>
    public DateTimeOffset? RedeemRequestedAt { get; set; }

    /// <summary>
    /// 获取或设置真正用掉的时间
    /// </summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置最后一次状态变化的时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
