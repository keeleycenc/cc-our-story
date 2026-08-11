// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Text;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Settings;

namespace OurStory.Services.Moments;

internal class MomentService(
    OurStoryDbContext db,
    ISettingsService settings,
    IMarkdownRenderer markdown,
    SiteClock clock) : IMomentService {    
    public async Task<PagedList<MomentCard>> GetPageAsync(int page, MomentViewer viewer, CancellationToken cancellationToken = default) {
        var site = await settings.GetAsync(cancellationToken);
        var pageSize = site.MomentsPageSize;
        page = page < 1 ? 1 : page;

        var query = Visible(viewer);
        var total = await query.CountAsync(cancellationToken);

        var moments = await query
            .OrderByDescending(moment => moment.MomentDate)
            .ThenByDescending(moment => moment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(moment => moment.Author)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var counts = await CommentCountsAsync(moments, cancellationToken);
        var cards = moments.Select(moment => ToCard(moment, viewer, site, counts)).ToList();

        return new PagedList<MomentCard>(cards, page, pageSize, total);
    }

    public async Task<IReadOnlyList<MomentCard>> GetLatestAsync(int count, MomentViewer viewer, CancellationToken cancellationToken = default) {
        var site = await settings.GetAsync(cancellationToken);

        var moments = await Visible(viewer)
            .OrderByDescending(moment => moment.MomentDate)
            .ThenByDescending(moment => moment.Id)
            .Take(count)
            .Include(moment => moment.Author)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var counts = await CommentCountsAsync(moments, cancellationToken);
        return [.. moments.Select(moment => ToCard(moment, viewer, site, counts))];
    }

    public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
        db.Moments.CountAsync(moment => moment.Status == MomentStatus.Published, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        db.Moments.CountAsync(cancellationToken);

    public async Task<MomentDetail?> GetDetailAsync(string slug, MomentViewer viewer, CancellationToken cancellationToken = default) {
        var moment = await Visible(viewer)
            .Include(item => item.Author)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == slug, cancellationToken);

        if (moment is null) {
            return null;
        }

        var site = await settings.GetAsync(cancellationToken);
        var locked = viewer.IsLockedFor(moment);
        var localDate = clock.ToLocal(moment.MomentDate);

        var previous = await Visible(viewer)
            .Where(item => item.MomentDate < moment.MomentDate)
            .OrderByDescending(item => item.MomentDate)
            .Select(item => new MomentLink(item.Title, item.Slug))
            .FirstOrDefaultAsync(cancellationToken);

        var next = await Visible(viewer)
            .Where(item => item.MomentDate > moment.MomentDate)
            .OrderBy(item => item.MomentDate)
            .Select(item => new MomentLink(item.Title, item.Slug))
            .FirstOrDefaultAsync(cancellationToken);

        var commentCount = await db.Comments
            .CountAsync(comment => comment.MomentId == moment.Id && comment.IsApproved, cancellationToken);

        return new MomentDetail {
            Id = moment.Id,
            Title = moment.Title,
            Slug = moment.Slug,
            ContentHtml = locked ? string.Empty : moment.ContentHtml,
            Mood = Fallback(moment.Mood, "日常"),
            Location = moment.Location ?? string.Empty,
            MomentDate = localDate,
            IsLocked = locked,
            AllowComment = moment.AllowComment && !locked,
            AuthorName = AuthorName(moment, site),
            LoveDay = LoveTimeline.ElapsedDays(localDate, site.LoveStartedAt),
            CommentCount = commentCount,
            Previous = previous,
            Next = next
        };
    }

    public async Task<int?> UnlockAsync(string slug, string password, CancellationToken cancellationToken = default) {
        var moment = await db.Moments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == slug, cancellationToken);

        if (moment is null || !moment.IsProtected) {
            return null;
        }

        // 访问密码是给自己人用的暗号，不是账号口令，比对明文即可
        return string.Equals(moment.Password, password, StringComparison.Ordinal) ? moment.Id : null;
    }

    public async Task<PagedList<Moment>> ListForAdminAsync(int page, int pageSize, int? authorId = null, CancellationToken cancellationToken = default) {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var query = authorId is { } id
            ? db.Moments.Where(moment => moment.AuthorId == id)
            : db.Moments;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(moment => moment.MomentDate)
            .ThenByDescending(moment => moment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(moment => moment.Author)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedList<Moment>(items, page, pageSize, total);
    }

    public Task<Moment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Moments.AsNoTracking().FirstOrDefaultAsync(moment => moment.Id == id, cancellationToken);

    public async Task<Moment> CreateAsync(MomentEditModel model, int authorId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);

        var moment = new Moment {
            AuthorId = authorId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await ApplyAsync(moment, model, cancellationToken);
        _ = db.Moments.Add(moment);
        _ = await db.SaveChangesAsync(cancellationToken);
        return moment;
    }

    public async Task<bool> UpdateAsync(int id, MomentEditModel model, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);

        var moment = await db.Moments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (moment is null) {
            return false;
        }

        await ApplyAsync(moment, model, cancellationToken);
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) {
        var moment = await db.Moments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (moment is null) {
            return false;
        }

        // 留言之间还有父子关系，外键设成了 Restrict，所以先按层级从下往上删
        var comments = await db.Comments.Where(comment => comment.MomentId == id).ToListAsync(cancellationToken);
        db.Comments.RemoveRange(comments.Where(comment => comment.ParentId is not null));
        db.Comments.RemoveRange(comments.Where(comment => comment.ParentId is null));
        _ = db.Moments.Remove(moment);

        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    #region 私有方法

    private async Task ApplyAsync(Moment moment, MomentEditModel model, CancellationToken cancellationToken) {
        moment.Title = model.Title.Trim();
        moment.Content = model.Content ?? string.Empty;
        moment.ContentHtml = markdown.ToHtml(moment.Content);
        moment.Mood = Trim(model.Mood);
        moment.Location = Trim(model.Location);
        moment.Password = Trim(model.Password);
        moment.Status = model.Status;
        moment.AllowComment = model.AllowComment;
        moment.MomentDate = clock.ToUtc(model.MomentDate);
        moment.UpdatedAt = DateTimeOffset.UtcNow;

        moment.Summary = Trim(model.Summary) ?? HtmlText.Excerpt(moment.ContentHtml, 140);
        moment.CoverUrl = Trim(model.CoverUrl) ?? NullIfEmpty(HtmlText.FirstImage(moment.ContentHtml));

        var desired = SlugFactory.Normalize(model.Slug);
        if (desired.Length == 0) {
            desired = SlugFactory.FromTitle(moment.Title, model.MomentDate);
        }

        if (!string.Equals(desired, moment.Slug, StringComparison.Ordinal)) {
            moment.Slug = await UniqueSlugAsync(desired, moment.Id, cancellationToken);
        }
    }

    private async Task<string> UniqueSlugAsync(string desired, int selfId, CancellationToken cancellationToken) {
        var candidate = desired;
        var suffix = 2;

        while (await db.Moments.AnyAsync(moment => moment.Slug == candidate && moment.Id != selfId, cancellationToken)) {
            candidate = $"{desired}-{suffix++}";
        }

        return candidate;
    }

    private IQueryable<Moment> Visible(MomentViewer viewer) =>
        viewer.IsOwner
            ? db.Moments
            : db.Moments.Where(moment => moment.Status == MomentStatus.Published);

    private async Task<Dictionary<int, int>> CommentCountsAsync(List<Moment> moments, CancellationToken cancellationToken) {
        if (moments.Count == 0) {
            return [];
        }

        var ids = moments.Select(moment => moment.Id).ToList();
        return await db.Comments
            .Where(comment => ids.Contains(comment.MomentId) && comment.IsApproved)
            .GroupBy(comment => comment.MomentId)
            .Select(group => new { MomentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.MomentId, item => item.Count, cancellationToken);
    }

    private MomentCard ToCard(Moment moment, MomentViewer viewer, SiteSettings site, Dictionary<int, int> commentCounts) {
        var locked = viewer.IsLockedFor(moment);
        var localDate = clock.ToLocal(moment.MomentDate);

        return new MomentCard {
            Id = moment.Id,
            Title = moment.Title,
            Slug = moment.Slug,
            // 上锁的记录连摘要和封面都不能露，否则等于没锁
            Excerpt = locked ? "这段回忆上了锁，输入密码才能打开。" : moment.Summary ?? string.Empty,
            CoverUrl = locked ? string.Empty : moment.CoverUrl ?? string.Empty,
            Mood = Fallback(moment.Mood, "日常"),
            Location = moment.Location ?? string.Empty,
            MomentDate = localDate,
            IsLocked = locked,
            AuthorName = AuthorName(moment, site),
            CommentCount = commentCounts.GetValueOrDefault(moment.Id),
            LoveDay = LoveTimeline.ElapsedDays(localDate, site.LoveStartedAt)
        };
    }

    /// <summary>页面上的称呼只有站点设置这一个出处，账号自己不再单独存名字。</summary>
    private static string AuthorName(Moment moment, SiteSettings site) =>
        moment.Author is null ? "我们" : site.RoleName(moment.Author.Role);

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    #endregion
}
