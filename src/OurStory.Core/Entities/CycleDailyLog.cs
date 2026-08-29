// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Entities;

/// <summary>
/// 表示某一天补充的身体状况，与经期记录保持独立
/// </summary>
/// <remarks>
/// 同一情侣关系在同一天仅保留一条记录，双方均可修改。
/// 周期日期发生变化时，每日补充记录不会随之删除。
/// </remarks>
public sealed class CycleDailyLog {
    /// <summary>
    /// 获取或设置记录标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置所属情侣关系标识
    /// </summary>
    public int RelationshipId { get; set; }

    /// <summary>
    /// 获取或设置这条记录对应的日期
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 获取或设置当天的经量
    /// </summary>
    public CycleFlow Flow { get; set; }

    /// <summary>
    /// 获取或设置当天的心情
    /// </summary>
    public CycleMood Mood { get; set; }

    /// <summary>
    /// 获取或设置当天的不适程度，0 表示没有，3 表示明显
    /// </summary>
    public int Pain { get; set; }

    /// <summary>
    /// 获取或设置当天记下的不适
    /// </summary>
    public CycleSymptom Symptoms { get; set; }

    /// <summary>
    /// 获取或设置当天的补充说明
    /// </summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置首次创建者
    /// </summary>
    public int CreatedByUserId { get; set; }

    /// <summary>
    /// 获取或设置最后修改者
    /// </summary>
    public int UpdatedByUserId { get; set; }

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置最后修改时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取一个值，指示当前记录是否不包含任何有效内容
    /// </summary>
    public bool IsEmpty =>
        Flow == CycleFlow.Unset
        && Mood == CycleMood.Unset
        && Pain <= 0
        && Symptoms == CycleSymptom.None
        && Note.Length == 0;

    /// <summary>
    /// 获取或设置所属情侣关系
    /// </summary>
    public CoupleRelationship? Relationship { get; set; }

    /// <summary>
    /// 获取或设置创建者
    /// </summary>
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// 获取或设置最后修改者
    /// </summary>
    public User? UpdatedByUser { get; set; }
}
