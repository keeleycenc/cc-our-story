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
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var matched = completed.Count(item => item.Answers.Select(answer => answer.OptionIndex).Distinct().Count() == 1);
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
        int optionIndex,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default) {
        if (role is not (UserRole.Boy or UserRole.Girl)) {
            return AffinitySubmitResult.Forbidden;
        }

        var daily = await db.AffinityDailyQuestions
            .Include(item => item.Answers)
            .FirstOrDefaultAsync(item => item.Id == dailyQuestionId && item.Day == clock.TodayKey, cancellationToken);
        if (daily is null) {
            return AffinitySubmitResult.InvalidQuestion;
        }

        var options = ReadOptions(daily.OptionsJson);
        if (daily.Type != AffinityQuestionType.SingleChoice || optionIndex < 0 || optionIndex >= options.Length) {
            return AffinitySubmitResult.InvalidOption;
        }

        if (daily.Answers.Any(answer => answer.Role == role || answer.UserId == userId)) {
            return AffinitySubmitResult.AlreadyAnswered;
        }

        var partnerHasAnswered = daily.Answers.Any(answer => answer.Role != role);
        var answer = new AffinityAnswer {
            DailyQuestionId = daily.Id,
            UserId = userId,
            Role = role,
            OptionIndex = optionIndex,
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
                item.RewardPoints,
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
            item.RewardPoints,
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
        if (model.Type != AffinityQuestionType.SingleChoice
            || text.Length is < 2 or > 300
            || !AffinityQuestionCategories.Contains(category)
            || options.Count is < 2 or > 8
            || model.RewardPoints is < HeartPointRules.MinAffinityReward or > HeartPointRules.MaxReward
            || creatorRole is null) {
            throw new ArgumentException("题干、分类、选项或奖励不符合要求。", nameof(model));
        }

        var now = SiteClock.UtcNow;
        var question = new AffinityQuestion {
            Text = text,
            Category = category,
            Type = model.Type,
            RewardPoints = model.RewardPoints,
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

        var site = await settings.GetAsync(cancellationToken);
        return new AffinityQuestionCard(
            question.Id,
            question.Category,
            question.Type,
            question.IsActive,
            question.IsSealed,
            question.Options.Count,
            0,
            question.RewardPoints,
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
        var existing = await DailyQuery().FirstOrDefaultAsync(item => item.Day == day, cancellationToken);
        if (existing is not null) {
            return existing;
        }

        var question = await db.AffinityQuestions
            .Where(item => item.IsActive && item.IsSealed && item.Options.Count >= 2 && item.DailyQuestions.Count == 0)
            .Include(item => item.Options)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (question is null) {
            return null;
        }
        var options = question.Options.OrderBy(item => item.SortOrder).Select(item => item.Text).ToArray();
        var daily = new AffinityDailyQuestion {
            Day = day,
            QuestionId = question.Id,
            QuestionText = question.Text,
            Category = question.Category,
            Type = question.Type,
            OptionsJson = JsonSerializer.Serialize(options),
            RewardPoints = question.RewardPoints,
            CreatedAt = SiteClock.UtcNow
        };
        _ = db.AffinityDailyQuestions.Add(daily);

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException) {
            db.Entry(daily).State = EntityState.Detached;
            return await DailyQuery().FirstAsync(item => item.Day == day, cancellationToken);
        }

        return daily;
    }

    private IQueryable<AffinityDailyQuestion> DailyQuery() => db.AffinityDailyQuestions
        .Include(item => item.Answers)
        .AsNoTracking();

    private AffinityToday ToToday(AffinityDailyQuestion daily, UserRole role) {
        var mine = daily.Answers.FirstOrDefault(answer => answer.Role == role);
        var partner = daily.Answers.FirstOrDefault(answer => answer.Role != role);
        var revealed = mine is not null && partner is not null;
        return new AffinityToday(
            daily.Id,
            daily.Day,
            daily.QuestionText,
            daily.Category,
            daily.Type,
            ReadOptions(daily.OptionsJson),
            daily.RewardPoints,
            mine?.OptionIndex,
            mine is null ? null : clock.ToLocal(mine.AnsweredAt),
            revealed ? partner!.OptionIndex : null,
            revealed ? clock.ToLocal(partner!.AnsweredAt) : null);
    }

    private AffinityHistoryItem ToHistory(AffinityDailyQuestion daily, UserRole role) {
        var options = ReadOptions(daily.OptionsJson);
        var mine = daily.Answers.Single(answer => answer.Role == role);
        var partner = daily.Answers.Single(answer => answer.Role != role);
        return new AffinityHistoryItem(
            daily.Day,
            daily.QuestionText,
            daily.Category,
            daily.Type,
            Option(options, mine.OptionIndex),
            clock.ToLocal(mine.AnsweredAt),
            Option(options, partner.OptionIndex),
            clock.ToLocal(partner.AnsweredAt),
            daily.RewardPoints,
            mine.OptionIndex == partner.OptionIndex);
    }

    private static string[] ReadOptions(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string Option(string[] options, int index) =>
        index >= 0 && index < options.Length ? options[index] : "（选项已失效）";

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
