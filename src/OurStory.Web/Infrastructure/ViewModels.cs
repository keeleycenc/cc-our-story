// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services.Comments;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 详情页下半截「留言」那一段需要的全部东西
/// </summary>
public class CommentsViewModel {
    /// <summary>
    /// 获取或设置 Slug
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// 获取或设置 AllowComment
    /// </summary>
    public bool AllowComment { get; init; }

    /// <summary>
    /// 获取或设置 Comments
    /// </summary>
    public IReadOnlyList<CommentNode> Comments { get; init; } = [];

    /// <summary>
    /// 获取或设置 TotalCount
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>正在回复哪一条；0 表示写新留言。</summary>
    public int ReplyTo { get; init; }

    /// <summary>
    /// 获取或设置 RequireMail
    /// </summary>
    public bool RequireMail { get; init; }

    /// <summary>
    /// 获取或设置 AllowGuestComments
    /// </summary>
    public bool AllowGuestComments { get; init; }

    /// <summary>
    /// 获取或设置 IsLoggedIn
    /// </summary>
    public bool IsLoggedIn { get; init; }

    /// <summary>
    /// 获取或设置 DisplayName
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>上一次提交失败的原因，直接显示在表单上方。</summary>
    public string? Error { get; init; }
}

/// <summary>留言列表里的一条。楼中楼只有两层，所以这个局部视图最多再套一层自己</summary>
/// <param name="Comment">这条留言</param>
/// <param name="Slug">所属记录的短名，用来拼「回复」的地址</param>
/// <param name="IsReply">是不是挂在别人下面的回复，决定头像大小和缩进</param>
public record CommentItemModel(CommentNode Comment, string Slug, bool IsReply = false) {
    /// <summary>
    /// 回复超过这个条数就折叠
    /// </summary>
    public const int ReplyPreview = 3;
}

/// <summary>独立的提示页（404 以外的异常，以及密码错误的回执）</summary>
/// <param name="Code">HTTP 状态码</param>
/// <param name="Message">给人看的一句话</param>
public record ExceptionDocumentModel(int Code, string Message) {
    /// <summary>
    /// 获取 Title
    /// </summary>
    public string Title => Code switch {
        404 => "页面走丢了",
        403 when Message.Contains("密码", StringComparison.Ordinal) => "密码不正确",
        _ => "温馨提示"
    };

    /// <summary>
    /// 获取 IconName
    /// </summary>
    public string IconName => Code switch {
        404 => "heart-crack",
        403 when Message.Contains("密码", StringComparison.Ordinal) => "lock",
        _ => "heart"
    };
}
