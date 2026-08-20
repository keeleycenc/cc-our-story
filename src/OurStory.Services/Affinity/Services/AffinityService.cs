// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.Notifications;
using System.Security.Cryptography;
using System.Text.Json;

namespace OurStory.Services.Affinity;

internal sealed class AffinityService(
    OurStoryDbContext db,
    SiteClock clock,
    INotificationQueue notifications) : IAffinityService {
    private const int RecentQuestionWindow = 7;

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

        var history = historyEntities.Select(item => ToHistory(item, role)).ToList();
        return new AffinityDashboard(today, new AffinityStats(total, matched), new PagedList<AffinityHistoryItem>(history, page, pageSize, total));
    }

    public async Task<string> GetTodayStatusAsync(int userId, UserRole role, CancellationToken cancellationToken = default) {
        if (role is not (UserRole.Boy or UserRole.Girl)) {
            return "仅两个人可参与";
        }

        var daily = await GetOrCreateTodayAsync(cancellationToken);
        if (daily is null) {
            return "等待添加题目";
        }

        var mine = daily.Answers.Any(answer => answer.Role == role);
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
        if (optionIndex < 0 || optionIndex >= options.Length) {
            return AffinitySubmitResult.InvalidOption;
        }

        if (daily.Answers.Any(answer => answer.Role == role || answer.UserId == userId)) {
            return AffinitySubmitResult.AlreadyAnswered;
        }

        var partnerHasAnswered = daily.Answers.Any(answer => answer.Role != role);
        _ = db.AffinityAnswers.Add(new AffinityAnswer {
            DailyQuestionId = daily.Id,
            UserId = userId,
            Role = role,
            OptionIndex = optionIndex,
            AnsweredAt = SiteClock.UtcNow
        });

        try {
            _ = await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException) {
            return AffinitySubmitResult.AlreadyAnswered;
        }

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

    public async Task<IReadOnlyList<AffinityQuestionCard>> GetQuestionsAsync(CancellationToken cancellationToken = default) {
        var questions = await db.AffinityQuestions
            .Include(item => item.Options)
            .Include(item => item.DailyQuestions)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return [.. questions.Select(ToCard)];
    }

    public async Task<AffinityQuestionCard?> GetQuestionAsync(int id, CancellationToken cancellationToken = default) {
        var question = await db.AffinityQuestions
            .Include(item => item.Options)
            .Include(item => item.DailyQuestions)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return question is null ? null : ToCard(question);
    }

    public async Task<AffinityQuestionCard> SaveQuestionAsync(
        int? id,
        AffinityQuestionEditModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        var text = model.Text.Trim();
        var category = model.Category.Trim();
        var options = NormalizeOptions(model.Options);
        if (text.Length is < 2 or > 300 || category.Length is < 1 or > 30 || options.Count is < 2 or > 8) {
            throw new ArgumentException("题干、分类或选项不符合要求。", nameof(model));
        }

        AffinityQuestion question;
        if (id is { } questionId) {
            question = await db.AffinityQuestions.Include(item => item.Options)
                .FirstOrDefaultAsync(item => item.Id == questionId, cancellationToken)
                ?? throw new InvalidOperationException("题目不存在。");
            db.AffinityQuestionOptions.RemoveRange(question.Options);
        } else {
            question = new AffinityQuestion { CreatedAt = SiteClock.UtcNow };
            _ = db.AffinityQuestions.Add(question);
        }

        question.Text = text;
        question.Category = category;
        question.IsActive = model.IsActive;
        question.UpdatedAt = SiteClock.UtcNow;
        question.Options = [.. options.Select((option, index) => new AffinityQuestionOption { Text = option, SortOrder = index })];
        _ = await db.SaveChangesAsync(cancellationToken);
        return ToCard(question);
    }

    public async Task<bool> SetQuestionActiveAsync(int id, bool active, CancellationToken cancellationToken = default) {
        var changed = await db.AffinityQuestions.Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsActive, active)
                .SetProperty(item => item.UpdatedAt, SiteClock.UtcNow), cancellationToken);
        return changed > 0;
    }

    public async Task<bool> DeleteQuestionAsync(int id, CancellationToken cancellationToken = default) {
        var question = await db.AffinityQuestions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (question is null) {
            return false;
        }

        _ = db.AffinityQuestions.Remove(question);
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AffinityDailyQuestion?> GetOrCreateTodayAsync(CancellationToken cancellationToken) {
        var day = clock.TodayKey;
        var existing = await DailyQuery().FirstOrDefaultAsync(item => item.Day == day, cancellationToken);
        if (existing is not null) {
            return existing;
        }

        var recentIds = await db.AffinityDailyQuestions
            .Where(item => item.QuestionId != null)
            .OrderByDescending(item => item.Day)
            .Take(RecentQuestionWindow)
            .Select(item => item.QuestionId!.Value)
            .ToListAsync(cancellationToken);

        var candidates = await db.AffinityQuestions
            .Where(item => item.IsActive && item.Options.Count >= 2)
            .Include(item => item.Options)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0) {
            return null;
        }

        var fresh = candidates.Where(item => !recentIds.Contains(item.Id)).ToList();
        var pool = fresh.Count > 0 ? fresh : candidates;
        var question = pool[RandomNumberGenerator.GetInt32(pool.Count)];
        var options = question.Options.OrderBy(item => item.SortOrder).Select(item => item.Text).ToArray();
        var daily = new AffinityDailyQuestion {
            Day = day,
            QuestionId = question.Id,
            QuestionText = question.Text,
            Category = question.Category,
            OptionsJson = JsonSerializer.Serialize(options),
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

    private static AffinityToday ToToday(AffinityDailyQuestion daily, UserRole role) {
        var mine = daily.Answers.FirstOrDefault(answer => answer.Role == role);
        var partner = daily.Answers.FirstOrDefault(answer => answer.Role != role);
        var revealed = mine is not null && partner is not null;
        return new AffinityToday(
            daily.Id,
            daily.Day,
            daily.QuestionText,
            daily.Category,
            ReadOptions(daily.OptionsJson),
            mine?.OptionIndex,
            revealed ? partner!.OptionIndex : null);
    }

    private static AffinityHistoryItem ToHistory(AffinityDailyQuestion daily, UserRole role) {
        var options = ReadOptions(daily.OptionsJson);
        var mine = daily.Answers.Single(answer => answer.Role == role);
        var partner = daily.Answers.Single(answer => answer.Role != role);
        return new AffinityHistoryItem(
            daily.Day,
            daily.QuestionText,
            daily.Category,
            Option(options, mine.OptionIndex),
            Option(options, partner.OptionIndex),
            mine.OptionIndex == partner.OptionIndex);
    }

    private static AffinityQuestionCard ToCard(AffinityQuestion question) => new(
        question.Id,
        question.Text,
        question.Category,
        question.IsActive,
        [.. question.Options.OrderBy(item => item.SortOrder).Select(item => item.Text)],
        question.DailyQuestions.Count);

    private static string[] ReadOptions(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

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
