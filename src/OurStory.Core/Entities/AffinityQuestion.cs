// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Entities;

/// <summary>
/// 获取心有灵犀题库中的一道单选题
/// </summary>
public class AffinityQuestion {
    /// <summary>
    /// 获取或设置题目标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置题目内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题目分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题型
    /// </summary>
    public AffinityQuestionType Type { get; set; } = AffinityQuestionType.SingleChoice;

    /// <summary>
    /// 获取或设置每位参与者完成本题可获得的心意值
    /// </summary>
    public int RewardPoints { get; set; } = 5;

    /// <summary>
    /// 获取或设置题目是否已封存。封存后内容不可通过后台管理功能读取或修改
    /// </summary>
    public bool IsSealed { get; set; } = true;

    /// <summary>
    /// 获取或设置题目是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 获取或设置创建者标识。系统预置题目没有创建者
    /// </summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// 获取或设置创建者
    /// </summary>
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置题目选项集合
    /// </summary>
    public ICollection<AffinityQuestionOption> Options { get; set; } = [];

    /// <summary>
    /// 获取或设置题目的采用记录。每道题最多采用一次
    /// </summary>
    public ICollection<AffinityDailyQuestion> DailyQuestions { get; set; } = [];
}

/// <summary>
/// 获取题库题目的一个可选答案
/// </summary>
public class AffinityQuestionOption {
    /// <summary>
    /// 获取或设置选项标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置所属题目标识
    /// </summary>
    public int QuestionId { get; set; }

    /// <summary>
    /// 获取或设置所属题目
    /// </summary>
    public AffinityQuestion? Question { get; set; }

    /// <summary>
    /// 获取或设置选项内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置选项排序值
    /// </summary>
    public int SortOrder { get; set; }
}
