// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Data;
using OurStory.Services.Anniversaries;
using OurStory.Services.HeartPoints;
using OurStory.Services.Moments;
using OurStory.Services.Shop;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 站点里的哪些动作该惊动对方
/// </summary>
/// <remarks>
/// 这里只看「有没有排进队列、发给谁、写了什么」。真正发出去是通知服务的事，
/// 队列换成了一份只记账的替身
/// </remarks>
public class NotificationTriggerTests {
    [Fact]
    public async Task 发布点点滴滴会通知对方() {
        using var db = TestDoubles.Database(nameof(发布点点滴滴会通知对方));
        var queue = TestDoubles.Notifications();
        var moments = Moments(db, queue);

        _ = await moments.CreateAsync(Draft("第一次一起看海", MomentStatus.Published), authorId: 1);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.Moment, request.Topic);
        Assert.Equal(1, request.ExceptUserId);
        Assert.Contains("第一次一起看海", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 存成草稿不会惊动任何人() {
        using var db = TestDoubles.Database(nameof(存成草稿不会惊动任何人));
        var queue = TestDoubles.Notifications();

        _ = await Moments(db, queue).CreateAsync(Draft("还没写完", MomentStatus.Draft), authorId: 1);

        Assert.Empty(queue.Sent);
    }

    [Fact]
    public async Task 草稿转正才通知改错别字不会() {
        using var db = TestDoubles.Database(nameof(草稿转正才通知改错别字不会));
        var queue = TestDoubles.Notifications();
        var moments = Moments(db, queue);

        var moment = await moments.CreateAsync(Draft("慢慢写", MomentStatus.Draft), authorId: 1);
        _ = await moments.UpdateAsync(moment.Id, Draft("慢慢写", MomentStatus.Published));
        _ = await moments.UpdateAsync(moment.Id, Draft("慢慢写完了", MomentStatus.Published));

        _ = Assert.Single(queue.Sent);
    }

    [Fact]
    public async Task 上锁的记录不把内容摊在锁屏上() {
        using var db = TestDoubles.Database(nameof(上锁的记录不把内容摊在锁屏上));
        var queue = TestDoubles.Notifications();

        var draft = Draft("只有我们知道", MomentStatus.Published);
        draft.Content = "这段话不该出现在通知里";
        draft.Password = "520";

        _ = await Moments(db, queue).CreateAsync(draft, authorId: 1);

        var body = Assert.Single(queue.Sent).Message.Body;
        Assert.Contains("只有我们知道", body, StringComparison.Ordinal);
        Assert.DoesNotContain("这段话不该出现在通知里", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 记下纪念日会通知对方并说还有几天() {
        using var db = TestDoubles.Database(nameof(记下纪念日会通知对方并说还有几天));
        var queue = TestDoubles.Notifications();

        _ = await Anniversaries(db, queue).CreateAsync(
            new AnniversaryEditModel {
                Title = "我们的第一次旅行",
                AnniversaryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                RepeatYearly = true
            },
            authorId: 2);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.Anniversary, request.Topic);
        Assert.Equal(2, request.ExceptUserId);
        Assert.Contains("就是今天", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 上架心愿会通知对方() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();

        var result = await Shop(harness, queue).PublishAsync(
            new ShopPublishModel { Title = "洗碗一次", Price = 10, ListingDays = 7, ValidDays = 7 },
            boyId);

        Assert.True(result.Success);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.Shop, request.Topic);

        // 还没人兑换，只知道「不是发布者」，所以走的是排除自己那一条路
        Assert.Equal(boyId, request.ExceptUserId);
        Assert.Contains("洗碗一次", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 兑换之后通知发布者本人() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var queue = TestDoubles.Notifications();
        var shop = Shop(harness, queue);

        // 兑换要真的扣得起心意，所以这里用会记账的那份实现
        _ = harness.Db.HeartPointEntries.Add(new HeartPointEntry {
            UserId = girlId,
            ChangeAmount = 100,
            Reason = HeartPointReason.DailyHeartbeat,
            SourceKey = "seed:notification",
            Note = "测试进项"
        });
        _ = await harness.Db.SaveChangesAsync();

        _ = await shop.PublishAsync(
            new ShopPublishModel { Title = "陪我看电影", Price = 10, ListingDays = 7, ValidDays = 7 },
            boyId);

        var itemId = harness.Db.ShopItems.Single().Id;
        var result = await shop.PurchaseAsync(itemId, girlId);

        Assert.True(result.Success);

        var request = queue.Sent[^1];
        Assert.Equal(boyId, request.TargetUserId);
        Assert.Contains("陪我看电影", request.Message.Body, StringComparison.Ordinal);
    }

    #region 私有方法

    private static MomentService Moments(OurStoryDbContext db, NotificationQueueSpy queue) =>
        new(db, new SettingsStub(), TestDoubles.Markdown(), TestDoubles.NoPoints(), queue, TestDoubles.Atmosphere(), TestDoubles.Clock());

    private static AnniversaryService Anniversaries(OurStoryDbContext db, NotificationQueueSpy queue) =>
        new(db, TestDoubles.Clock(), TestDoubles.Markdown(), TestDoubles.NoPoints(), queue, new SettingsStub());

    private static ShopService Shop(SqliteHarness harness, NotificationQueueSpy queue) =>
        new(
            harness.Db,
            new SettingsStub(),
            new HeartPointService(harness.Db, new SettingsStub(), TestDoubles.Clock()),
            queue,
            TestDoubles.Clock());

    private static MomentEditModel Draft(string title, MomentStatus status) => new() {
        Title = title,
        Content = "今天的一点小事",
        Status = status,
        MomentDate = DateTime.UtcNow
    };

    #endregion
}
