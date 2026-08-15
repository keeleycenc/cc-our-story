// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 心愿预设
/// </summary>
public class ShopPreset {
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
    /// 获取或设置封面图片地址，选中预设时一并带进发布表单
    /// </summary>
    /// <remarks>
    /// 系统自带的那几个不配图：十来个预设各挂一张图，后台列表会变成一面照片墙
    /// </remarks>
    public string? CoverUrl { get; set; }

    /// <summary>
    /// 获取或设置建议的核销方式，选中预设时一并带出来
    /// </summary>
    public ShopRedeemMode RedeemMode { get; set; } = ShopRedeemMode.MutualConfirm;

    /// <summary>
    /// 获取或设置下拉框里的排序，小的排在前面
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 获取或设置是否还在下拉框里出现
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
