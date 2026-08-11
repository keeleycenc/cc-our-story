// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Settings;
using System.Net;
using System.Text;

namespace OurStory.Services.Comments;

internal class CommentService(OurStoryDbContext db, ISettingsService settings, SiteClock clock) : ICommentService {
    public async Task<IReadOnlyList<CommentNode>> GetTreeAsync(int momentId, CancellationToken cancellationToken = default) {
        var site = await settings.GetAsync(cancellationToken);

        // 「作者」角标只给写这篇记录的那一位，另一半过来留言也是普通身份
        var authorId = await db.Moments
            .Where(moment => moment.Id == momentId)
            .Select(moment => (int?)moment.AuthorId)
            .FirstOrDefaultAsync(cancellationToken);

        var comments = await db.Comments
            .Where(comment => comment.MomentId == momentId && comment.IsApproved)
            .OrderBy(comment => comment.CreatedAt)
            .Include(comment => comment.Author)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var nodes = comments.ToDictionary(comment => comment.Id, comment => ToNode(comment, site, authorId));

        // 父留言被删掉的孤儿回复不算数，当顶层显示，不让它凭空消失
        var parents = comments
            .Where(comment => comment.ParentId is { } parentId && nodes.ContainsKey(parentId))
            .ToDictionary(comment => comment.Id, comment => comment.ParentId!.Value);

        var roots = new List<CommentNode>();
        foreach (var comment in comments) {
            var node = nodes[comment.Id];
            if (!parents.TryGetValue(comment.Id, out var parentId)) {
                roots.Add(node);
                continue;
            }

            // 楼中楼只留两层：孙辈以下全部拍到顶层留言下面，缩进不再加深，
            // 谁回谁交给「回复 @某人」说明
            var rootId = RootOf(comment.Id, parents);
            nodes[rootId].Replies.Add(node);
            if (parentId != rootId) {
                node.ReplyToId = parentId;
                node.ReplyToName = nodes[parentId].AuthorName;
            }
        }

        return roots;
    }

    public async Task<Comment> AddAsync(CommentSubmission submission, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(submission);

        var comment = new Comment {
            MomentId = submission.MomentId,
            ParentId = submission.ParentId,
            AuthorId = submission.AuthorId,
            AuthorName = submission.AuthorName.Trim(),
            AuthorMail = Trim(submission.AuthorMail),
            AuthorUrl = Trim(submission.AuthorUrl),
            Content = submission.Content.Trim(),
            VisitorHash = submission.VisitorHash,
            IsApproved = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _ = db.Comments.Add(comment);
        _ = await db.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<PagedList<Comment>> ListForAdminAsync(int page, int pageSize, CancellationToken cancellationToken = default) {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var total = await db.Comments.CountAsync(cancellationToken);
        var items = await db.Comments
            .OrderByDescending(comment => comment.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(comment => comment.Moment)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedList<Comment>(items, page, pageSize, total);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        db.Comments.CountAsync(cancellationToken);

    public async Task<bool> SetApprovedAsync(int id, bool approved, CancellationToken cancellationToken = default) {
        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (comment is null) {
            return false;
        }

        comment.IsApproved = approved;
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) {
        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (comment is null) {
            return false;
        }

        // 外键是 Restrict，所以先把挂在它下面的回复摘下来变成顶层
        var replies = await db.Comments.Where(item => item.ParentId == id).ToListAsync(cancellationToken);
        foreach (var reply in replies) {
            reply.ParentId = null;
        }

        _ = db.Comments.Remove(comment);
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    #region 私有方法

    private CommentNode ToNode(Comment comment, SiteSettings site, int? momentAuthorId) {
        var name = DisplayName(comment, site);
        return new CommentNode {
            Id = comment.Id,
            AuthorName = name,
            AuthorUrl = comment.AuthorUrl,
            ContentHtml = ToHtml(comment.Content),
            CreatedAt = clock.ToLocal(comment.CreatedAt),
            IsOwner = comment.AuthorId is not null,
            IsAuthor = comment.AuthorId is { } id && id == momentAuthorId,
            AvatarUrl = comment.Author is null ? string.Empty : site.RoleAvatar(comment.Author.Role),
            AvatarTone = Tone(name)
        };
    }

    /// <summary>沿着父链一直往上，找到这条回复属于哪条顶层留言。</summary>
    private static int RootOf(int id, Dictionary<int, int> parents) {
        // 数据里理论上不会出现环，真出了也只是走一圈就停下，不至于把页面转死
        var walked = new HashSet<int>();
        var current = id;
        while (walked.Add(current) && parents.TryGetValue(current, out var parentId)) {
            current = parentId;
        }

        return current;
    }

    /// <summary>按称呼算一个稳定的配色编号，改用随机数会导致同一个人每次刷新换个颜色。</summary>
    private static int Tone(string name) {
        var sum = 0;
        foreach (var character in name) {
            sum = ((sum * 31) + character) & 0xFFFF;
        }

        return (sum % 6) + 1;
    }

    private static string ToHtml(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var paragraph in paragraphs) {
            _ = builder.Append("<p>")
                .Append(WebUtility.HtmlEncode(paragraph).Replace("\n", "<br>", StringComparison.Ordinal))
                .Append("</p>");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 自己人的留言按站点设置里的称呼显示，改了名字连旧留言一起跟着变；
    /// 访客没有账号，只能用当时填的名字。
    /// </summary>
    private static string DisplayName(Comment comment, SiteSettings site) =>
        comment.Author is null ? comment.AuthorName : site.RoleName(comment.Author.Role);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    #endregion
}
