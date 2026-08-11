// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Data;
using OurStory.Services.Comments;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 留言树：楼中楼只留两层，「作者」只认这篇记录的作者
/// </summary>
public sealed class CommentTreeTests : IDisposable {
    private const int MomentId = 1;
    private const int AuthorId = 10;
    private const int PartnerId = 20;

    private readonly OurStoryDbContext _db;
    private readonly CommentService _comments;

    /// <summary>
    /// 执行 CommentTreeTests 操作
    /// </summary>
    public CommentTreeTests() {
        _db = TestDoubles.Database("comments");

        _ = _db.Users.Add(new User { Id = AuthorId, UserName = "boy", Role = UserRole.Boy });
        _ = _db.Users.Add(new User { Id = PartnerId, UserName = "girl", Role = UserRole.Girl });
        _ = _db.Moments.Add(new Moment { Id = MomentId, Title = "记一天", Slug = "one-day", AuthorId = AuthorId });
        _ = _db.SaveChanges();

        _comments = new CommentService(_db, new SettingsStub(), TestDoubles.Clock());
    }

    /// <summary>
    /// 释放上下文
    /// </summary>
    public void Dispose() {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    /// <summary>
    /// 验证回复再深也只有两层，孙辈拍平后记得住回复的是谁()
    /// </summary>
    [Fact]
    public async Task 回复超过两层时拍平到顶层留言下面() {
        Add(1, null, "路人甲");
        Add(2, 1, "路人乙");
        Add(3, 2, "路人丙");
        Add(4, 3, "路人丁");

        var roots = await _comments.GetTreeAsync(MomentId);

        var root = Assert.Single(roots);
        Assert.Equal(3, root.Replies.Count);
        Assert.All(root.Replies, reply => Assert.Empty(reply.Replies));

        // 直接回顶层的那条不用标「回复 @谁」，再往下的都要标
        Assert.Null(root.Replies[0].ReplyToName);
        Assert.Equal("路人乙", root.Replies[1].ReplyToName);
        Assert.Equal(2, root.Replies[1].ReplyToId);
        Assert.Equal("路人丙", root.Replies[2].ReplyToName);
        Assert.Equal(3, root.Replies[2].ReplyToId);
    }

    /// <summary>
    /// 验证父留言被删掉的回复不会消失()
    /// </summary>
    [Fact]
    public async Task 找不到父留言的回复当顶层显示() {
        Add(1, 404, "路人甲");

        var roots = await _comments.GetTreeAsync(MomentId);

        var root = Assert.Single(roots);
        Assert.Equal(1, root.Id);
        Assert.Null(root.ReplyToName);
    }

    /// <summary>
    /// 验证「作者」只给写这篇记录的人，另一半来留言不算()
    /// </summary>
    [Fact]
    public async Task 作者角标只认这篇记录的作者() {
        Add(1, null, "男主", AuthorId);
        Add(2, null, "女主", PartnerId);
        Add(3, null, "路人甲");

        var roots = await _comments.GetTreeAsync(MomentId);

        Assert.True(roots[0].IsAuthor);
        Assert.False(roots[1].IsAuthor);
        Assert.False(roots[2].IsAuthor);

        // 两个人都还是站主，只是不都挂「作者」
        Assert.True(roots[1].IsOwner);
        Assert.False(roots[2].IsOwner);
    }

    private void Add(int id, int? parentId, string name, int? authorId = null) {
        _ = _db.Comments.Add(new Comment {
            Id = id,
            MomentId = MomentId,
            ParentId = parentId,
            AuthorId = authorId,
            AuthorName = name,
            Content = "内容 " + id,
            IsApproved = true,
            CreatedAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero).AddMinutes(id)
        });

        _ = _db.SaveChanges();
    }
}
