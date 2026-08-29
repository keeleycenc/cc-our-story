// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core.Entities;
using OurStory.Data.Conventions;

namespace OurStory.Data;

/// <summary>
/// 获取整站唯一的数据库上下文
///
/// 当前数据规模较小，无需采用分库分表方案
/// </summary>
public class OurStoryDbContext(DbContextOptions<OurStoryDbContext> options) : DbContext(options) {
    /// <summary>
    /// 获取用户数据集合
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// 获取情侣关系数据集合
    /// </summary>
    public DbSet<CoupleRelationship> CoupleRelationships => Set<CoupleRelationship>();

    /// <summary>
    /// 获取花信记录数据集合
    /// </summary>
    public DbSet<CycleRecord> CycleRecords => Set<CycleRecord>();

    /// <summary>
    /// 获取花信每日补充记录数据集合
    /// </summary>
    public DbSet<CycleDailyLog> CycleDailyLogs => Set<CycleDailyLog>();

    /// <summary>
    /// 获取动态数据集合
    /// </summary>
    public DbSet<Moment> Moments => Set<Moment>();

    /// <summary>
    /// 获取纪念日数据集合
    /// </summary>
    public DbSet<Anniversary> Anniversaries => Set<Anniversary>();

    /// <summary>
    /// 获取评论数据集合
    /// </summary>
    public DbSet<Comment> Comments => Set<Comment>();

    /// <summary>
    /// 获取心跳数据集合
    /// </summary>
    public DbSet<Heartbeat> Heartbeats => Set<Heartbeat>();

    /// <summary>
    /// 获取设置数据集合
    /// </summary>
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();

    /// <summary>
    /// 获取心点记录数据集合
    /// </summary>
    public DbSet<HeartPointEntry> HeartPointEntries => Set<HeartPointEntry>();

    /// <summary>
    /// 获取商店物品数据集合
    /// </summary>
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();

    /// <summary>
    /// 获取商店预设数据集合
    /// </summary>
    public DbSet<ShopPreset> ShopPresets => Set<ShopPreset>();

    /// <summary>
    /// 获取推送设备数据集合
    /// </summary>
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    /// <summary>
    /// 获取通知设置数据集合
    /// </summary>
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();

    /// <summary>
    /// 获取心有灵犀题目数据集合
    /// </summary>
    public DbSet<AffinityQuestion> AffinityQuestions => Set<AffinityQuestion>();

    /// <summary>
    /// 获取心有灵犀题目选项数据集合
    /// </summary>
    public DbSet<AffinityQuestionOption> AffinityQuestionOptions => Set<AffinityQuestionOption>();

    /// <summary>
    /// 获取心有灵犀每日题目数据集合
    /// </summary>
    public DbSet<AffinityDailyQuestion> AffinityDailyQuestions => Set<AffinityDailyQuestion>();

    /// <summary>
    /// 获取心有灵犀答案数据集合
    /// </summary>
    public DbSet<AffinityAnswer> AffinityAnswers => Set<AffinityAnswer>();

    /// <summary>
    /// 执行模型创建配置
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(OurStoryDbContext).Assembly);

        // 在实体映射完成后遍历模型，并将所有时间戳列转换为整数类型
        TimestampConverters.Apply(modelBuilder);
    }
}
