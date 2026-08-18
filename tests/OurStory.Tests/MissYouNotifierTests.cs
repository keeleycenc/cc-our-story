// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Services.Notifications;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 「想你」的攒单测试
/// </summary>
public class MissYouNotifierTests {
    /// <summary>攒单的窗口，和站点上跑的那份一致。</summary>
    private static readonly TimeSpan Window = MissYouNotifier.DefaultWindow;

    /// <summary>窗口内的一次点击间隔，怎么点都还在同一条里。</summary>
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(3);

    [Fact]
    public void 点一下等窗口过去才发出来() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();
        using var notifier = new MissYouNotifier(queue, Window, time);

        notifier.Record(1, "男主", 1);

        // 窗口还没到，不该有任何动静
        time.Advance(Window - Gap);
        Assert.Empty(queue.Sent);

        time.Advance(Gap);

        var request = Assert.Single(queue.Sent);
        Assert.Equal(NotificationTopic.MissYou, request.Topic);
        Assert.Equal(1, request.ExceptUserId);
        Assert.Equal("男主想你了", request.Message.Title);
        Assert.Contains("一下", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void 连着点只合成一条并报出总次数() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();
        using var notifier = new MissYouNotifier(queue, Window, time);

        // 首页那颗心可以连着按，一次十几下很正常
        for (var round = 0; round < 5; round++) {
            notifier.Record(1, "男主", 3);
            time.Advance(Gap);
        }

        Assert.Empty(queue.Sent);
        time.Advance(Window);

        var request = Assert.Single(queue.Sent);
        Assert.Contains("15", request.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void 每来一次点击都把计时拨回起点() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();
        using var notifier = new MissYouNotifier(queue, Window, time);

        notifier.Record(1, "男主", 1);
        time.Advance(Window - Gap);

        // 窗口还差一点就到了，这时候又点了一下，就得从头再等
        notifier.Record(1, "男主", 1);
        time.Advance(Window - Gap);
        Assert.Empty(queue.Sent);

        time.Advance(Gap);
        _ = Assert.Single(queue.Sent);
    }

    [Fact]
    public void 停手之后再点是新的一条() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();
        using var notifier = new MissYouNotifier(queue, Window, time);

        notifier.Record(1, "男主", 2);
        time.Advance(Window);

        notifier.Record(1, "男主", 1);
        time.Advance(Window);

        Assert.Equal(2, queue.Sent.Count);
        Assert.Contains("2 下", queue.Sent[0].Message.Body, StringComparison.Ordinal);
        Assert.Contains("一下", queue.Sent[1].Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void 两个人各攒各的() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();
        using var notifier = new MissYouNotifier(queue, Window, time);

        notifier.Record(1, "男主", 2);
        notifier.Record(2, "女主", 7);

        time.Advance(Window);

        Assert.Equal(2, queue.Sent.Count);
        Assert.Contains(queue.Sent, item => item.ExceptUserId == 1 && item.Message.Title == "男主想你了");
        Assert.Contains(queue.Sent, item => item.ExceptUserId == 2 && item.Message.Title == "女主想你了");
    }

    [Fact]
    public void 没点就不会凭空发一条() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();
        using var notifier = new MissYouNotifier(queue, Window, time);

        notifier.Record(1, "男主", 0);
        time.Advance(Window);

        Assert.Empty(queue.Sent);
    }

    [Fact]
    public void 关掉之后还在等的那条就不发了() {
        var queue = TestDoubles.Notifications();
        var time = TestDoubles.Time();

        using (var notifier = new MissYouNotifier(queue, Window, time)) {
            notifier.Record(1, "男主", 4);
        }

        time.Advance(Window);

        Assert.Empty(queue.Sent);
    }
}
