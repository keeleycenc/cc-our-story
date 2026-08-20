// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Data;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 迁移链本身的测试
/// </summary>
public class MigrationTests {
    [Fact]
    public async Task 迁移可以从空库一路跑到最新() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = new OurStoryDbContext(
            new DbContextOptionsBuilder<OurStoryDbContext>().UseSqlite(connection).Options);

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task 迁移之后心意与心愿的表可以读写() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = new OurStoryDbContext(
            new DbContextOptionsBuilder<OurStoryDbContext>().UseSqlite(connection).Options);

        await db.Database.MigrateAsync();

        var boy = new User { UserName = "boy", Role = UserRole.Boy, PasswordHash = "test" };
        _ = db.Users.Add(boy);
        _ = await db.SaveChangesAsync();

        _ = db.HeartPointEntries.Add(new HeartPointEntry {
            UserId = boy.Id,
            ChangeAmount = 12,
            Reason = HeartPointReason.AnniversaryPublished,
            SourceKey = "anniversary:2026-08-15",
            Note = "记下一个纪念日"
        });

        _ = db.ShopItems.Add(new ShopItem {
            Title = "洗碗券",
            Description = "今晚的碗我来洗",
            Price = 30,
            SellerId = boy.Id,
            ListingDays = 30,
            ValidDays = 30,
            ListingExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        });

        _ = await db.SaveChangesAsync();

        Assert.Equal(12, await db.HeartPointEntries.SumAsync(entry => entry.ChangeAmount));
        Assert.Equal(1, await db.ShopItems.CountAsync());
    }

    [Fact]
    public async Task 心有灵犀迁移会建立每日和答案唯一约束() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new OurStoryDbContext(
            new DbContextOptionsBuilder<OurStoryDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();

        var boy = new User { UserName = "boy-affinity", Role = UserRole.Boy, PasswordHash = "test" };
        _ = db.Users.Add(boy);
        _ = await db.SaveChangesAsync();
        var daily = new AffinityDailyQuestion {
            Day = "2026-08-20",
            QuestionText = "测试题目",
            Category = "日常",
            OptionsJson = "[\"一\",\"二\"]"
        };
        _ = db.AffinityDailyQuestions.Add(daily);
        _ = await db.SaveChangesAsync();

        db.AffinityAnswers.AddRange(
            new AffinityAnswer { DailyQuestionId = daily.Id, UserId = boy.Id, Role = UserRole.Boy, SelectedOptionIndexesJson = "[0]" },
            new AffinityAnswer { DailyQuestionId = daily.Id, UserId = boy.Id, Role = UserRole.Boy, SelectedOptionIndexesJson = "[1]" });

        _ = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task 同一来源的流水在库上就是唯一的() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var db = new OurStoryDbContext(
            new DbContextOptionsBuilder<OurStoryDbContext>().UseSqlite(connection).Options);

        await db.Database.MigrateAsync();

        var boy = new User { UserName = "boy", Role = UserRole.Boy, PasswordHash = "test" };
        _ = db.Users.Add(boy);
        _ = await db.SaveChangesAsync();

        _ = db.HeartPointEntries.Add(Entry(boy.Id));
        _ = await db.SaveChangesAsync();

        _ = db.HeartPointEntries.Add(Entry(boy.Id));
        _ = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static HeartPointEntry Entry(int userId) => new() {
        UserId = userId,
        ChangeAmount = 2,
        Reason = HeartPointReason.DailyHeartbeat,
        SourceKey = "heartbeat:2026-08-15",
        Note = "今天想你了"
    };
}
