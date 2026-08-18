// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Comments;

/// <summary>
/// 提交上来的一条留言
/// </summary>
public class CommentSubmission {
    /// <summary>
    /// 获取或设置 MomentId
    /// </summary>
    public int MomentId { get; set; }

    /// <summary>
    /// 获取或设置 ParentId
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// 获取或设置 AuthorId
    /// </summary>
    public int? AuthorId { get; set; }

    /// <summary>
    /// 获取或设置 AuthorName
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 AuthorMail
    /// </summary>
    public string? AuthorMail { get; set; }

    /// <summary>
    /// 获取或设置 AuthorUrl
    /// </summary>
    public string? AuthorUrl { get; set; }

    /// <summary>
    /// 获取或设置 Content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 VisitorHash
    /// </summary>
    public string? VisitorHash { get; set; }

    /// <summary>
    /// 获取或设置写这条留言的氛围组角色标识；人写的留言留空
    /// </summary>
    public string? LlmMemberId { get; set; }

    /// <summary>
    /// 获取或设置那个角色当时的头像地址
    /// </summary>
    public string? LlmAvatarUrl { get; set; }
}
