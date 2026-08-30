// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Settings;

namespace OurStory.Services.Cycles;

internal sealed class CycleService(
    OurStoryDbContext db,
    SiteClock clock,
    ISettingsService settings,
    ICycleAnalysisService analysis,
    ICycleInsightService insight,
    CycleAnalysisOptions options,
    CycleWriteCoordinator writes) : ICycleService {
    public async Task<CycleDashboard> GetDashboardAsync(
        int userId,
        int page,
        int pageSize,
        int year,
        int month,
        CancellationToken cancellationToken = default) {
        var relationshipId = await RequireRelationshipAsync(userId, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var selectedMonth = SafeMonth(year, month, clock.Today);

        var records = await RecordsAsync(relationshipId, cancellationToken);
        var total = records.Count;
        var pageRecords = records
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var projection = await ProjectAsync(
            relationshipId,
            records,
            Span(selectedMonth, records),
            cancellationToken);

        return new CycleDashboard(
            projection.Current(),
            projection.Statistics,
            projection.Calendar(selectedMonth),
            new PagedList<CycleRecordItem>(
                [.. pageRecords.Select(projection.Item)],
                page,
                pageSize,
                total));
    }

    public async Task<CycleCalendarMonth> GetCalendarAsync(
        int userId,
        int year,
        int month,
        CancellationToken cancellationToken = default) {
        var relationshipId = await RequireRelationshipAsync(userId, cancellationToken);
        var selectedMonth = SafeMonth(year, month, clock.Today);
        var records = await RecordsAsync(relationshipId, cancellationToken);
        var projection = await ProjectAsync(relationshipId, records, Span(selectedMonth, records), cancellationToken);
        return projection.Calendar(selectedMonth);
    }

    public async Task<string> GetHomeStatusAsync(int userId, CancellationToken cancellationToken = default) {
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return "仅双方可查看";
        }

        var facts = await FactsAsync(relationshipId.Value, cancellationToken);
        if (facts.Count == 0) {
            return "共同记录第一封花信";
        }

        var statistics = analysis.Analyze(facts, clock.Today);
        if (facts.Any(item => item.EndDate is null)) {
            var active = facts.Single(item => item.EndDate is null);
            return $"本次经期第 {Math.Max(1, clock.Today.DayNumber - active.StartDate.DayNumber + 1)} 天";
        }

        if (statistics.NextPrediction is not { } prediction) {
            return "继续共同记录，周期参考会更准确";
        }

        var until = prediction.ExpectedStart.DayNumber - clock.Today.DayNumber;
        return until switch {
            > 0 => $"预计 {until} 天后到来",
            0 => "预计日期为今天",
            _ => $"已超过预计日期 {-until} 天"
        };
    }

    public async Task<CycleWriteResult> StartAsync(
        int userId,
        string requestKey,
        bool confirmSuspicious,
        CancellationToken cancellationToken = default) =>
        await CreateAsync(
            userId,
            new CycleRecordSubmission(clock.Today, null, string.Empty, requestKey, confirmSuspicious),
            cancellationToken);

    public async Task<CycleWriteResult> EndAsync(int userId, CancellationToken cancellationToken = default) {
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return Forbidden();
        }

        await using var lease = await writes.EnterAsync(relationshipId.Value, cancellationToken);
        var active = await db.CycleRecords.SingleOrDefaultAsync(
            item => item.RelationshipId == relationshipId && item.EndDate == null,
            cancellationToken);
        if (active is null) {
            return Conflict("当前没有正在进行的记录。");
        }

        if (clock.Today < active.StartDate) {
            return Invalid("结束日期不能早于开始日期。");
        }

        active.EndDate = clock.Today;
        Touch(active, userId);
        _ = await db.SaveChangesAsync(cancellationToken);
        return new(CycleWriteStatus.Saved, "本次花信已完整记录，双方可以随时查看。", active.Id);
    }

    public async Task<CycleWriteResult> CreateAsync(
        int userId,
        CycleRecordSubmission submission,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(submission);
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return Forbidden();
        }

        await using var lease = await writes.EnterAsync(relationshipId.Value, cancellationToken);
        if (!ValidRequestKey(submission.RequestKey)) {
            return Invalid("请求已失效，请刷新页面后重试。");
        }

        if (await WasProcessedAsync(relationshipId.Value, submission.RequestKey, cancellationToken)) {
            return new(CycleWriteStatus.AlreadyProcessed, "本次登记已保存，无需重复提交。");
        }

        if (await HasActiveAsync(relationshipId.Value, cancellationToken)) {
            return Conflict("已有一条正在进行的记录，请先登记结束日期。");
        }

        if (Validate(submission.StartDate, submission.EndDate, submission.Note, options.MaximumNoteLength) is { } invalid) {
            return invalid;
        }

        var facts = await FactsAsync(relationshipId.Value, cancellationToken);
        if (!submission.ConfirmSuspicious
            && Doubt(facts, submission.StartDate, submission.EndDate) is { } warning) {
            return new(CycleWriteStatus.RequiresConfirmation, warning);
        }

        var now = SiteClock.UtcNow;
        var record = new CycleRecord {
            RelationshipId = relationshipId.Value,
            StartDate = submission.StartDate,
            EndDate = submission.EndDate,
            Note = Normalize(submission.Note, options.MaximumNoteLength),
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            RequestKey = submission.RequestKey,
            CreatedAt = now,
            UpdatedAt = now
        };
        _ = db.CycleRecords.Add(record);

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
            return new(
                CycleWriteStatus.Saved,
                submission.EndDate is null
                    ? "开始日期已记下，接下来的变化也可以由两个人共同补充。"
                    : "完整记录已保存，双方可以随时查看。",
                record.Id);
        } catch (DbUpdateException) {
            db.Entry(record).State = EntityState.Detached;
            return Conflict("另一方刚刚完成了相同操作，请刷新后查看。");
        }
    }

    public async Task<CycleWriteResult> UpdateAsync(
        int userId,
        int recordId,
        DateOnly startDate,
        DateOnly? endDate,
        string note,
        CancellationToken cancellationToken = default) {
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return Forbidden();
        }

        await using var lease = await writes.EnterAsync(relationshipId.Value, cancellationToken);
        var record = await db.CycleRecords.SingleOrDefaultAsync(
            item => item.Id == recordId && item.RelationshipId == relationshipId,
            cancellationToken);
        if (record is null) {
            return NotFound();
        }

        if (Validate(startDate, endDate, note, options.MaximumNoteLength) is { } invalid) {
            return invalid;
        }

        if (endDate is null
            && await db.CycleRecords.AnyAsync(
                item => item.RelationshipId == relationshipId && item.EndDate == null && item.Id != recordId,
                cancellationToken)) {
            return Conflict("已有另一条正在进行的记录，不能同时保留两条未结束记录。");
        }

        var others = await db.CycleRecords
            .Where(item => item.RelationshipId == relationshipId && item.Id != recordId)
            .Select(item => new CycleFact(item.StartDate, item.EndDate))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (others.Any(item => Overlaps(startDate, endDate ?? clock.Today, item.StartDate, item.EndDate ?? clock.Today))) {
            return Conflict("调整后的日期与另一条记录重叠，请检查后重新提交。");
        }

        record.StartDate = startDate;
        record.EndDate = endDate;
        record.Note = Normalize(note, options.MaximumNoteLength);
        Touch(record, userId);
        _ = await db.SaveChangesAsync(cancellationToken);
        return new(CycleWriteStatus.Saved, "记录已更新，双方看到的内容会保持一致。", record.Id);
    }

    public async Task<CycleWriteResult> DeleteAsync(
        int userId,
        int recordId,
        CancellationToken cancellationToken = default) {
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return Forbidden();
        }

        await using var lease = await writes.EnterAsync(relationshipId.Value, cancellationToken);
        var record = await db.CycleRecords.SingleOrDefaultAsync(
            item => item.Id == recordId && item.RelationshipId == relationshipId,
            cancellationToken);
        if (record is null) {
            return NotFound();
        }

        _ = db.CycleRecords.Remove(record);
        _ = await db.SaveChangesAsync(cancellationToken);

        return new(CycleWriteStatus.Saved, "记录已删除；对应日期的身体状况仍会为双方保留。", null);
    }

    public async Task<CycleWriteResult> SaveDayAsync(
        int userId,
        CycleDaySubmission submission,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(submission);
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return Forbidden();
        }

        if (submission.Date > clock.Today) {
            return Invalid("不能补充未来日期的记录。");
        }

        var note = Normalize(submission.Note, options.MaximumDayNoteLength);
        if (note.Length > options.MaximumDayNoteLength) {
            return Invalid($"这一天的补充说明不能超过 {options.MaximumDayNoteLength} 个字。");
        }

        await using var lease = await writes.EnterAsync(relationshipId.Value, cancellationToken);
        var log = await db.CycleDailyLogs.SingleOrDefaultAsync(
            item => item.RelationshipId == relationshipId && item.Date == submission.Date,
            cancellationToken);
        var now = SiteClock.UtcNow;
        var empty = submission.Flow == CycleFlow.Unset
            && submission.Mood == CycleMood.Unset
            && submission.Pain <= 0
            && submission.Symptoms == CycleSymptom.None
            && note.Length == 0;

        if (log is null && !empty) {
            _ = db.CycleDailyLogs.Add(new CycleDailyLog {
                RelationshipId = relationshipId.Value,
                Date = submission.Date,
                Flow = submission.Flow,
                Mood = submission.Mood,
                Pain = Math.Clamp(submission.Pain, 0, 3),
                Symptoms = submission.Symptoms,
                Note = note,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
        } else if (log is not null && empty) {
            _ = db.CycleDailyLogs.Remove(log);
        } else if (log is not null) {
            log.Flow = submission.Flow;
            log.Mood = submission.Mood;
            log.Pain = Math.Clamp(submission.Pain, 0, 3);
            log.Symptoms = submission.Symptoms;
            log.Note = note;
            log.UpdatedByUserId = userId;
            log.UpdatedAt = now;
        }

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException) {
            return Conflict("另一方刚刚更新了这一天，请刷新页面后查看最新内容。");
        }

        return new CycleWriteResult(CycleWriteStatus.Saved, $"{submission.Date:M 月 d 日}的状态已记下，双方都可以继续补充。");
    }

    public async Task<CycleNarrativeContext?> LatestNarrativeAsync(
        int userId,
        CancellationToken cancellationToken = default) {
        var relationshipId = await FindRelationshipAsync(userId, cancellationToken);
        if (relationshipId is null) {
            return null;
        }

        var records = await RecordsAsync(relationshipId.Value, cancellationToken);
        if (records.Count == 0) {
            return null;
        }

        var projection = await ProjectAsync(
            relationshipId.Value,
            records,
            Span(clock.Today, records),
            cancellationToken);
        return projection.Context(records[^1]);
    }

    public async Task<int> RefreshSummariesAsync(int limit, CancellationToken cancellationToken = default) {
        if (!insight.UsesModel || limit <= 0) {
            return 0;
        }

        var relationshipIds = await db.CycleRecords
            .Select(item => item.RelationshipId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var written = 0;

        foreach (var relationshipId in relationshipIds) {
            if (written >= limit) {
                break;
            }

            written += await RefreshOneAsync(relationshipId, limit - written, cancellationToken);
        }

        return written;
    }

    #region 私有方法

    private async Task<int> RefreshOneAsync(int relationshipId, int limit, CancellationToken cancellationToken) {
        var records = await RecordsAsync(relationshipId, cancellationToken);

        var candidates = records
            .Where(item => item.EndDate is not null)
            .OrderByDescending(item => item.StartDate)
            .ToArray();
        if (candidates.Length == 0) {
            return 0;
        }

        var projection = await ProjectAsync(relationshipId, records, Span(clock.Today, records), cancellationToken);
        var written = 0;

        foreach (var record in candidates) {
            if (written >= limit) {
                break;
            }

            var context = projection.Context(record);
            var stamp = CycleNarrative.Stamp(context);
            if (record.SummarySource == CycleSummarySource.Model
                && record.Summary.Length > 0
                && record.SummaryStamp == stamp) {
                continue;
            }

            var summary = await insight.WriteAsync(context, cancellationToken);
            if (!summary.FromModel) {
                continue;
            }

            var tracked = await db.CycleRecords.SingleOrDefaultAsync(
                item => item.Id == record.Id,
                cancellationToken);
            if (tracked is null) {
                continue;
            }

            tracked.Summary = summary.Text;
            tracked.SummarySource = CycleSummarySource.Model;
            tracked.SummaryStamp = stamp;
            tracked.SummaryUpdatedAt = summary.UpdatedAt;
            written++;
        }

        if (written > 0) {
            _ = await db.SaveChangesAsync(cancellationToken);
        }

        return written;
    }

    private async Task<CycleProjection> ProjectAsync(
        int relationshipId,
        IReadOnlyList<CycleRecord> records,
        (DateOnly From, DateOnly To) span,
        CancellationToken cancellationToken) {
        var logs = await db.CycleDailyLogs
            .Where(item => item.RelationshipId == relationshipId && item.Date >= span.From && item.Date <= span.To)
            .Include(item => item.UpdatedByUser)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var site = await settings.GetAsync(cancellationToken);
        return new CycleProjection(analysis, options, site, clock.Today, records, logs);
    }

    private Task<List<CycleRecord>> RecordsAsync(int relationshipId, CancellationToken cancellationToken) =>
        db.CycleRecords
            .Where(item => item.RelationshipId == relationshipId)
            .Include(item => item.CreatedByUser)
            .Include(item => item.UpdatedByUser)
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    // 小结的事实指纹覆盖目标周期之前的历史，因此每日记录需要一次取全，
    // 否则同一条记录在不同页面算出的指纹会不一致，已保存的模型小结将无法命中。
    private (DateOnly From, DateOnly To) Span(DateOnly month, List<CycleRecord> records) {
        var first = new DateOnly(month.Year, month.Month, 1);
        var from = first.AddDays(-(7 + options.MaximumPeriodDays));
        var to = first.AddMonths(1).AddDays(7 + options.MaximumPeriodDays);

        if (records.Count > 0) {
            var earliest = records.Min(item => item.StartDate);
            var latest = records.Max(item => item.EndDate ?? clock.Today);
            from = from < earliest ? from : earliest;
            to = to > latest ? to : latest;
        }

        return (from, to);
    }

    private CycleWriteResult? Validate(DateOnly startDate, DateOnly? endDate, string? note, int noteLimit) {
        if (endDate is { } end) {
            if (startDate > end) {
                return Invalid("结束日期不能早于开始日期。");
            }

            if (end.DayNumber - startDate.DayNumber + 1 > options.MaximumPeriodDays) {
                return Invalid($"单次记录不能超过 {options.MaximumPeriodDays} 天，请检查日期。");
            }
        }

        if ((endDate ?? startDate) > clock.Today) {
            return Invalid("不能登记未来日期。");
        }

        return Normalize(note, noteLimit + 1).Length > noteLimit
            ? Invalid($"备注不能超过 {noteLimit} 个字。")
            : null;
    }

    private string? Doubt(IReadOnlyList<CycleFact> facts, DateOnly startDate, DateOnly? endDate) {
        var end = endDate ?? startDate;
        if (facts.Any(item => Overlaps(startDate, end, item.StartDate, item.EndDate ?? clock.Today))) {
            return "所填日期与已有记录重叠，可能是双方重复登记。";
        }

        return analysis.IsSuspiciousStart(facts, startDate, out var reason) ? reason : null;
    }

    private async Task<int> RequireRelationshipAsync(int userId, CancellationToken cancellationToken) =>
        await FindRelationshipAsync(userId, cancellationToken)
        ?? throw new UnauthorizedAccessException("花信如期仅对当前情侣关系中的双方开放。");

    private async Task<int?> FindRelationshipAsync(int userId, CancellationToken cancellationToken) =>
        await db.Users
            .Where(user => user.Id == userId
                && user.IsActive
                && user.CoupleRelationshipId != null
                && user.CoupleRelationship!.IsActive
                && (user.Role == UserRole.Boy || user.Role == UserRole.Girl))
            .Select(user => user.CoupleRelationshipId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<List<CycleFact>> FactsAsync(int relationshipId, CancellationToken cancellationToken) =>
        await db.CycleRecords
            .Where(item => item.RelationshipId == relationshipId)
            .OrderBy(item => item.StartDate)
            .Select(item => new CycleFact(item.StartDate, item.EndDate))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    private Task<bool> HasActiveAsync(int relationshipId, CancellationToken cancellationToken) =>
        db.CycleRecords.AnyAsync(item => item.RelationshipId == relationshipId && item.EndDate == null, cancellationToken);

    private Task<bool> WasProcessedAsync(int relationshipId, string requestKey, CancellationToken cancellationToken) =>
        db.CycleRecords.AnyAsync(item => item.RelationshipId == relationshipId && item.RequestKey == requestKey, cancellationToken);

    private static void Touch(CycleRecord record, int userId) {
        record.UpdatedByUserId = userId;
        record.UpdatedAt = SiteClock.UtcNow;
        record.SummaryStamp = string.Empty;
    }

    private static DateOnly SafeMonth(int year, int month, DateOnly fallback) =>
        year is >= 1900 and <= 2200 && month is >= 1 and <= 12
            ? new DateOnly(year, month, 1)
            : new DateOnly(fallback.Year, fallback.Month, 1);

    private static bool Overlaps(DateOnly firstStart, DateOnly firstEnd, DateOnly secondStart, DateOnly secondEnd) =>
        firstStart <= secondEnd && secondStart <= firstEnd;

    private static bool ValidRequestKey(string? requestKey) => Guid.TryParse(requestKey, out _);

    private static string Normalize(string? note, int limit) {
        var trimmed = (note ?? string.Empty).Trim();
        return trimmed.Length <= limit ? trimmed : trimmed[..limit];
    }

    private static CycleWriteResult Forbidden() => new(CycleWriteStatus.Forbidden, "此操作仅对当前情侣关系中的双方开放。");

    private static CycleWriteResult NotFound() =>
        new(CycleWriteStatus.NotFound, "未找到该记录，或该记录不属于当前情侣关系。");

    private static CycleWriteResult Conflict(string message) => new(CycleWriteStatus.Conflict, message);

    private static CycleWriteResult Invalid(string message) => new(CycleWriteStatus.Invalid, message);

    #endregion
}
