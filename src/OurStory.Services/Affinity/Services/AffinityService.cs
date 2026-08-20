// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.HeartPoints;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;
using System.Text.Json;

namespace OurStory.Services.Affinity;

internal sealed class AffinityService(
    OurStoryDbContext db,
    SiteClock clock,
    INotificationQueue notifications,
    IHeartPointService heartPoints,
    ISettingsService settings) : IAffinityService {
    public async Task<AffinityDashboard> GetDashboardAsync(
        int userId,
        UserRole role,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) {
        EnsureOwner(role);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var todayEntity = await GetOrCreateTodayAsync(cancellationToken);
        var today = todayEntity is null ? null : ToToday(todayEntity, role);

        var answerDays = await db.AffinityAnswers
            .Where(answer => answer.UserId == userId)
            .Select(answer => answer.DailyQuestion!.Day)
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var completed = await db.AffinityDailyQuestions
            .Where(item => item.Answers.Count >= 2)
            .Include(item => item.Answers)
            .Include(item => item.Question)
                .ThenInclude(question => question!.CreatedByUser)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var matched = completed.Count(AnswersMatch);
        var total = completed.Count;
        var historyEntities = completed
            .OrderByDescending(item => item.Day)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var stats = new AffinityStats(answerDays.Count, CurrentStreak(answerDays, clock.Today), total, matched);
        var history = historyEntities.Select(item => ToHistory(item, role)).ToList();
        return new AffinityDashboard(today, stats, new PagedList<AffinityHistoryItem>(history, page, pageSize, total));
    }

    public async Task<string> GetTodayStatusAsync(int userId, UserRole role, CancellationToken cancellationToken = default) {
        if (role is not (UserRole.Boy or UserRole.Girl)) {
            return "仅两个人可参与";
        }

        var daily = await GetOrCreateTodayAsync(cancellationToken);
        if (daily is null) {
            return "等待添加题目";
        }

        var mine = daily.Answers.Any(answer => answer.UserId == userId);
        return daily.Answers.Count >= 2 ? "今日已揭晓" : mine ? "等待 TA" : "今日待回答";
    }

    public async Task<AffinitySubmitResult> SubmitAsync(
        int dailyQuestionId,
        AffinityAnswerSubmission submission,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(submission);
        if (role is not (UserRole.Boy or UserRole.Girl)) {
            return AffinitySubmitResult.Forbidden;
        }

        var daily = await db.AffinityDailyQuestions
            .Include(item => item.Answers)
            .FirstOrDefaultAsync(item => item.Id == dailyQuestionId, cancellationToken);
        if (daily is null) {
            return AffinitySubmitResult.InvalidQuestion;
        }

        if (!string.Equals(daily.Day, clock.TodayKey, StringComparison.Ordinal)) {
            var pendingId = await db.AffinityDailyQuestions
                .Where(item => item.Answers.Count == 1)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (pendingId != daily.Id) {
                return AffinitySubmitResult.InvalidQuestion;
            }
        }

        var options = ReadOptions(daily.OptionsJson);
        var value = NormalizeSubmission(daily.Type, submission, options.Length);
        if (value is null) {
            return AffinitySubmitResult.InvalidAnswer;
        }

        if (daily.Answers.Any(answer => answer.Role == role || answer.UserId == userId)) {
            return AffinitySubmitResult.AlreadyAnswered;
        }

        var partnerHasAnswered = daily.Answers.Any(answer => answer.Role != role);
        var answer = new AffinityAnswer {
            DailyQuestionId = daily.Id,
            UserId = userId,
            Role = role,
            SelectedOptionIndexesJson = JsonSerializer.Serialize(value.SelectedOptionIndexes),
            TextAnswer = value.Text,
            AnsweredAt = SiteClock.UtcNow
        };
        _ = db.AffinityAnswers.Add(answer);

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException) {
            db.Entry(answer).State = EntityState.Detached;
            return AffinitySubmitResult.AlreadyAnswered;
        }

        _ = await heartPoints.AwardOnceAsync(
            userId,
            HeartPointReason.AffinityAnswer,
            $"affinity-answer:{daily.Id}:{userId}",
            daily.RewardPoints,
            $"心有灵犀 · {daily.Day}",
            cancellationToken);

        _ = notifications.Enqueue(NotificationRequest.ToPartner(
            NotificationTopic.Affinity,
            userId,
            partnerHasAnswered
                ? new PushMessage(
                    "今日答案已揭晓",
                    "你们都完成了今天的心有灵犀，来看看有没有想到一起吧。",
                    "/affinity",
                    $"affinity-{daily.Day}")
                : new PushMessage(
                    "TA 回答了今天的问题",
                    "心有灵犀正在等你作答，答案会在两个人都完成后揭晓。",
                    "/affinity",
                    $"affinity-{daily.Day}")));

        return AffinitySubmitResult.Accepted;
    }

    public async Task<IReadOnlyList<AffinityQuestionCard>> GetSealedQuestionsAsync(CancellationToken cancellationToken = default) {
        var questions = await db.AffinityQuestions
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new {
                item.Id,
                item.Category,
                item.Type,
                item.IsActive,
                item.IsSealed,
                OptionCount = item.Options.Count,
                UsedCount = item.DailyQuestions.Count,
                CreatorRole = item.CreatedByUser == null ? (UserRole?)null : item.CreatedByUser.Role,
                item.CreatedAt
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var site = await settings.GetAsync(cancellationToken);
        return [.. questions.Select(item => new AffinityQuestionCard(
            item.Id,
            item.Category,
            item.Type,
            item.IsActive,
            item.IsSealed,
            item.OptionCount,
            item.UsedCount,
            site.RewardAffinity,
            item.CreatorRole is { } role ? site.RoleName(role) : "系统预置",
            clock.ToLocal(item.CreatedAt)))];
    }

    public async Task<AffinityQuestionCard> CreateQuestionAsync(
        AffinityQuestionCreateModel model,
        int creatorUserId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        var text = model.Text.Trim();
        var category = model.Category.Trim();
        var options = NormalizeOptions(model.Options);
        var creatorRole = await db.Users
            .Where(user => user.Id == creatorUserId && user.IsActive)
            .Select(user => (UserRole?)user.Role)
            .SingleOrDefaultAsync(cancellationToken);
        var optionsAreValid = model.Type switch {
            AffinityQuestionType.SingleChoice or AffinityQuestionType.MultipleChoice => options.Count is >= 2 and <= 8,
            AffinityQuestionType.OpenEnded => options.Count == 0,
            _ => false
        };
        if (!optionsAreValid
            || text.Length is < 2 or > 300
            || !AffinityQuestionCategories.Contains(category)
            || creatorRole is null) {
            throw new ArgumentException("题干、分类或选项不符合要求。", nameof(model));
        }

        var site = await settings.GetAsync(cancellationToken);
        var now = SiteClock.UtcNow;
        var question = new AffinityQuestion {
            Text = text,
            Category = category,
            Type = model.Type,
            RewardPoints = site.RewardAffinity,
            IsActive = true,
            IsSealed = true,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Options = [.. options.Select((option, index) => new AffinityQuestionOption {
                Text = option,
                SortOrder = index
            })]
        };
        _ = db.AffinityQuestions.Add(question);
        _ = await db.SaveChangesAsync(cancellationToken);

        return new AffinityQuestionCard(
            question.Id,
            question.Category,
            question.Type,
            question.IsActive,
            question.IsSealed,
            question.Options.Count,
            0,
            site.RewardAffinity,
            site.RoleName(creatorRole.Value),
            clock.ToLocal(question.CreatedAt));
    }

    internal static int CurrentStreak(IEnumerable<string> days, DateOnly today) {
        var answered = days
            .Select(day => DateOnly.TryParse(day, out var parsed) ? parsed : (DateOnly?)null)
            .Where(day => day is not null)
            .Select(day => day!.Value)
            .ToHashSet();

        var cursor = answered.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (answered.Contains(cursor)) {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private async Task<AffinityDailyQuestion?> GetOrCreateTodayAsync(CancellationToken cancellationToken) {
        var day = clock.TodayKey;

        // 一方已经回答的题目不能因跨天被替换；它会一直作为当前题等待另一方完成。
        var pending = await DailyQuery()
            .Where(item => item.Answers.Count == 1)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (pending is not null) {
            return pending;
        }

        // 跨天题在今天由第二个人完成后，当天继续停留在揭晓状态，次日再换新题。
        var todayStart = clock.ToUtc(clock.Today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStart = clock.ToUtc(clock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var completedToday = await DailyQuery()
            .Where(item => item.Answers.Count >= 2 && item.Answers.Any(answer =>
                answer.AnsweredAt >= todayStart && answer.AnsweredAt < tomorrowStart))
            .ToListAsync(cancellationToken);
        var justRevealed = completedToday
            .OrderByDescending(item => item.Answers.Max(answer => answer.AnsweredAt))
            .ThenByDescending(item => item.Id)
            .FirstOrDefault();
        if (justRevealed is not null) {
            return justRevealed;
        }

        var existing = await DailyQuery().FirstOrDefaultAsync(item => item.Day == day, cancellationToken);
        if (existing is not null) {
            return existing;
        }

        var question = await db.AffinityQuestions
            .Where(item => item.IsActive
                && item.IsSealed
                && item.DailyQuestions.Count == 0
                && (item.Type == AffinityQuestionType.OpenEnded && item.Options.Count == 0
                    || item.Type != AffinityQuestionType.OpenEnded && item.Options.Count >= 2))
            .Include(item => item.Options)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (question is null) {
            return null;
        }

        var options = question.Options.OrderBy(item => item.SortOrder).Select(item => item.Text).ToArray();
        var site = await settings.GetAsync(cancellationToken);
        var daily = new AffinityDailyQuestion {
            Day = day,
            LoveDay = LoveTimeline.DayNumber(clock.LocalNow, site.LoveStartedAt),
            QuestionId = question.Id,
            QuestionText = question.Text,
            Category = question.Category,
            Type = question.Type,
            OptionsJson = JsonSerializer.Serialize(options),
            RewardPoints = site.RewardAffinity,
            CreatedAt = SiteClock.UtcNow
        };
        _ = db.AffinityDailyQuestions.Add(daily);

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException) {
            db.Entry(daily).State = EntityState.Detached;
            return await DailyQuery().FirstAsync(item => item.Day == day, cancellationToken);
        }

        return await DailyQuery().FirstAsync(item => item.Id == daily.Id, cancellationToken);
    }

    private IQueryable<AffinityDailyQuestion> DailyQuery() => db.AffinityDailyQuestions
        .Include(item => item.Answers)
        .Include(item => item.Question)
            .ThenInclude(question => question!.CreatedByUser)
        .AsNoTracking();

    private AffinityToday ToToday(AffinityDailyQuestion daily, UserRole role) {
        var mine = daily.Answers.FirstOrDefault(answer => answer.Role == role);
        var partner = daily.Answers.FirstOrDefault(answer => answer.Role != role);
        var revealed = mine is not null && partner is not null;
        var mineValue = mine is null ? null : ToAnswerValue(mine);
        var partnerValue = revealed ? ToAnswerValue(partner!) : null;
        return new AffinityToday(
            daily.Id,
            daily.Day,
            daily.LoveDay,
            daily.Question?.CreatedByUser?.Role,
            daily.QuestionText,
            daily.Category,
            daily.Type,
            ReadOptions(daily.OptionsJson),
            daily.RewardPoints,
            mineValue,
            mine is null ? null : clock.ToLocal(mine.AnsweredAt),
            partnerValue,
            revealed ? clock.ToLocal(partner!.AnsweredAt) : null,
            revealed && AnswerValuesEqual(daily.Type, mineValue!, partnerValue!));
    }

    private AffinityHistoryItem ToHistory(AffinityDailyQuestion daily, UserRole role) {
        var options = ReadOptions(daily.OptionsJson);
        var mine = daily.Answers.Single(answer => answer.Role == role);
        var partner = daily.Answers.Single(answer => answer.Role != role);
        return new AffinityHistoryItem(
            daily.Day,
            daily.LoveDay,
            daily.Question?.CreatedByUser?.Role,
            daily.QuestionText,
            daily.Category,
            daily.Type,
            AnswerText(daily.Type, options, mine),
            clock.ToLocal(mine.AnsweredAt),
            AnswerText(daily.Type, options, partner),
            clock.ToLocal(partner.AnsweredAt),
            daily.RewardPoints,
            AnswerValuesEqual(daily.Type, ToAnswerValue(mine), ToAnswerValue(partner)));
    }

    private static string[] ReadOptions(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string Option(string[] options, int index) =>
        index >= 0 && index < options.Length ? options[index] : "（选项已失效）";

    private static string AnswerText(AffinityQuestionType type, string[] options, AffinityAnswer answer) {
        var value = ToAnswerValue(answer);
        return type == AffinityQuestionType.OpenEnded
            ? value.Text ?? string.Empty
            : string.Join("、", value.SelectedOptionIndexes.Select(index => Option(options, index)));
    }

    private static AffinityAnswerValue ToAnswerValue(AffinityAnswer answer) => new(
        NormalizeSelection(JsonSerializer.Deserialize<int[]>(answer.SelectedOptionIndexesJson) ?? []),
        answer.TextAnswer);

    private static int[] NormalizeSelection(IEnumerable<int> optionIndexes) => [.. optionIndexes.Distinct().Order()];

    private static AffinityAnswerValue? NormalizeSubmission(
        AffinityQuestionType type,
        AffinityAnswerSubmission submission,
        int optionCount) {
        var selected = NormalizeSelection(submission.SelectedOptionIndexes ?? []);
        var text = (submission.Text ?? string.Empty).Trim();
        var optionsAreValid = selected.All(index => index >= 0 && index < optionCount);

        return type switch {
            AffinityQuestionType.SingleChoice when optionsAreValid && selected.Length == 1 && text.Length == 0 =>
                new AffinityAnswerValue(selected, null),
            AffinityQuestionType.MultipleChoice when optionsAreValid && selected.Length > 0 && text.Length == 0 =>
                new AffinityAnswerValue(selected, null),
            AffinityQuestionType.OpenEnded when selected.Length == 0 && text.Length is >= 1 and <= 1000 =>
                new AffinityAnswerValue([], text),
            _ => null
        };
    }

    private static bool AnswerValuesEqual(
        AffinityQuestionType type,
        AffinityAnswerValue first,
        AffinityAnswerValue second) => type switch {
            AffinityQuestionType.SingleChoice or AffinityQuestionType.MultipleChoice =>
                first.SelectedOptionIndexes.SequenceEqual(second.SelectedOptionIndexes),
            AffinityQuestionType.OpenEnded => string.Equals(first.Text, second.Text, StringComparison.Ordinal),
            _ => false
        };

    private static bool AnswersMatch(AffinityDailyQuestion daily) {
        var answers = daily.Answers.Take(2).ToArray();
        return answers.Length == 2
            && AnswerValuesEqual(daily.Type, ToAnswerValue(answers[0]), ToAnswerValue(answers[1]));
    }

    private static List<string> NormalizeOptions(IEnumerable<string> options) => [.. options
        .Select(item => item.Trim())
        .Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Where(item => item.Length <= 120)];

    private static void EnsureOwner(UserRole role) {
        if (role is not (UserRole.Boy or UserRole.Girl)) {
            throw new UnauthorizedAccessException("仅限情侣参与心有灵犀");
        }
    }
}
