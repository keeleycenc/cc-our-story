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
    /// 获取或设置题目是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;

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
    /// 获取或设置每日采用题目集合
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
