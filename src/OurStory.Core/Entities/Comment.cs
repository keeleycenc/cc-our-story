// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 一条留言。登录之后 <see cref="AuthorId"/> 有值，访客留言只留下自己填的称呼。
/// </summary>
public class Comment {
    /// <summary>
    /// 获取或设置留言的唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置留言所属的动态的唯一标识符
    /// </summary>
    public int MomentId { get; set; }

    /// <summary>
    /// 获取或设置留言所属的动态
    /// </summary>
    public Moment? Moment { get; set; }

    /// <summary>
    /// 获取或设置回复的是哪一条；顶层留言为 null
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// 获取或设置回复的留言
    /// </summary>
    public Comment? Parent { get; set; }

    /// <summary>
    /// 获取或设置留言的作者的唯一标识符；访客留言为 null
    /// </summary>
    public int? AuthorId { get; set; }

    /// <summary>
    /// 获取或设置留言的作者；访客留言为 null
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// 获取或设置留言的作者的名称；访客留言为访客填写的称呼
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置留言的作者的邮箱；访客留言为访客填写的邮箱
    /// </summary>
    public string? AuthorMail { get; set; }

    /// <summary>
    /// 获取或设置留言的作者的个人主页；访客留言为访客填写的个人主页
    /// </summary>
    public string? AuthorUrl { get; set; }

    /// <summary>
    /// 获取或设置留言的内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置留言是否通过审核
    /// </summary>
    public bool IsApproved { get; set; } = true;

    /// <summary>
    /// 获取或设置访客指纹，和心动记录用的是同一套算法，不存原始 IP。
    /// </summary>
    public string? VisitorHash { get; set; }

    /// <summary>
    /// 获取或设置写这条留言的氛围组角色标识；人写的留言为 null
    /// </summary>
    public string? LlmMemberId { get; set; }

    /// <summary>
    /// 获取或设置留言当时那个角色的头像地址，角色被删掉之后就靠它
    /// </summary>
    public string? LlmAvatarUrl { get; set; }

    /// <summary>
    /// 获取或设置留言的创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置留言的回复
    /// </summary>
    public ICollection<Comment> Replies { get; set; } = [];

    /// <summary>
    /// 获取这条留言的来路
    /// </summary>
    public CommentSource Source =>
        LlmMemberId is not null ? CommentSource.LlmAtmosphere
        : AuthorId is not null ? CommentSource.Owner
        : CommentSource.Guest;
}
