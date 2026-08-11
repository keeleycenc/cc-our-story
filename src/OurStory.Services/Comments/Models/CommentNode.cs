// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Comments;

/// <summary>
/// 页面上的一条留言，正文已经转义成可以直接输出的 HTML
/// </summary>
public class CommentNode {
    /// <summary>
    /// 获取 Id
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 获取作者名称
    /// </summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// 获取作者的 Url
    /// </summary>
    public string? AuthorUrl { get; init; }

    /// <summary>
    /// 获取已转义并按空行分段的正文
    /// </summary>
    public string ContentHtml { get; init; } = string.Empty;

    /// <summary>
    /// 获取创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 获取一个值，指示是不是站主两个人自己回的
    /// </summary>
    public bool IsOwner { get; init; }

    /// <summary>
    /// 获取一个值，指示是不是这篇记录的作者本人；另一半来留言不算，只有 TA 才挂「作者」
    /// </summary>
    public bool IsAuthor { get; init; }

    /// <summary>
    /// 获取站主的头像地址，访客留言为空，页面上退回文字头像
    /// </summary>
    public string AvatarUrl { get; init; } = string.Empty;

    /// <summary>
    /// 获取文字头像的配色编号，按称呼算出来，同一个人每次都是同一种颜色
    /// </summary>
    public int AvatarTone { get; init; }

    /// <summary>
    /// 获取或设置回复的是同层里的哪一条；直接回复顶层留言时为 null
    /// </summary>
    public int? ReplyToId { get; set; }

    /// <summary>
    /// 获取或设置回复的是谁，配合 <see cref="ReplyToId"/> 显示成「回复 @某人」
    /// </summary>
    public string? ReplyToName { get; set; }

    /// <summary>
    /// 获取文字头像上的那个字，emoji 这种代理对要整个取出来
    /// </summary>
    public string Initial => AuthorName.Length switch {
        0 => "?",
        1 => AuthorName,
        _ => char.IsSurrogatePair(AuthorName, 0) ? AuthorName[..2] : AuthorName[..1]
    };

    /// <summary>
    /// 获取挂在这条留言下面的回复
    ///
    /// 只有顶层留言会有内容：再深的回复也一律拍平到这一层，
    /// 靠 <see cref="ReplyToName"/> 交代回复关系，缩进不会一层层加深。
    /// </summary>
    public List<CommentNode> Replies { get; } = [];
}
