// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Entities;

/// <summary>
/// 表示站点中的一段情侣关系。当前产品只有一段有效关系，显式建模是为了让私密数据始终有清晰的权限边界。
/// </summary>
public sealed class CoupleRelationship {
    /// <summary>
    /// 获取或设置关系标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置关系是否仍然有效
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取属于这段关系的用户
    /// </summary>
    public ICollection<User> Members { get; set; } = [];

    /// <summary>
    /// 获取属于这段关系的花信记录
    /// </summary>
    public ICollection<CycleRecord> CycleRecords { get; set; } = [];

    /// <summary>
    /// 获取属于这段关系的每日补充记录
    /// </summary>
    public ICollection<CycleDailyLog> CycleDailyLogs { get; set; } = [];
}
