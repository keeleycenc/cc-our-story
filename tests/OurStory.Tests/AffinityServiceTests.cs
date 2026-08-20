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
        var site = new SiteSettings {
            BoyName = "男主",
            GirlName = "女主",
            LoveStartedAt = TestDoubles.Clock().LocalNow.Date.AddDays(-19).AddHours(20)
        };
        var service = Service(harness, queue, site: site);
        _ = await service.CreateQuestionAsync(Question(), boyId);

        var initial = await service.GetDashboardAsync(boyId, UserRole.Boy);
        var daily = Assert.IsType<AffinityToday>(initial.Today);
        Assert.Equal(20, daily.LoveDay);
        Assert.Equal(UserRole.Boy, daily.CreatorRole);

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, Selection(1), boyId, UserRole.Boy));

        var boyWaiting = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(boyId, UserRole.Boy)).Today);
        var girlWaiting = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(girlId, UserRole.Girl)).Today);
        Assert.Equal([1], boyWaiting.MyAnswer!.SelectedOptionIndexes);
        _ = Assert.NotNull(boyWaiting.MyAnsweredAt);
        Assert.Null(boyWaiting.PartnerAnswer);
        Assert.Null(boyWaiting.PartnerAnsweredAt);
        Assert.Null(girlWaiting.MyAnswer);
        Assert.Null(girlWaiting.PartnerAnswer);

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, Selection(1), girlId, UserRole.Girl));

        var revealed = await service.GetDashboardAsync(boyId, UserRole.Boy);
        Assert.True(revealed.Today!.IsRevealed);
        Assert.True(revealed.Today.HasSameAnswer);
        _ = Assert.NotNull(revealed.Today.PartnerAnsweredAt);
        Assert.Equal(1, revealed.Stats.TotalAnswers);
        Assert.Equal(1, revealed.Stats.SameChoiceAnswerDays);
        Assert.Equal(1, revealed.Stats.CreatedQuestions);
        var history = Assert.Single(revealed.History.Items);
        Assert.True(history.HasSameAnswer);
        Assert.Equal(20, history.LoveDay);
        Assert.Equal(UserRole.Boy, history.CreatorRole);
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

        Assert.Equal(AffinitySubmitResult.InvalidAnswer, await service.SubmitAsync(daily.DailyQuestionId, Selection(99), boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, Selection(0), boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.AlreadyAnswered, await service.SubmitAsync(daily.DailyQuestionId, Selection(1), boyId, UserRole.Boy));
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
        Assert.Equal(5, card.RewardPoints);
        Assert.Equal("男主", card.CreatorName);
        Assert.Null(typeof(AffinityQuestionCard).GetProperty("Text"));
        Assert.Null(typeof(AffinityQuestionCard).GetProperty("Options"));
        Assert.DoesNotContain(typeof(IAffinityService).GetMethods(), method =>
            method.Name.Contains("Delete", StringComparison.Ordinal)
            || method.Name.Contains("Update", StringComparison.Ordinal)
            || method.Name.Contains("GetQuestion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 统一答题奖励按每日题快照发放一次() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var points = Points(harness);
        var site = new SiteSettings { BoyName = "男主", GirlName = "女主", RewardAffinity = 9 };
        var service = Service(harness, heartPoints: points, site: site);
        _ = await service.CreateQuestionAsync(Question(), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        site.RewardAffinity = 3;
        daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;
        Assert.Equal(9, daily.RewardPoints);
        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, Selection(0), boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.AlreadyAnswered, await service.SubmitAsync(daily.DailyQuestionId, Selection(0), boyId, UserRole.Boy));
        Assert.Equal(9, await points.GetBalanceAsync(boyId));

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

    [Fact]
    public async Task 一方作答的题目跨天后继续等待且完成当天保持揭晓() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        _ = await service.CreateQuestionAsync(Question(), boyId);
        _ = await service.CreateQuestionAsync(Question(text: "第二道题"), boyId);

        var firstDay = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;
        Assert.Equal(
            AffinitySubmitResult.Accepted,
            await service.SubmitAsync(firstDay.DailyQuestionId, Selection(0), boyId, UserRole.Boy));

        var daily = await harness.Db.AffinityDailyQuestions.SingleAsync(item => item.Id == firstDay.DailyQuestionId);
        daily.Day = TestDoubles.Clock().Today.AddDays(-1).ToString("yyyy-MM-dd");
        _ = await harness.Db.SaveChangesAsync();

        var carried = (await service.GetDashboardAsync(girlId, UserRole.Girl)).Today!;
        Assert.Equal(firstDay.DailyQuestionId, carried.DailyQuestionId);
        Assert.Equal(1, await harness.Db.AffinityDailyQuestions.CountAsync());
        Assert.Equal(
            AffinitySubmitResult.Accepted,
            await service.SubmitAsync(carried.DailyQuestionId, Selection(1), girlId, UserRole.Girl));

        var revealed = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;
        Assert.Equal(firstDay.DailyQuestionId, revealed.DailyQuestionId);
        Assert.True(revealed.IsRevealed);
        Assert.Equal(1, await harness.Db.AffinityDailyQuestions.CountAsync());

        var completed = await harness.Db.AffinityDailyQuestions
            .Include(item => item.Answers)
            .SingleAsync(item => item.Id == firstDay.DailyQuestionId);
        foreach (var answer in completed.Answers) {
            answer.AnsweredAt = answer.AnsweredAt.AddDays(-1);
        }

        _ = await harness.Db.SaveChangesAsync();

        var nextDay = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;
        Assert.NotEqual(firstDay.DailyQuestionId, nextDay.DailyQuestionId);
        Assert.Equal(2, await harness.Db.AffinityDailyQuestions.CountAsync());
    }

    [Fact]
    public async Task 空题库不会创建每日题并返回等待添加题目状态() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);

        var dashboard = await service.GetDashboardAsync(boyId, UserRole.Boy);

        Assert.Null(dashboard.Today);
        Assert.Equal("等待添加题目", await service.GetTodayStatusAsync(boyId, UserRole.Boy));
    }

    [Fact]
    public async Task 题库用尽后返回空状态供页面显示回退卡片() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        _ = await service.CreateQuestionAsync(Question(), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;
        _ = await service.SubmitAsync(daily.DailyQuestionId, Selection(0), boyId, UserRole.Boy);
        _ = await service.SubmitAsync(daily.DailyQuestionId, Selection(1), girlId, UserRole.Girl);

        var used = await harness.Db.AffinityDailyQuestions
            .Include(item => item.Answers)
            .SingleAsync(item => item.Id == daily.DailyQuestionId);
        used.Day = TestDoubles.Clock().Today.AddDays(-1).ToString("yyyy-MM-dd");
        foreach (var answer in used.Answers) {
            answer.AnsweredAt = answer.AnsweredAt.AddDays(-1);
        }

        _ = await harness.Db.SaveChangesAsync();

        Assert.Null((await service.GetDashboardAsync(boyId, UserRole.Boy)).Today);
        Assert.Equal("等待添加题目", await service.GetTodayStatusAsync(boyId, UserRole.Boy));
    }

    [Fact]
    public async Task 多选答案忽略选择顺序并按完整集合统计相同答案() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        _ = await service.CreateQuestionAsync(Question(type: AffinityQuestionType.MultipleChoice), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, Selection(0, 2), boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, Selection(2, 0), girlId, UserRole.Girl));

        var dashboard = await service.GetDashboardAsync(boyId, UserRole.Boy);
        Assert.True(dashboard.Today!.HasSameAnswer);
        Assert.Equal(1, dashboard.Stats.SameChoiceAnswerDays);
        Assert.Equal([0, 2], dashboard.Today.MyAnswer!.SelectedOptionIndexes);
        Assert.Equal("散步、吃夜宵", Assert.Single(dashboard.History.Items).MyAnswer);
    }

    [Fact]
    public async Task 单选题不能同时提交多个选项() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        _ = await service.CreateQuestionAsync(Question(), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        Assert.Equal(AffinitySubmitResult.InvalidAnswer,
            await service.SubmitAsync(daily.DailyQuestionId, Selection(0, 1), boyId, UserRole.Boy));
        Assert.Empty(await harness.Db.AffinityAnswers.ToListAsync());
    }

    [Fact]
    public async Task 开放题使用文字答案并在双方完成后揭晓() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        _ = await service.CreateQuestionAsync(Question(type: AffinityQuestionType.OpenEnded), boyId);
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        Assert.Empty(daily.Options);
        Assert.Equal(AffinitySubmitResult.InvalidAnswer,
            await service.SubmitAsync(daily.DailyQuestionId, Text("   "), boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.Accepted,
            await service.SubmitAsync(daily.DailyQuestionId, Text("雨天窝在沙发看电影"), boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.Accepted,
            await service.SubmitAsync(daily.DailyQuestionId, Text("雨天窝在沙发看电影"), girlId, UserRole.Girl));

        var dashboard = await service.GetDashboardAsync(boyId, UserRole.Boy);
        Assert.True(dashboard.Today!.IsRevealed);
        Assert.True(dashboard.Today.HasSameAnswer);
        Assert.Equal(0, dashboard.Stats.SameChoiceAnswerDays);
        Assert.Equal("雨天窝在沙发看电影", dashboard.Today.MyAnswer!.Text);
        Assert.Equal("雨天窝在沙发看电影", Assert.Single(dashboard.History.Items).PartnerAnswer);
    }

    [Fact]
    public async Task 访客不能获取或提交答题内容() {
        await using var harness = SqliteHarness.Create();
        var service = Service(harness);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetDashboardAsync(0, UserRole.Guest));
        Assert.Equal(AffinitySubmitResult.Forbidden, await service.SubmitAsync(1, Selection(0), 0, UserRole.Guest));
    }

    private static AffinityService Service(
        SqliteHarness harness,
        NotificationQueueSpy? queue = null,
        IHeartPointService? heartPoints = null,
        SiteSettings? site = null) =>
        new(harness.Db, TestDoubles.Clock(), queue ?? TestDoubles.Notifications(), heartPoints ?? TestDoubles.NoPoints(), new SettingsStub(site));

    private static HeartPointService Points(SqliteHarness harness) =>
        new(harness.Db, new SettingsStub(), TestDoubles.Clock());

    private static AffinityAnswerSubmission Selection(params int[] indexes) => new(indexes, null);

    private static AffinityAnswerSubmission Text(string text) => new([], text);

    private static AffinityQuestionCreateModel Question(
        string text = "今晚最想一起做什么？",
        AffinityQuestionType type = AffinityQuestionType.SingleChoice) => new() {
        Text = text,
        Category = "日常",
        Type = type,
        Options = type == AffinityQuestionType.OpenEnded ? [] : ["散步", "看电影", "吃夜宵"]
    };
}
