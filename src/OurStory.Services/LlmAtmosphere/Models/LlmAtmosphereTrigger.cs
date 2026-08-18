// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 表示一项已调度的氛围组互动任务
/// </summary>
/// <param name="Kind">互动类型，是发表评论还是回复已有留言</param>
/// <param name="MomentId">目标点点滴滴的唯一标识符</param>
/// <param name="MemberId">执行互动的角色唯一标识符</param>
/// <param name="DueAt">计划执行时间</param>
/// <param name="ParentCommentId">目标父留言的唯一标识符；<see cref="LlmAtmosphereTriggerKind.Comment"/> 时为 null</param>
public sealed record LlmAtmosphereTrigger(
    LlmAtmosphereTriggerKind Kind,
    int MomentId,
    string MemberId,
    DateTimeOffset DueAt,
    int? ParentCommentId = null) {
    /// <summary>
    /// 获取用于识别同一互动任务的去重键
    /// </summary>
    /// <remarks>
    /// 计划执行时间不参与去重。同一角色针对同一记录和目标留言的同类互动只保留最先调度的一项。
    /// </remarks>
    public (LlmAtmosphereTriggerKind Kind, int MomentId, string MemberId, int? ParentCommentId) Key =>
        (Kind, MomentId, MemberId, ParentCommentId);
}

/// <summary>
/// 定义氛围组互动任务的类型
/// </summary>
public enum LlmAtmosphereTriggerKind {
    /// <summary>
    /// 在目标记录下发表一条顶层留言
    /// </summary>
    Comment = 0,

    /// <summary>
    /// 回复目标记录下已有的留言
    /// </summary>
    Reply = 1
}
