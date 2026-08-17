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
using OurStory.Services.HeartPoints;
using OurStory.Services.Moments;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;

namespace OurStory.Services.Anniversaries;

internal class AnniversaryService(
    OurStoryDbContext db,
    SiteClock clock,
    IMarkdownRenderer markdown,
    IHeartPointService heartPoints,
    INotificationQueue notifications,
    ISettingsService settings) : IAnniversaryService {
    public async Task<IReadOnlyList<AnniversaryOccurrence>> GetForViewerAsync(bool isOwner, CancellationToken cancellationToken = default) {
        var query = isOwner ? db.Anniversaries : db.Anniversaries.Where(item => !item.IsPrivate);
        var items = await query.Include(item => item.Author).AsNoTracking().ToListAsync(cancellationToken);
        var site = await settings.GetAsync(cancellationToken);
        return Sort(items.Select(item => ToOccurrence(item, site)));
    }

    public async Task<IReadOnlyList<AnniversaryOccurrence>> GetAllAsync(CancellationToken cancellationToken = default) {
        var items = await db.Anniversaries.Include(item => item.Author).AsNoTracking().ToListAsync(cancellationToken);
        var site = await settings.GetAsync(cancellationToken);
        return Sort(items.Select(item => ToOccurrence(item, site)));
    }

    public Task<Anniversary?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Anniversaries.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<int> CountForViewerAsync(bool isOwner, CancellationToken cancellationToken = default) =>
        isOwner
            ? db.Anniversaries.CountAsync(cancellationToken)
            : db.Anniversaries.CountAsync(item => !item.IsPrivate, cancellationToken);

    public async Task<AnniversaryOccurrence?> GetOccurrenceAsync(int id, bool isOwner, CancellationToken cancellationToken = default) {
        var query = isOwner ? db.Anniversaries : db.Anniversaries.Where(item => !item.IsPrivate);
        var item = await query.Include(value => value.Author).AsNoTracking().FirstOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) {
            return null;
        }

        var site = await settings.GetAsync(cancellationToken);
        return ToOccurrence(item, site);
    }

    public async Task<Anniversary> CreateAsync(AnniversaryEditModel model, int? authorId, CancellationToken cancellationToken = default) {
        var now = SiteClock.UtcNow;
        var item = new Anniversary { AuthorId = authorId, CreatedAt = now, UpdatedAt = now };
        Copy(model, item);
        _ = db.Anniversaries.Add(item);
        _ = await db.SaveChangesAsync(cancellationToken);

        if (authorId is { } id) {
            _ = await heartPoints.AwardDailyAsync(id, HeartPointReason.AnniversaryPublished, clock.TodayKey, cancellationToken);
        }

        await NotifyAsync(item, authorId, cancellationToken);
        return item;
    }

    public async Task<bool> UpdateAsync(int id, AnniversaryEditModel model, CancellationToken cancellationToken = default) {
        var item = await db.Anniversaries.FirstOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (item is null) {
            return false;
        }

        Copy(model, item);
        item.UpdatedAt = SiteClock.UtcNow;
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) {
        var deleted = await db.Anniversaries.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    #region 私有方法

    private async Task NotifyAsync(Anniversary item, int? authorId, CancellationToken cancellationToken) {
        var site = await settings.GetAsync(cancellationToken);
        var occurrence = AnniversaryTimeline.Calculate(item, clock.Today);

        var when = occurrence.DaysUntil switch {
            null => $"{occurrence.SolarDateText}，已经过去了",
            0 => "就是今天",
            1 => "就在明天",
            { } days => $"还有 {days} 天"
        };

        var author = authorId is { } id
            ? site.RoleName(await db.Users.Where(user => user.Id == id).Select(user => user.Role).FirstOrDefaultAsync(cancellationToken))
            : "我们";

        _ = notifications.Enqueue(NotificationRequest.ToPartner(
            NotificationTopic.Anniversary,
            authorId ?? 0,
            new PushMessage(
                $"{author}记下了一个日子",
                $"{item.Title} · {when}",
                occurrence.Url,
                $"anniversary-{item.Id}")));
    }

    private static IReadOnlyList<AnniversaryOccurrence> Sort(IEnumerable<AnniversaryOccurrence> items) =>
        [.. items.OrderBy(item => item.IsArchived)
            .ThenBy(item => item.DaysUntil ?? int.MaxValue)
            .ThenBy(item => item.OriginalDate)
            .ThenBy(item => item.Id)];

    private AnniversaryOccurrence ToOccurrence(Anniversary item, SiteSettings site) {
        if (item.NoteHtml.Length == 0 && !string.IsNullOrWhiteSpace(item.Note)) {
            item.NoteHtml = markdown.ToHtml(item.Note);
            item.CoverUrl ??= NullIfEmpty(HtmlText.FirstImage(item.NoteHtml));
        }

        var authorName = item.Author is null ? "我们" : site.RoleName(item.Author.Role);
        return AnniversaryTimeline.Calculate(item, clock.Today, authorName);
    }

    private void Copy(AnniversaryEditModel model, Anniversary item) {
        item.Title = model.Title.Trim();
        item.AnniversaryDate = model.AnniversaryDate;
        item.CalendarType = model.CalendarType;
        item.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        item.NoteHtml = markdown.ToHtml(item.Note);
        item.CoverUrl = Trim(model.CoverUrl) ?? NullIfEmpty(HtmlText.FirstImage(item.NoteHtml));
        item.Kind = model.Kind;
        item.RepeatYearly = model.RepeatYearly;
        item.IsPrivate = model.IsPrivate;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    #endregion
}
