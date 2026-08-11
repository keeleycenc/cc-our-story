// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Models;
using OurStory.Services.Comments;
using OurStory.Services.Moments;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Moments;

/// <summary>
/// 表示 DetailModel
/// </summary>
public class DetailModel(
    ISettingsService settingsService,
    IMomentService moments,
    ICommentService comments,
    MomentUnlockStore unlockStore,
    VisitorIdentityAccessor visitors) : PageModel {
    private const string CommentErrorKey = "CommentError";

    /// <summary>
    /// 执行 Site 操作
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 执行 Detail 操作
    /// </summary>
    public MomentDetail Detail { get; private set; } = new();

    /// <summary>
    /// 执行 Comments 操作
    /// </summary>
    public CommentsViewModel Comments { get; private set; } = new();

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task<IActionResult> OnGetAsync(string slug, int replyTo, CancellationToken cancellationToken) {
        var detail = await LoadAsync(slug, cancellationToken);
        if (detail is null) {
            return NotFound();
        }

        Detail = detail;
        Comments = await BuildCommentsAsync(detail, replyTo, TempData[CommentErrorKey] as string, cancellationToken);
        return Page();
    }

    /// <summary>
    /// 受密码保护的记录，前端就地校验，不跳异常页。
    ///
    /// 密码对了回 200，前端自己刷新；不对回 403 + 提示页，
    /// 前端从里面把提示语抠出来放进弹窗。
    /// </summary>
    public async Task<IActionResult> OnPostUnlockAsync(string slug, string? protectPassword, CancellationToken cancellationToken) {
        var momentId = await moments.UnlockAsync(slug, protectPassword ?? string.Empty, cancellationToken);
        if (momentId is null) {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Partial("_ExceptionDocument", new ExceptionDocumentModel(403, "密码不正确，请检查后重新输入。"));
        }

        unlockStore.Remember(momentId.Value);
        return new OkResult();
    }

    /// <summary>
    /// 处理 CommentAsync(string, int, string?, string?, string?, string?, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostCommentAsync(
        string slug,
        int replyTo,
        string? author,
        string? mail,
        string? url,
        string? text,
        CancellationToken cancellationToken) {
        var detail = await LoadAsync(slug, cancellationToken);
        if (detail is null) {
            return NotFound();
        }

        var site = await settingsService.GetAsync(cancellationToken);
        var isLoggedIn = User.IsOwner();

        if (!detail.AllowComment || (!isLoggedIn && !site.AllowGuestComments)) {
            return RedirectToPage(new { slug });
        }

        var error = Validate(site, isLoggedIn, author, mail, text);
        if (error is not null) {
            TempData[CommentErrorKey] = error;
            return Redirect($"/moments/{slug}#respond");
        }

        var who = await visitors.GetAsync(cancellationToken);

        var created = await comments.AddAsync(new CommentSubmission {
            MomentId = detail.Id,
            ParentId = replyTo > 0 ? replyTo : null,
            AuthorId = isLoggedIn ? User.UserId() : null,
            AuthorName = isLoggedIn ? site.RoleName(User.Role()) : author!.Trim(),
            AuthorMail = isLoggedIn ? null : mail,
            AuthorUrl = isLoggedIn ? null : url,
            Content = text!,
            VisitorHash = isLoggedIn ? null : who.VisitorHash
        }, cancellationToken);

        // 直接回到刚写的那一条，不用自己在列表里找
        return Redirect($"/moments/{slug}#comment-{created.Id}");
    }

    private async Task<MomentDetail?> LoadAsync(string slug, CancellationToken cancellationToken) {
        Site = await settingsService.GetAsync(cancellationToken);
        var viewer = new MomentViewer(User.IsOwner(), unlockStore.UnlockedIds());
        return await moments.GetDetailAsync(slug, viewer, cancellationToken);
    }

    private async Task<CommentsViewModel> BuildCommentsAsync(
        MomentDetail detail,
        int replyTo,
        string? error,
        CancellationToken cancellationToken) {
        // 上锁没解开时连留言都不显示，否则等于从留言里泄露了内容
        var nodes = detail.IsLocked
            ? []
            : await comments.GetTreeAsync(detail.Id, cancellationToken);

        return new CommentsViewModel {
            Slug = detail.Slug,
            AllowComment = detail.AllowComment,
            Comments = nodes,
            TotalCount = detail.IsLocked ? 0 : detail.CommentCount,
            ReplyTo = replyTo,
            RequireMail = Site.CommentsRequireMail,
            AllowGuestComments = Site.AllowGuestComments,
            IsLoggedIn = User.IsOwner(),
            DisplayName = Site.RoleName(User.Role()),
            Error = error
        };
    }

    private static string? Validate(SiteSettings site, bool isLoggedIn, string? author, string? mail, string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return "留言内容不能为空。";
        }

        if (text.Length > 2000) {
            return "留言太长了，2000 字以内吧。";
        }

        if (isLoggedIn) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(author)) {
            return "还没有填写称呼。";
        }

        if (site.CommentsRequireMail && string.IsNullOrWhiteSpace(mail)) {
            return "请填写邮箱。";
        }

        return null;
    }
}
