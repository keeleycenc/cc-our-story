// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Models;

/// <summary>
/// 一次心有灵犀回答。选择题使用选项索引，开放题使用文字，两种载荷互斥
/// </summary>
public sealed record AffinityAnswerValue(
    IReadOnlyList<int> SelectedOptionIndexes,
    string? Text);

/// <summary>
/// 提交心有灵犀答案时的统一输入
/// </summary>
public sealed record AffinityAnswerSubmission(
    IReadOnlyCollection<int> SelectedOptionIndexes,
    string? Text);

/// <summary>
/// 今日题目的安全视图。未揭晓时不包含对方答案与作答时间
/// </summary>
public sealed record AffinityToday(
    int DailyQuestionId,
    string Day,
    int LoveDay,
    UserRole? CreatorRole,
    string Question,
    string Category,
    AffinityQuestionType Type,
    IReadOnlyList<string> Options,
    int RewardPoints,
    AffinityAnswerValue? MyAnswer,
    DateTime? MyAnsweredAt,
    AffinityAnswerValue? PartnerAnswer,
    DateTime? PartnerAnsweredAt,
    bool HasSameAnswer) {

    /// <summary>
    /// 获取是否已完成答题
    /// </summary>
    public bool HasAnswered => MyAnswer is not null;

    /// <summary>
    /// 获取是否已揭晓双方答案
    /// </summary>
    public bool IsRevealed => HasAnswered && PartnerAnswer is not null;
}

/// <summary>
/// 当前用户与双方共同答题的统计
/// </summary>
public sealed record AffinityStats(
    int TotalAnswers,
    int CurrentStreak,
    int SameChoiceAnswerDays,
    int CreatedQuestions);

/// <summary>
/// 双方已经揭晓的一条历史记录
/// </summary>
public sealed record AffinityHistoryItem(
    int DailyQuestionId,
    string Day,
    int LoveDay,
    UserRole? CreatorRole,
    string Question,
    string Category,
    AffinityQuestionType Type,
    string MyAnswer,
    DateTime MyAnsweredAt,
    string PartnerAnswer,
    DateTime PartnerAnsweredAt,
    int RewardPoints,
    bool HasSameAnswer);

/// <summary>
/// 后台只读的共同作答记录。题目完成后才允许展示题干
/// </summary>
public sealed record AffinityAnsweredQuestionCard(
    int DailyQuestionId,
    string Day,
    int LoveDay,
    string Question,
    string Category,
    AffinityQuestionType Type,
    int RewardPoints,
    string CreatorName,
    DateTime RevealedAt);

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
    string CreatorName,
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
    /// 答案与题型要求不符
    /// </summary>
    InvalidAnswer,

    /// <summary>
    /// 无权限提交
    /// </summary>
    Forbidden
}
