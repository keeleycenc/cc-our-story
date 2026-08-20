// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using Xunit;

namespace OurStory.Tests;

public class AffinityServiceTests {
    [Fact]
    public async Task 一方作答后另一方仍看不到答案直到双方完成() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var service = Service(harness, queue);
        _ = await service.SaveQuestionAsync(null, Question());

        var initial = await service.GetDashboardAsync(boyId, UserRole.Boy);
        var daily = Assert.IsType<AffinityToday>(initial.Today);

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 1, boyId, UserRole.Boy));

        var boyWaiting = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(boyId, UserRole.Boy)).Today);
        var girlWaiting = Assert.IsType<AffinityToday>((await service.GetDashboardAsync(girlId, UserRole.Girl)).Today);
        Assert.Equal(1, boyWaiting.MyOptionIndex);
        Assert.Null(boyWaiting.PartnerOptionIndex);
        Assert.Null(girlWaiting.MyOptionIndex);
        Assert.Null(girlWaiting.PartnerOptionIndex);

        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 1, girlId, UserRole.Girl));

        var revealed = await service.GetDashboardAsync(boyId, UserRole.Boy);
        Assert.True(revealed.Today!.IsRevealed);
        Assert.True(revealed.Today.IsMatch);
        Assert.Equal(1, revealed.Stats.AnsweredDays);
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
        _ = await service.SaveQuestionAsync(null, Question());
        var daily = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        Assert.Equal(AffinitySubmitResult.InvalidOption, await service.SubmitAsync(daily.DailyQuestionId, 99, boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.Accepted, await service.SubmitAsync(daily.DailyQuestionId, 0, boyId, UserRole.Boy));
        Assert.Equal(AffinitySubmitResult.AlreadyAnswered, await service.SubmitAsync(daily.DailyQuestionId, 1, boyId, UserRole.Boy));
        Assert.Equal(1, await harness.Db.AffinityAnswers.CountAsync());
    }

    [Fact]
    public async Task 编辑或删除题库题目不会改变每日快照() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var service = Service(harness);
        var question = await service.SaveQuestionAsync(null, Question());
        var before = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;

        _ = await service.SaveQuestionAsync(question.Id, new AffinityQuestionEditModel {
            Text = "完全不同的新题目",
            Category = "未来",
            Options = ["新答案一", "新答案二"],
            IsActive = true
        });
        Assert.True(await service.DeleteQuestionAsync(question.Id));

        var after = (await service.GetDashboardAsync(boyId, UserRole.Boy)).Today!;
        Assert.Equal(before.Question, after.Question);
        Assert.Equal(before.Options, after.Options);
        Assert.Null((await harness.Db.AffinityDailyQuestions.AsNoTracking().SingleAsync()).QuestionId);
    }

    [Fact]
    public async Task 访客不能获取或提交答题内容() {
        await using var harness = SqliteHarness.Create();
        var service = Service(harness);

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetDashboardAsync(0, UserRole.Guest));
        Assert.Equal(AffinitySubmitResult.Forbidden, await service.SubmitAsync(1, 0, 0, UserRole.Guest));
    }

    private static AffinityService Service(SqliteHarness harness, NotificationQueueSpy? queue = null) =>
        new(harness.Db, TestDoubles.Clock(), queue ?? TestDoubles.Notifications());

    private static AffinityQuestionEditModel Question() => new() {
        Text = "今晚最想一起做什么？",
        Category = "日常",
        Options = ["散步", "看电影", "吃夜宵"],
        IsActive = true
    };
}
