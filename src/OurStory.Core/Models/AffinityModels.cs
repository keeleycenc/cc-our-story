// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
namespace OurStory.Core.Models;

/// <summary>
/// 获取今日题目的安全视图。未揭晓时 PartnerOptionIndex 永远为空
/// </summary>
public sealed record AffinityToday(
    int DailyQuestionId,
    string Day,
    string Question,
    string Category,
    IReadOnlyList<string> Options,
    int? MyOptionIndex,
    int? PartnerOptionIndex) {
    /// <summary>
    /// 获取是否已回答今日题目
    /// </summary>
    public bool HasAnswered => MyOptionIndex is not null;

    /// <summary>
    /// 获取是否已揭晓双方答案
    /// </summary>
    public bool IsRevealed => MyOptionIndex is not null && PartnerOptionIndex is not null;

    /// <summary>
    /// 获取双方答案是否匹配
    /// </summary>
    public bool IsMatch => IsRevealed && MyOptionIndex == PartnerOptionIndex;
}

/// <summary>
/// 获取心有灵犀统计数据
/// </summary>
/// <param name="AnsweredDays">获取已回答天数</param>
/// <param name="MatchedDays">获取匹配天数</param>
public sealed record AffinityStats(int AnsweredDays, int MatchedDays) {
    /// <summary>
    /// 获取匹配率百分比
    /// </summary>
    public int MatchRate => AnsweredDays == 0 ? 0 : (int)Math.Round(MatchedDays * 100d / AnsweredDays);
}

/// <summary>
/// 获取心有灵犀历史记录项
/// </summary>
public sealed record AffinityHistoryItem(
    string Day,
    string Question,
    string Category,
    string MyAnswer,
    string PartnerAnswer,
    bool IsMatch);

/// <summary>
/// 获取心有灵犀仪表盘数据
/// </summary>
public sealed record AffinityDashboard(
    AffinityToday? Today,
    AffinityStats Stats,
    PagedList<AffinityHistoryItem> History);

/// <summary>
/// 获取心有灵犀题目卡片信息
/// </summary>
public sealed record AffinityQuestionCard(
    int Id,
    string Text,
    string Category,
    bool IsActive,
    IReadOnlyList<string> Options,
    int UsedCount);

/// <summary>
/// 获取或设置心有灵犀题目编辑模型
/// </summary>
public sealed class AffinityQuestionEditModel {
    /// <summary>
    /// 获取或设置题目内容
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题目分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置题目选项集合
    /// </summary>
    public IReadOnlyList<string> Options { get; set; } = [];

    /// <summary>
    /// 获取或设置题目是否启用
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 获取心有灵犀提交结果
/// </summary>
public enum AffinitySubmitResult {
    /// <summary>
    /// 获取已接受提交
    /// </summary>
    Accepted,

    /// <summary>
    /// 获取已回答提示
    /// </summary>
    AlreadyAnswered,

    /// <summary>
    /// 获取无效题目提示
    /// </summary>
    InvalidQuestion,

    /// <summary>
    /// 获取无效选项提示
    /// </summary>
    InvalidOption,

    /// <summary>
    /// 获取无权限提示
    /// </summary>
    Forbidden
}
