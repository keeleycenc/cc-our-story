// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Models;

/// <summary>
/// 今日题目的安全视图。未揭晓时不包含对方答案与作答时间
/// </summary>
public sealed record AffinityToday(
    int DailyQuestionId,
    string Day,
    string Question,
    string Category,
    AffinityQuestionType Type,
    IReadOnlyList<string> Options,
    int RewardPoints,
    int? MyOptionIndex,
    DateTime? MyAnsweredAt,
    int? PartnerOptionIndex,
    DateTime? PartnerAnsweredAt) {
    /// <summary>
    /// 获取是否已完成答题
    /// </summary>
    public bool HasAnswered => MyOptionIndex is not null;

    /// <summary>
    /// 获取是否已揭晓双方答案
    /// </summary>
    public bool IsRevealed => MyOptionIndex is not null && PartnerOptionIndex is not null;

    /// <summary>
    /// 获取双方答案是否一致
    /// </summary>
    public bool IsMatch => IsRevealed && MyOptionIndex == PartnerOptionIndex;
}

/// <summary>
/// 当前用户与双方共同答题的统计
/// </summary>
public sealed record AffinityStats(
    int AnsweredDays,
    int CurrentStreak,
    int RevealedDays,
    int MatchedDays) {
    /// <summary>
    /// 获取匹配率百分比
    /// </summary>
    public int MatchRate => RevealedDays == 0 ? 0 : (int)Math.Round(MatchedDays * 100d / RevealedDays);
}

/// <summary>
/// 双方已经揭晓的一条历史记录
/// </summary>
public sealed record AffinityHistoryItem(
    string Day,
    string Question,
    string Category,
    AffinityQuestionType Type,
    string MyAnswer,
    DateTime MyAnsweredAt,
    string PartnerAnswer,
    DateTime PartnerAnsweredAt,
    int RewardPoints,
    bool IsMatch);

/// <summary>
/// 获取亲密度主页数据
/// </summary>
public sealed record AffinityDashboard(
    AffinityToday? Today,
    AffinityStats Stats,
    PagedList<AffinityHistoryItem> History);

/// <summary>
/// 后台可见的封存题目元数据。不包含题干与选项文本
/// </summary>
public sealed record AffinityQuestionCard(
    int Id,
    string Category,
    AffinityQuestionType Type,
    bool IsActive,
    bool IsSealed,
    int OptionCount,
    int UsedCount,
    int RewardPoints,
    DateTime CreatedAt);

/// <summary>
/// 创建后即封存的题目输入
/// </summary>
public sealed class AffinityQuestionCreateModel {
    /// <summary>
    /// 获取或设置题目内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题目分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题目类型
    /// </summary>
    public AffinityQuestionType Type { get; set; } = AffinityQuestionType.SingleChoice;

    /// <summary>
    /// 获取或设置题目选项
    /// </summary>
    public IReadOnlyList<string> Options { get; set; } = [];

    /// <summary>
    /// 获取或设置心意奖励值
    /// </summary>
    public int RewardPoints { get; set; } = 5;
}

/// <summary>
/// 获取题目提交结果
/// </summary>
public enum AffinitySubmitResult {
    /// <summary>
    /// 提交成功
    /// </summary>
    Accepted,

    /// <summary>
    /// 已经提交过答案
    /// </summary>
    AlreadyAnswered,

    /// <summary>
    /// 题目无效
    /// </summary>
    InvalidQuestion,

    /// <summary>
    /// 选项无效
    /// </summary>
    InvalidOption,

    /// <summary>
    /// 无权限提交
    /// </summary>
    Forbidden
}
