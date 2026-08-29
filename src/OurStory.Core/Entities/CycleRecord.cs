// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Entities;

/// <summary>
/// 表示一次经期事实记录。统计与预测均根据事实动态计算，不存储冗余结果
/// </summary>
/// <remarks>
/// 模型小结需要消耗 Token，因此允许持久化保存。
/// <see cref="SummaryStamp"/> 用于标识生成小结时采用的事实版本；事实变化后，小结将失效并等待重新生成。
/// </remarks>
public sealed class CycleRecord {
    /// <summary>
    /// 获取或设置记录标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置所属情侣关系标识
    /// </summary>
    public int RelationshipId { get; set; }

    /// <summary>
    /// 获取或设置开始日期
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// 获取或设置结束日期；空值表示正在进行
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// 获取或设置双方共同可见的备注
    /// </summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置本次周期的小结正文；尚未生成时为空
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置小结的来源
    /// </summary>
    public CycleSummarySource SummarySource { get; set; } = CycleSummarySource.Rule;

    /// <summary>
    /// 获取或设置生成小结时所依据事实的指纹
    /// </summary>
    /// <remarks>
    /// 与当前事实指纹不一致时，表示日期、备注或每日记录已发生变化，需要重新生成小结。
    /// </remarks>
    public string SummaryStamp { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置小结的生成时间；尚未生成时为空
    /// </summary>
    public DateTimeOffset? SummaryUpdatedAt { get; set; }

    /// <summary>
    /// 获取或设置首次创建者
    /// </summary>
    public int CreatedByUserId { get; set; }

    /// <summary>
    /// 获取或设置最后修改者
    /// </summary>
    public int UpdatedByUserId { get; set; }

    /// <summary>
    /// 获取或设置客户端写入幂等键
    /// </summary>
    public string RequestKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置最后修改时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

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
