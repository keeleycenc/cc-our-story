// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 表示一条心意流水
/// </summary>
public class HeartPointEntry {
    /// <summary>
    /// 获取或设置唯一标识
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 获取或设置这笔流水算在谁头上，只会是男主或女主
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 获取或设置流水归属的用户
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 获取或设置变动数额，获得为正，兑换为负
    /// </summary>
    public int ChangeAmount { get; set; }

    /// <summary>
    /// 获取或设置这笔心意的来头
    /// </summary>
    public HeartPointReason Reason { get; set; }

    /// <summary>
    /// 获取或设置去重用的来源键，形如 <c>heartbeat:2026-08-15</c> 或 <c>purchase:12</c>
    /// </summary>
    /// <remarks>
    /// 和 <see cref="UserId"/> 一起做了唯一索引：奖励每天最多发一次、
    /// 同一次兑换最多扣一次，靠的都是它，而不是先查再写那一套
    /// </remarks>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置账单上显示的一句话
    /// </summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否由「初始心意」补记
    /// </summary>
    /// <remarks>
    /// 商城上线之前就发生过的事，补记时打上这个记号，账单上分得清哪些是后来补的
    /// </remarks>
    public bool IsBackfill { get; set; }

    /// <summary>
    /// 获取或设置记账时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
