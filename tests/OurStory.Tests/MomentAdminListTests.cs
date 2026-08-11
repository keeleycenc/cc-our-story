// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Data;
using OurStory.Services.Moments;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 后台列表：总览只列自己发的，点点滴滴那一页还是两个人的都列
/// </summary>
public sealed class MomentAdminListTests : IDisposable {
    private const int Boy = 10;
    private const int Girl = 20;

    private readonly OurStoryDbContext _db;
    private readonly MomentService _moments;

    /// <summary>
    /// 执行 MomentAdminListTests 操作
    /// </summary>
    public MomentAdminListTests() {
        _db = TestDoubles.Database("moments");

        // 列表要 Include 作者，两条用户记录不能少
        _ = _db.Users.Add(new User { Id = Boy, UserName = "boy", Role = UserRole.Boy });
        _ = _db.Users.Add(new User { Id = Girl, UserName = "girl", Role = UserRole.Girl });

        Add(1, Boy, "男主写的一");
        Add(2, Girl, "女主写的");
        Add(3, Boy, "男主写的二");
        _ = _db.SaveChanges();

        _moments = new MomentService(_db, new SettingsStub(), new MarkdownRenderer(), TestDoubles.Clock());
    }

    /// <summary>
    /// 释放上下文
    /// </summary>
    public void Dispose() {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    /// <summary>
    /// 验证传了作者就只列这个人发的，总数也跟着只数这个人的()
    /// </summary>
    [Fact]
    public async Task 指定作者时只列这个人发的() {
        var page = await _moments.ListForAdminAsync(1, 10, Boy);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, moment => Assert.Equal(Boy, moment.AuthorId));
    }

    /// <summary>
    /// 验证不传作者时两个人的都在()
    /// </summary>
    [Fact]
    public async Task 不指定作者时列出全部() {
        var page = await _moments.ListForAdminAsync(1, 10);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
    }

    private void Add(int id, int authorId, string title) =>
        _db.Moments.Add(new Moment {
            Id = id,
            AuthorId = authorId,
            Title = title,
            Slug = "slug-" + id,
            MomentDate = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero).AddDays(id)
        });
}
