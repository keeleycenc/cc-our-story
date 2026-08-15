// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;

namespace OurStory.Services.Shop;

/// <summary>
/// 发布一件心愿要填数据项
/// </summary>
public class ShopPublishModel {
    /// <summary>
    /// 获取或设置心愿名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置心愿描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置封面图片地址，留空时前台摆一个图标
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
    /// 获取或设置上架多少天没人兑换就下架
    /// </summary>
    public int ListingDays { get; set; } = 30;

    /// <summary>
    /// 获取或设置兑换之后多少天没用掉就作废
    /// </summary>
    public int ValidDays { get; set; } = 30;

    /// <summary>
    /// 获取或设置用了哪个预设，手填的话为空
    /// </summary>
    public int? PresetId { get; set; }
}

/// <summary>
/// 心愿预设的编辑数据
/// </summary>
public class ShopPresetEditModel {
    /// <summary>
    /// 获取或设置心愿名称
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置心愿描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置封面图片地址，选中预设时一并带进发布表单
    /// </summary>
    public string? CoverUrl { get; set; }

    /// <summary>
    /// 获取或设置建议的核销方式
    /// </summary>
    public ShopRedeemMode RedeemMode { get; set; } = ShopRedeemMode.MutualConfirm;

    /// <summary>
    /// 获取或设置下拉框里的排序
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// 商城列表的筛选条件
/// </summary>
/// <param name="Page">页码，从 1 开始</param>
/// <param name="PageSize">每页几件</param>
/// <param name="Seller">只看某个人发布的，null 表示不限</param>
/// <param name="Status">只看某个状态的，null 表示不限</param>
public sealed record ShopQuery(int Page = 1, int PageSize = 12, UserRole? Seller = null, ShopItemStatus? Status = null);

/// <summary>
/// 谁在看这个商城
/// </summary>
/// <param name="Role">身份</param>
/// <param name="UserId">登录用户的主键，访客为 null</param>
public sealed record ShopViewer(UserRole Role, int? UserId) {
    /// <summary>
    /// 获取一个值，指示是否是两个人当中的一个
    /// </summary>
    public bool IsOwner => Role is UserRole.Boy or UserRole.Girl;

    /// <summary>获取访客</summary>
    public static ShopViewer Guest { get; } = new(UserRole.Guest, null);
}
