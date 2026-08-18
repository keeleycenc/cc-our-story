// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Options;
using OurStory.Data;
using OurStory.Services.Comments;
using OurStory.Services.LlmAtmosphere;
using OurStory.Services.Moments;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 氛围组什么时候该开口、开口之后写下来的是什么
/// </summary>
/// <remarks>
/// 模型换成了照本宣科的替身：这里要测的是「排不排得上队」「该不该拦下来」
/// 「拿回来的话怎么落到评论表里」，不是模型本身写得好不好
/// </remarks>
public class LlmAtmosphereTests {
    private const int MomentId = 1;
    private const int AuthorId = 1;
    private const string MemberId = "warm";

    [Fact]
    public async Task 发布之后氛围组会被招呼一声() {
        using var db = TestDoubles.Database(nameof(发布之后氛围组会被招呼一声));
        var atmosphere = TestDoubles.Atmosphere();

        var moment = await Moments(db, atmosphere).CreateAsync(Draft("第一次一起看海"), AuthorId);

        var published = Assert.Single(atmosphere.Published);
        Assert.Equal(moment.Id, published.MomentId);
        Assert.False(published.IsProtected);
    }

    [Fact]
    public async Task 存成草稿不会惊动氛围组() {
        using var db = TestDoubles.Database(nameof(存成草稿不会惊动氛围组));
        var atmosphere = TestDoubles.Atmosphere();

        _ = await Moments(db, atmosphere).CreateAsync(Draft("还没写完", MomentStatus.Draft), AuthorId);

        Assert.Empty(atmosphere.Published);
    }

    [Fact]
    public async Task 关掉留言的记录不会招呼氛围组() {
        using var db = TestDoubles.Database(nameof(关掉留言的记录不会招呼氛围组));
        var atmosphere = TestDoubles.Atmosphere();

        var draft = Draft("这条不想被评论");
        draft.AllowComment = false;

        _ = await Moments(db, atmosphere).CreateAsync(draft, AuthorId);

        Assert.Empty(atmosphere.Published);
    }

    [Fact]
    public void 上锁的记录默认不排任何待办() {
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(Options(Member(commentChance: 100))));

        scheduler.OnMomentPublished(MomentId, isProtected: true);

        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public void 后台开了开关之后上锁的记录才排待办() {
        var options = Options(Member(commentChance: 100));
        options.IncludeProtected = true;
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(options));

        scheduler.OnMomentPublished(MomentId, isProtected: true);

