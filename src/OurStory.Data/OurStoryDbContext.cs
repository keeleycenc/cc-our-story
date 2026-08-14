// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core.Entities;
using OurStory.Data.Conventions;

namespace OurStory.Data;

/// <summary>
/// 整站唯一的数据库上下文。
///
/// 表都很小（两个人的站点），所以没有分库分表之类的考虑
/// </summary>
public class OurStoryDbContext(DbContextOptions<OurStoryDbContext> options) : DbContext(options) {
    /// <summary>
    /// 执行 Users 操作
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// 执行 Moments 操作
    /// </summary>
    public DbSet<Moment> Moments => Set<Moment>();

    /// <summary>
    /// 执行 Anniversaries 操作
    /// </summary>
    public DbSet<Anniversary> Anniversaries => Set<Anniversary>();

    /// <summary>
    /// 执行 Comments 操作
    /// </summary>
    public DbSet<Comment> Comments => Set<Comment>();

    /// <summary>
    /// 执行 Heartbeats 操作
    /// </summary>
    public DbSet<Heartbeat> Heartbeats => Set<Heartbeat>();

    /// <summary>
    /// 执行 Settings 操作
    /// </summary>
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();

    /// <summary>
    /// 执行 OnModelCreating 操作
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(OurStoryDbContext).Assembly);

        // 放在映射之后：这一步要遍历已经建好的模型，把所有时间戳列改成整数
        TimestampConverters.Apply(modelBuilder);
    }
}
