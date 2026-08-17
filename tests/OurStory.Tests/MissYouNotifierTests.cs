// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Services.Notifications;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 「想你」的攒单：连着点很多下，对方只该收到一条
/// </summary>
public class MissYouNotifierTests {
    /// <summary>
    /// 测试里把半分钟的窗口缩到这么短，跑得快又不至于误判
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(120);

    [Fact]
    public async Task 点一下等窗口过去才发出来() {
        var queue = TestDoubles.Notifications();
        using var notifier = new MissYouNotifier(queue, Window);

        notifier.Record(1, "男主", 1);

        // 窗口还没到，不该有任何动静
        Assert.Empty(queue.Sent);

        await WaitAsync(queue);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.MissYou, request.Topic);
        Assert.Equal(1, request.ExceptUserId);
        Assert.Equal("男主想你了", request.Message.Title);
        Assert.Contains("一下", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 连着点只合成一条并报出总次数() {
        var queue = TestDoubles.Notifications();
        using var notifier = new MissYouNotifier(queue, Window);

        // 首页那颗心可以连着按，一次十几下很正常
        for (var round = 0; round < 5; round++) {
            notifier.Record(1, "男主", 3);
            await Task.Delay(30);
        }

        await WaitAsync(queue);

        var request = Assert.Single(queue.Sent);
        Assert.Contains("15", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 每来一次点击都把计时拨回起点() {
        var queue = TestDoubles.Notifications();
        using var notifier = new MissYouNotifier(queue, Window);

        notifier.Record(1, "男主", 1);
        await Task.Delay(80);

        // 窗口还差一点就到了，这时候又点了一下，就得从头再等
        notifier.Record(1, "男主", 1);
        await Task.Delay(80);
        Assert.Empty(queue.Sent);

        await WaitAsync(queue);
        _ = Assert.Single(queue.Sent);
    }

    [Fact]
    public async Task 停手之后再点是新的一条() {
        var queue = TestDoubles.Notifications();
        using var notifier = new MissYouNotifier(queue, Window);

        notifier.Record(1, "男主", 2);
        await WaitAsync(queue);

        notifier.Record(1, "男主", 1);
        await WaitAsync(queue, expected: 2);

        Assert.Equal(2, queue.Sent.Count);
        Assert.Contains("2 下", queue.Sent[0].Message.Body, StringComparison.Ordinal);
        Assert.Contains("一下", queue.Sent[1].Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 两个人各攒各的() {
        var queue = TestDoubles.Notifications();
        using var notifier = new MissYouNotifier(queue, Window);

        notifier.Record(1, "男主", 2);
        notifier.Record(2, "女主", 7);

        await WaitAsync(queue, expected: 2);

        Assert.Contains(queue.Sent, item => item.ExceptUserId == 1 && item.Message.Title == "男主想你了");
        Assert.Contains(queue.Sent, item => item.ExceptUserId == 2 && item.Message.Title == "女主想你了");
    }

    [Fact]
    public void 没点就不会凭空发一条() {
        var queue = TestDoubles.Notifications();
        using var notifier = new MissYouNotifier(queue, Window);

        notifier.Record(1, "男主", 0);

        Assert.Empty(queue.Sent);
    }

    /// <summary>
    /// 等到攒单器把东西吐出来；计时器有几毫秒抖动，所以轮询而不是死等一个时长
    /// </summary>
    private static async Task WaitAsync(NotificationQueueSpy queue, int expected = 1) {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (queue.Sent.Count < expected && DateTime.UtcNow < deadline) {
            await Task.Delay(20);
        }
    }
}
