// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using OurStory.Services.HeartPoints;
using Xunit;

namespace OurStory.Tests;

public class AffinityServiceTests {
    [Fact]
    public async Task 一方作答后另一方仍看不到答案直到双方完成() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var service = Service(harness, queue);
        _ = await service.CreateQuestionAsync(Question(), boyId);

        var initial = await service.GetDashboardAsync(boyId, UserRole.Boy);
        var daily = Assert.IsType<AffinityToday>(initial.Today);

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 1, boyId, UserRole.Boy));

        var boyWaiting = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(boyId, UserRole.Boy)).Today);
        var girlWaiting = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(girlId, UserRole.Girl)).Today);
        Assert.Equal(1, boyWaiting.MyOptionIndex);
        Assert.NotNull(boyWaiting.MyAnsweredAt);
        Assert.Null(boyWaiting.PartnerOptionIndex);
        Assert.Null(boyWaiting.PartnerAnsweredAt);
        Assert.Null(girlWaiting.MyOptionIndex);
        Assert.Null(girlWaiting.PartnerOptionIndex);

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 1, girlId, UserRole.Girl));

        var revealed = await service.GetDashboardAsync(boyId, UserRole.Boy);
        Assert.True(revealed.Today!.IsRevealed);
        Assert.True(revealed.Today.IsMatch);
        Assert.NotNull(revealed.Today.PartnerAnsweredAt);
        Assert.Equal(1, revealed.Stats.AnsweredDays);
        Assert.Equal(1, revealed.Stats.RevealedDays);
        Assert.Equal(1, revealed.Stats.MatchedDays);
        Assert.Equal(100, revealed.Stats.MatchRate);
        Assert.Single(revealed.History.Items);
        Assert.Equal(2, queue.Sent.Count);
        Assert.All(queue.Sent, request => Assert.Equal(NotificationTopic.Affinity, request.Topic));
    }

    [Fact]
    public async Task 每人每天只能提交一次且非法选项不会入库() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        _ = await service.CreateQuestionAsync(Question(), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        Assert.Equal(AffinitySubmitResult.InvalidOption, await service.SubmitAsync(daily.DailyQuestionId, 99, boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 0, boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.AlreadyAnswered, await service.SubmitAsync(daily.DailyQuestionId, 1, boyId, UserRole.Boy));
        Assert.Equal(1, await harness.Db.AffinityAnswers.CountAsync());
    }

    [Fact]
    public async Task 创建后只返回封存元数据且没有内容管理入口() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        var created = await service.CreateQuestionAsync(Question(), boyId);

        var card = Assert.Single(await service.GetSealedQuestionsAsync());
        Assert.Equal(created.Id, card.Id);
        Assert.True(card.IsSealed);
        Assert.Equal(3, card.OptionCount);
        Assert.Equal(7, card.RewardPoints);
        Assert.Equal("男主", card.CreatorName);
        Assert.Null(typeof(AffinityQuestionCard).GetProperty("Text"));
        Assert.Null(typeof(AffinityQuestionCard).GetProperty("Options"));
        Assert.DoesNotContain(typeof(IAffinityService).GetMethods(), method =>
            method.Name.Contains("Delete", StringComparison.Ordinal)
            || method.Name.Contains("Update", StringComparison.Ordinal)
            || method.Name.Contains("GetQuestion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 每道题按快照奖励一次心意() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var points = Points(harness);
        var service = Service(harness, heartPoints: points);
        _ = await service.CreateQuestionAsync(Question(), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        Assert.Equal(7, daily.RewardPoints);
        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 0, boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.AlreadyAnswered, await service.SubmitAsync(daily.DailyQuestionId, 0, boyId, UserRole.Boy));
        Assert.Equal(7, await points.GetBalanceAsync(boyId));

        var entry = await harness.Db.HeartPointEntries.AsNoTracking().SingleAsync();
        Assert.Equal(HeartPointReason.AffinityAnswer, entry.Reason);
    }

    [Theory]
    [InlineData(new string[] { "2026-08-18", "2026-08-19", "2026-08-20" }, 3)]
    [InlineData(new string[] { "2026-08-17", "2026-08-19" }, 1)]
    [InlineData(new string[] { "2026-08-17", "2026-08-18", "2026-08-19" }, 3)]
    [InlineData(new string[0], 0)]
    public void 连续答题允许今天尚未作答但不能中断(string[] days, int expected) {
        Assert.Equal(expected, AffinityService.CurrentStreak(days, new DateOnly(2026, 8, 20)));
    }

    [Fact]
    public async Task 已经成为每日题的题目不会再次使用() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        var first = await service.CreateQuestionAsync(Question(), boyId);
        var second = await service.CreateQuestionAsync(Question(text: "周末最想去哪里？"), boyId);

        _ = harness.Db.AffinityDailyQuestions.Add(new() {
            Day = "2026-08-19",
            QuestionId = first.Id,
            QuestionText = "已使用题目",
            Category = "日常",
            Type = AffinityQuestionType.SingleChoice,
            OptionsJson = "[\"A\",\"B\"]",
            RewardPoints = 7
        });
        _ = await harness.Db.SaveChangesAsync();

        var today = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(boyId, UserRole.Boy)).Today);
        Assert.Equal(second.Id, await harness.Db.AffinityDailyQuestions
            .Where(item => item.Id == today.DailyQuestionId)
            .Select(item => item.QuestionId!.Value)
            .SingleAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task 答题奖励必须在一到一百之间(int reward) {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateQuestionAsync(Question(reward: reward), boyId));
    }

    [Fact]
    public async Task 访客不能获取或提交答题内容() {
        await using var harness = SqliteHarness.Create();
        var service = Service(harness);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetDashboardAsync(0, UserRole.Guest));
        Assert.Equal(AffinitySubmitResult.Forbidden, await service.SubmitAsync(1, 0, 0, UserRole.Guest));
    }

    private static AffinityService Service(
        SqliteHarness harness,
        NotificationQueueSpy? queue = null,
        IHeartPointService? heartPoints = null) =>
        new(harness.Db, TestDoubles.Clock(), queue ?? TestDoubles.Notifications(), heartPoints ?? TestDoubles.NoPoints(), new SettingsStub());

    private static HeartPointService Points(SqliteHarness harness) =>
        new(harness.Db, new SettingsStub(), TestDoubles.Clock());

    private static AffinityQuestionCreateModel Question(
        string text = "今晚最想一起做什么？",
        int reward = 7) => new() {
        Text = text,
        Category = "日常",
        Type = AffinityQuestionType.SingleChoice,
        Options = ["散步", "看电影", "吃夜宵"],
        RewardPoints = reward
    };
}
