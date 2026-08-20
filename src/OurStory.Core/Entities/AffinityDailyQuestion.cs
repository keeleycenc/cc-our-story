// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Entities;

/// <summary>
/// 获取某一天采用的题目。题干与选项均为快照
/// </summary>
public class AffinityDailyQuestion {
    /// <summary>
    /// 获取或设置题目记录唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置题目所属日期
    /// </summary>
    public string Day { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置来源题目的标识
    /// </summary>
    public int? QuestionId { get; set; }

    /// <summary>
    /// 获取或设置来源题目
    /// </summary>
    public AffinityQuestion? Question { get; set; }

    /// <summary>
    /// 获取或设置题目内容快照
    /// </summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题目分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题型快照
    /// </summary>
    public AffinityQuestionType Type { get; set; } = AffinityQuestionType.SingleChoice;

    /// <summary>
    /// 获取或设置每位参与者的答题奖励快照
    /// </summary>
    public int RewardPoints { get; set; }

    /// <summary>
    /// 获取或设置题目选项快照 JSON
    /// </summary>
    public string OptionsJson { get; set; } = "[]";

    /// <summary>
    /// 获取或设置创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置该题目的回答集合
    /// </summary>
    public ICollection<AffinityAnswer> Answers { get; set; } = [];
}

/// <summary>
/// 获取男主或女主对某日题目的唯一答案
/// </summary>
public class AffinityAnswer {
    /// <summary>
    /// 获取或设置答案记录唯一标识
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 获取或设置每日题目标识
    /// </summary>
    public int DailyQuestionId { get; set; }

    /// <summary>
    /// 获取或设置每日题目
    /// </summary>
    public AffinityDailyQuestion? DailyQuestion { get; set; }

    /// <summary>
    /// 获取或设置回答用户标识
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 获取或设置回答用户
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 获取或设置用户角色
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// 获取或设置选择的选项索引
    /// </summary>
    public int OptionIndex { get; set; }

    /// <summary>
    /// 获取或设置回答时间
    /// </summary>
    public DateTimeOffset AnsweredAt { get; set; } = DateTimeOffset.UtcNow;
}