        Assert.Equal(1, scheduler.Pending);
    }

    [Fact]
    public void 概率为零的角色从不主动开口() {
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(Options(Member(commentChance: 0))));

        scheduler.OnMomentPublished(MomentId, isProtected: false);

        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public void 总开关关着时谁也不排队() {
        var options = Options(Member(commentChance: 100));
        options.Enabled = false;
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(options));

        scheduler.OnMomentPublished(MomentId, isProtected: false);

        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public void 排上队之后要等到点才取得走() {
        var time = TestDoubles.Time();
        var member = Member(commentChance: 100);
        member.DelayMinMinutes = 10;
        member.DelayMaxMinutes = 10;

        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(Options(member)), time);
        scheduler.OnMomentPublished(MomentId, isProtected: false);

        Assert.Empty(scheduler.TakeDue());

        time.Advance(TimeSpan.FromMinutes(11));
        var due = Assert.Single(scheduler.TakeDue());

        Assert.Equal(LlmAtmosphereTriggerKind.Comment, due.Kind);
        Assert.Equal(MemberId, due.MemberId);
        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public void 同一件事排两次只算一次() {
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(Options(Member())));
        var trigger = new LlmAtmosphereTrigger(
            LlmAtmosphereTriggerKind.Comment,
            MomentId,
            MemberId,
            DateTimeOffset.UtcNow);

        Assert.True(scheduler.Schedule(trigger));
        Assert.False(scheduler.Schedule(trigger with { DueAt = DateTimeOffset.UtcNow.AddHours(1) }));
        Assert.Equal(1, scheduler.Pending);
    }

    [Fact]
    public void 回给别人的话不会惊动氛围组() {
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(Options(Member(replyChance: 100))));

        scheduler.OnCommentAdded(MomentId, commentId: 9, repliedMemberId: null, isProtected: false);

        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public void 回给某个角色的话由它自己接() {
        var scheduler = new LlmAtmosphereScheduler(TestDoubles.Configuration(Options(Member(replyChance: 100))));

        scheduler.OnCommentAdded(MomentId, commentId: 9, MemberId, isProtected: false);

        var due = Assert.Single(scheduler.TakeDue());
        Assert.Equal(LlmAtmosphereTriggerKind.Reply, due.Kind);
        Assert.Equal(9, due.ParentCommentId);
        Assert.Equal(MemberId, due.MemberId);
    }

    [Fact]
    public async Task 有人回复氛围组时会把角色标识带上() {
        using var db = TestDoubles.Database(nameof(有人回复氛围组时会把角色标识带上));
        await SeedAsync(db);

        var atmosphere = TestDoubles.Atmosphere();
        var comments = Comments(db, atmosphere, Options(Member()));

        var mine = await comments.AddAsync(new CommentSubmission {
            MomentId = MomentId,
            AuthorName = "阿柔",
            Content = "今天的海好蓝",
            LlmMemberId = MemberId
        });

        _ = await comments.AddAsync(new CommentSubmission {
            MomentId = MomentId,
            ParentId = mine.Id,
            AuthorId = AuthorId,
            AuthorName = "男主",
            Content = "下次一起去"
        });

        var stirred = Assert.Single(atmosphere.Commented);
        Assert.Equal(MemberId, stirred.RepliedMemberId);
    }

    [Fact]
    public async Task 角色自己留的话不会再招呼一轮() {
        using var db = TestDoubles.Database(nameof(角色自己留的话不会再招呼一轮));
        await SeedAsync(db);

        var atmosphere = TestDoubles.Atmosphere();
        var comments = Comments(db, atmosphere, Options(Member()));

        var human = await comments.AddAsync(new CommentSubmission {
            MomentId = MomentId,
            AuthorId = AuthorId,
            AuthorName = "男主",
            Content = "今天很开心"
        });

        _ = await comments.AddAsync(new CommentSubmission {
            MomentId = MomentId,
            ParentId = human.Id,
            AuthorName = "阿柔",
            Content = "看得出来！",
            LlmMemberId = MemberId
        });

        Assert.Empty(atmosphere.Commented);
    }

    [Fact]
    public async Task 模型写回来的话会落成一条氛围组留言() {
        using var db = TestDoubles.Database(nameof(模型写回来的话会落成一条氛围组留言));
        await SeedAsync(db);

        var client = new ResponsesClientStub(ResponsesResult.Success("这张照片好好看呀"));
        var service = Service(db, client, Options(Member()));

        Assert.True(await service.RunAsync(Trigger()));

        var comment = Assert.Single(db.Comments);
        Assert.Equal("这张照片好好看呀", comment.Content);
        Assert.Equal(MemberId, comment.LlmMemberId);
        Assert.Equal(CommentSource.LlmAtmosphere, comment.Source);
        Assert.Null(comment.AuthorId);
    }

    [Fact]
    public async Task 同一个角色在一条记录下面只开一楼() {
        using var db = TestDoubles.Database(nameof(同一个角色在一条记录下面只开一楼));
        await SeedAsync(db);

        var client = new ResponsesClientStub(
            ResponsesResult.Success("第一句"),
            ResponsesResult.Success("第二句"));

        var service = Service(db, client, Options(Member()));

        Assert.True(await service.RunAsync(Trigger()));
        Assert.False(await service.RunAsync(Trigger()));

        _ = Assert.Single(db.Comments);
        _ = Assert.Single(client.Requests);
    }

    [Fact]
    public async Task 评论区已经够热闹就不再开口() {
        using var db = TestDoubles.Database(nameof(评论区已经够热闹就不再开口));
        await SeedAsync(db);

        _ = db.Comments.Add(new Comment {
            MomentId = MomentId,
            AuthorName = "别人",
            Content = "先来一句",
            LlmMemberId = "someone-else"
        });

        _ = await db.SaveChangesAsync();

        var options = Options(Member());
        options.MaxCommentsPerMoment = 1;

        var client = new ResponsesClientStub(ResponsesResult.Success("我也想说两句"));
        Assert.False(await Service(db, client, options).RunAsync(Trigger()));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task 草稿到期了也不会真的发给模型() {
        using var db = TestDoubles.Database(nameof(草稿到期了也不会真的发给模型));
        await SeedAsync(db, MomentStatus.Draft);

        var client = new ResponsesClientStub(ResponsesResult.Success("不该出现"));
        Assert.False(await Service(db, client, Options(Member())).RunAsync(Trigger()));

        Assert.Empty(client.Requests);
        Assert.Empty(db.Comments);
    }

    [Fact]
    public async Task 上锁的记录到期了默认也不发() {
        using var db = TestDoubles.Database(nameof(上锁的记录到期了默认也不发));
        await SeedAsync(db, password: "520");

        var client = new ResponsesClientStub(ResponsesResult.Success("不该出现"));
        Assert.False(await Service(db, client, Options(Member())).RunAsync(Trigger()));

        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task 带图那次被打回来会脱掉图再试一次() {
        using var db = TestDoubles.Database(nameof(带图那次被打回来会脱掉图再试一次));
        await SeedAsync(db);

        var member = Member();
        member.AllowImages = true;

        var client = new ResponsesClientStub(
            ResponsesResult.Failed(ResponsesFailure.Rejected),
            ResponsesResult.Success("光看文字也知道你们很开心"));

        var service = Service(db, client, Options(member), new MomentImageSourceStub("data:image/webp;base64,AAAA"));

        Assert.True(await service.RunAsync(Trigger()));

        Assert.Equal(2, client.Requests.Count);
        _ = Assert.Single(client.Requests[0].Images);
        Assert.Empty(client.Requests[1].Images);
    }

    [Fact]
    public async Task 限流不会白白再跑一趟() {
        using var db = TestDoubles.Database(nameof(限流不会白白再跑一趟));
        await SeedAsync(db);

        var member = Member();
        member.AllowImages = true;

        var client = new ResponsesClientStub(ResponsesResult.Failed(ResponsesFailure.RateLimited));
        var service = Service(db, client, Options(member), new MomentImageSourceStub("data:image/webp;base64,AAAA"));

        Assert.False(await service.RunAsync(Trigger()));

        _ = Assert.Single(client.Requests);
        Assert.Empty(db.Comments);
    }

    [Fact]
    public async Task 不许看图的角色请求里不带图() {
        using var db = TestDoubles.Database(nameof(不许看图的角色请求里不带图));
        await SeedAsync(db);

        var client = new ResponsesClientStub(ResponsesResult.Success("嗯嗯"));
        var service = Service(db, client, Options(Member()), new MomentImageSourceStub("data:image/webp;base64,AAAA"));

        Assert.True(await service.RunAsync(Trigger()));

        Assert.Empty(Assert.Single(client.Requests).Images);
    }

    [Fact]
    public async Task 角色被删掉之后历史留言照样显示() {
        using var db = TestDoubles.Database(nameof(角色被删掉之后历史留言照样显示));
        await SeedAsync(db);

        _ = db.Comments.Add(new Comment {
            MomentId = MomentId,
            AuthorName = "阿柔",
            Content = "那天真好",
            LlmMemberId = "已经删掉的角色",
            LlmAvatarUrl = "https://example.com/rou.png"
        });

        _ = await db.SaveChangesAsync();

        // 配置里已经没有这个角色了，只剩留言上那份快照
        var tree = await Comments(db, TestDoubles.Atmosphere(), Options()).GetTreeAsync(MomentId);

        var node = Assert.Single(tree);
        Assert.Equal("阿柔", node.AuthorName);
        Assert.Equal("https://example.com/rou.png", node.AvatarUrl);
        Assert.True(node.IsAtmosphere);
        Assert.False(node.IsOwner);
    }

    [Fact]
    public async Task 角色改了名字连旧留言一起跟着变() {
        using var db = TestDoubles.Database(nameof(角色改了名字连旧留言一起跟着变));
        await SeedAsync(db);

        _ = db.Comments.Add(new Comment {
            MomentId = MomentId,
            AuthorName = "阿柔",
            Content = "那天真好",
            LlmMemberId = MemberId,
            LlmAvatarUrl = "https://example.com/old.png"
        });

        _ = await db.SaveChangesAsync();

        var member = Member();
        member.Name = "小柔";
        member.AvatarUrl = "https://example.com/new.png";

        var tree = await Comments(db, TestDoubles.Atmosphere(), Options(member)).GetTreeAsync(MomentId);

        var node = Assert.Single(tree);
        Assert.Equal("小柔", node.AuthorName);
        Assert.Equal("https://example.com/new.png", node.AvatarUrl);
    }

    [Fact]
    public async Task 立即触发会把话写进评论区() {
        using var db = TestDoubles.Database(nameof(立即触发会把话写进评论区));
        await SeedAsync(db);

        var client = new ResponsesClientStub(ResponsesResult.Success("这张照片好好看呀"));
        var probe = await Service(db, client, Options(Member())).ProbeAsync(MemberId, 0, persist: true);

        Assert.True(probe.Ok);
        Assert.True(probe.Saved);
        Assert.Equal("这张照片好好看呀", probe.Text);

        var comment = Assert.Single(db.Comments);
        Assert.Equal(MemberId, comment.LlmMemberId);
    }

    [Fact]
    public async Task 试一句只看看不入库() {
        using var db = TestDoubles.Database(nameof(试一句只看看不入库));
        await SeedAsync(db);

        var client = new ResponsesClientStub(ResponsesResult.Success("光看文字也知道你们很开心"));
        var probe = await Service(db, client, Options(Member())).ProbeAsync(MemberId, 0, persist: false);

        Assert.True(probe.Ok);
        Assert.False(probe.Saved);
        Assert.Empty(db.Comments);
    }

    [Fact]
    public async Task 立即触发不理会概率和已经开过口这些规矩() {
        using var db = TestDoubles.Database(nameof(立即触发不理会概率和已经开过口这些规矩));
        await SeedAsync(db);

        var client = new ResponsesClientStub(
            ResponsesResult.Success("第一句"),
            ResponsesResult.Success("第二句"));

        // 概率为 0、而且这一楼已经开过了，日常路径都会被拦下来，调试这条不该被拦
        var service = Service(db, client, Options(Member(commentChance: 0)));

        Assert.True((await service.ProbeAsync(MemberId, 0, persist: true)).Ok);
        Assert.True((await service.ProbeAsync(MemberId, 0, persist: true)).Ok);

        Assert.Equal(2, db.Comments.Count());
    }

    [Fact]
    public async Task 停用的角色也能拿来试线路() {
        using var db = TestDoubles.Database(nameof(停用的角色也能拿来试线路));
        await SeedAsync(db);

        var member = Member();
        member.Enabled = false;

        var client = new ResponsesClientStub(ResponsesResult.Success("我还没上岗，先试试嗓子"));
        var probe = await Service(db, client, Options(member)).ProbeAsync(MemberId, 0, persist: false);

        Assert.True(probe.Ok);
    }

    [Fact]
    public async Task 没配全的角色试不了() {
        using var db = TestDoubles.Database(nameof(没配全的角色试不了));
        await SeedAsync(db);

        var member = Member();
        member.ApiKey = string.Empty;

        var client = new ResponsesClientStub(ResponsesResult.Success("不该出现"));
        var probe = await Service(db, client, Options(member)).ProbeAsync(MemberId, 0, persist: false);

        Assert.False(probe.Ok);
        Assert.Contains("API Key", probe.Message, StringComparison.Ordinal);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task 调试也不给草稿开后门() {
        using var db = TestDoubles.Database(nameof(调试也不给草稿开后门));
        await SeedAsync(db, MomentStatus.Draft);

        var client = new ResponsesClientStub(ResponsesResult.Success("不该出现"));
        var probe = await Service(db, client, Options(Member())).ProbeAsync(MemberId, MomentId, persist: true);

        Assert.False(probe.Ok);
        Assert.Contains("草稿", probe.Message, StringComparison.Ordinal);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task 话没说完时提示去调大输出上限() {
        using var db = TestDoubles.Database(nameof(话没说完时提示去调大输出上限));
        await SeedAsync(db);

        var client = new ResponsesClientStub(ResponsesResult.Failed(ResponsesFailure.Truncated));
        var probe = await Service(db, client, Options(Member())).ProbeAsync(MemberId, 0, persist: true);

        Assert.False(probe.Ok);
        Assert.Contains("token", probe.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.Comments);
    }

    [Fact]
    public void 复制角色时连接配置照抄一份() {
        var source = Member();
        source.AvatarUrl = "https://example.com/rou.png";
        source.AllowImages = true;
        source.MaxOutputTokens = 2048;

        var options = Options(source);
        var copy = source.CopyAs(options.NewId(), options.UniqueName($"{source.Name} 副本"));

        Assert.Equal(source.BaseUrl, copy.BaseUrl);
        Assert.Equal(source.Model, copy.Model);
        Assert.Equal(source.ApiKey, copy.ApiKey);
        Assert.Equal(source.Prompt, copy.Prompt);
        Assert.Equal(source.AvatarUrl, copy.AvatarUrl);
        Assert.Equal(source.AllowImages, copy.AllowImages);
        Assert.Equal(source.CommentChance, copy.CommentChance);
        Assert.Equal(source.ReplyChance, copy.ReplyChance);
        Assert.Equal(source.DelayMinMinutes, copy.DelayMinMinutes);
        Assert.Equal(source.DelayMaxMinutes, copy.DelayMaxMinutes);
        Assert.Equal(source.MaxOutputTokens, copy.MaxOutputTokens);
    }

    [Fact]
    public void 复制件必须换一个新标识() {
        var source = Member();
        var options = Options(source);

        var copy = source.CopyAs(options.NewId(), "阿柔 副本");

        Assert.NotEqual(source.Id, copy.Id);
        Assert.NotEmpty(copy.Id);
    }

    [Fact]
    public void 复制件先停用免得顶着同一个名字抢着说话() {
        var source = Member();
        Assert.True(source.Enabled);

        var copy = source.CopyAs("another", "阿柔 副本");

        Assert.False(copy.Enabled);
        Assert.False(copy.IsUsable);
        Assert.True(copy.IsConfigured);
    }

    [Fact]
    public void 复制件的名字不会和现有角色撞上() {
        var source = Member();
        var options = Options(source);

        var first = source.CopyAs(options.NewId(), options.UniqueName($"{source.Name} 副本"));
        options.Members.Add(first);

        var second = source.CopyAs(options.NewId(), options.UniqueName($"{source.Name} 副本"));

        Assert.Equal("阿柔 副本", first.Name);
        Assert.Equal("阿柔 副本 2", second.Name);
    }

    [Fact]
    public void 新发的标识不会和已有的撞上() {
        var source = Member();
        var options = Options(source);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MemberId };

        for (var i = 0; i < 50; i++) {
            var id = options.NewId();
            Assert.True(seen.Add(id), $"标识重复了：{id}");
            options.Members.Add(source.CopyAs(id, $"副本{i}"));
        }
    }

    [Fact]
    public async Task 巡检会把漏掉的那次留言补回来() {
        using var db = TestDoubles.Database(nameof(巡检会把漏掉的那次留言补回来));
        await SeedAsync(db);

        var planned = await Service(db, new ResponsesClientStub(), Options(Member(commentChance: 100))).SweepAsync();

        var trigger = Assert.Single(planned);
        Assert.Equal(LlmAtmosphereTriggerKind.Comment, trigger.Kind);
        Assert.Equal(MemberId, trigger.MemberId);
    }

    [Fact]
    public async Task 已经开过口的角色不会被巡检再排一次() {
        using var db = TestDoubles.Database(nameof(已经开过口的角色不会被巡检再排一次));
        await SeedAsync(db);

        _ = db.Comments.Add(new Comment {
            MomentId = MomentId,
            AuthorName = "阿柔",
            Content = "那天真好",
            LlmMemberId = MemberId,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
        });

        _ = await db.SaveChangesAsync();

        var planned = await Service(db, new ResponsesClientStub(), Options(Member(commentChance: 100))).SweepAsync();

        Assert.Empty(planned);
    }

    [Fact]
    public async Task 刚有人说完话就先歇一会儿() {
        using var db = TestDoubles.Database(nameof(刚有人说完话就先歇一会儿));
        await SeedAsync(db);

        _ = db.Comments.Add(new Comment {
            MomentId = MomentId,
            AuthorName = "别人",
            Content = "刚说完",
            LlmMemberId = "someone-else",
            CreatedAt = DateTimeOffset.UtcNow
        });

        _ = await db.SaveChangesAsync();

        var planned = await Service(db, new ResponsesClientStub(), Options(Member(commentChance: 100))).SweepAsync();

        Assert.Empty(planned);
    }

    [Fact]
    public async Task 巡检会补上没接住的那句回复() {
        using var db = TestDoubles.Database(nameof(巡检会补上没接住的那句回复));
        await SeedAsync(db);

        var mine = new Comment {
            MomentId = MomentId,
            AuthorName = "阿柔",
            Content = "海好蓝",
            LlmMemberId = MemberId,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
        };

        _ = db.Comments.Add(mine);
        _ = await db.SaveChangesAsync();

        _ = db.Comments.Add(new Comment {
            MomentId = MomentId,
            ParentId = mine.Id,
            AuthorId = AuthorId,
            AuthorName = "男主",
            Content = "下次一起去呀",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        });

        _ = await db.SaveChangesAsync();

        var planned = await Service(db, new ResponsesClientStub(), Options(Member(replyChance: 100))).SweepAsync();

        var trigger = Assert.Single(planned, item => item.Kind == LlmAtmosphereTriggerKind.Reply);
        Assert.Equal(mine.Id, trigger.ParentCommentId);
    }

    [Fact]
    public async Task 很久以前发的记录不在巡检范围里() {
        using var db = TestDoubles.Database(nameof(很久以前发的记录不在巡检范围里));
        await SeedAsync(db);

        var moment = await db.Moments.FindAsync(MomentId);
        moment!.CreatedAt = DateTimeOffset.UtcNow.AddDays(-30);
        _ = await db.SaveChangesAsync();

        var planned = await Service(db, new ResponsesClientStub(), Options(Member(commentChance: 100))).SweepAsync();

        Assert.Empty(planned);
    }

    #region 私有方法

    private static LlmAtmosphereService Service(
        OurStoryDbContext db,
        IResponsesClient client,
        LlmAtmosphereOptions options,
        IMomentImageSource? images = null) {
        var configuration = TestDoubles.Configuration(options);

        return new LlmAtmosphereService(
            db,
            configuration,
            new SettingsStub(),
            new CommentService(db, new SettingsStub(), configuration, TestDoubles.Atmosphere(), TestDoubles.Clock()),
            client,
            images ?? new MomentImageSourceStub(),
            TestDoubles.Clock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmAtmosphereService>.Instance);
    }

    private static MomentService Moments(OurStoryDbContext db, AtmosphereSchedulerSpy atmosphere) =>
        new(
            db,
            new SettingsStub(),
            TestDoubles.Markdown(),
            TestDoubles.NoPoints(),
            TestDoubles.Notifications(),
            atmosphere,
            TestDoubles.Clock());

    private static CommentService Comments(
        OurStoryDbContext db,
        AtmosphereSchedulerSpy atmosphere,
        LlmAtmosphereOptions options) =>
        new(db, new SettingsStub(), TestDoubles.Configuration(options), atmosphere, TestDoubles.Clock());

    private static async Task SeedAsync(
        OurStoryDbContext db,
        MomentStatus status = MomentStatus.Published,
        string? password = null) {
        _ = db.Users.Add(new User { Id = AuthorId, UserName = "boy", Role = UserRole.Boy });
        _ = db.Moments.Add(new Moment {
            Id = MomentId,
            Title = "第一次一起看海",
            Slug = "sea",
            ContentHtml = "<p>浪很大，风也很大</p>",
            AuthorId = AuthorId,
            Status = status,
            AllowComment = true,
            Password = password
        });

        _ = await db.SaveChangesAsync();
    }

    private static LlmAtmosphereTrigger Trigger(int? parentId = null) =>
        new(
            parentId is null ? LlmAtmosphereTriggerKind.Comment : LlmAtmosphereTriggerKind.Reply,
            MomentId,
            MemberId,
            DateTimeOffset.UtcNow,
            parentId);

    private static LlmAtmosphereOptions Options(params LlmAtmosphereMember[] members) =>
        new() {
            Enabled = true,
            Members = [.. members]
        };

    private static LlmAtmosphereMember Member(int commentChance = 60, int replyChance = 70) =>
        new() {
            Id = MemberId,
            Name = "阿柔",
            BaseUrl = "https://example.com/v1",
            Model = "test-model",
            ApiKey = "sk-test",
            Prompt = "说话温温柔柔",
            Enabled = true,
            CommentChance = commentChance,
            ReplyChance = replyChance,
            DelayMinMinutes = 0,
            DelayMaxMinutes = 0
        };

    private static MomentEditModel Draft(string title, MomentStatus status = MomentStatus.Published) =>
        new() {
            Title = title,
            Content = "海很蓝",
            Status = status,
            MomentDate = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Unspecified)
        };

    #endregion
}
