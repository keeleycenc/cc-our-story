// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services;
using OurStory.Services.HeartPoints;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 启动时那几步：建表、建账号、放自带预设、把旧数据补记成心意
/// </summary>
public class DatabaseInitializerTests {
    [Fact]
    public async Task 首次启动会放入自带的心愿预设() {
        await using var harness = SqliteHarness.Create(createSchema: false);

        _ = await Initializer(harness).InitializeAsync();

        var presets = await harness.Db.ShopPresets.AsNoTracking().OrderBy(item => item.SortOrder).ToListAsync();
        Assert.Equal(DefaultShopPresets.All.Count, presets.Count);
        Assert.Equal(DefaultShopPresets.All[0].Title, presets[0].Title);
        Assert.All(presets, preset => Assert.True(preset.IsActive));

        // 自带的一律不配图，免得后台列表变成一面照片墙
        Assert.All(presets, preset => Assert.Null(preset.CoverUrl));
    }

    [Fact]
    public async Task 预设被删光之后不会重新塞回来() {
        await using var harness = SqliteHarness.Create(createSchema: false);
        var settings = new SettingsStub();

        _ = await Initializer(harness, settings).InitializeAsync();
        _ = await harness.Db.ShopPresets.ExecuteDeleteAsync();

        _ = await Initializer(harness, settings).InitializeAsync();

        Assert.Equal(0, await harness.Db.ShopPresets.CountAsync());
    }

    [Fact]
    public async Task 预设还剩一个时不会补齐其它的() {
        await using var harness = SqliteHarness.Create(createSchema: false);
        var settings = new SettingsStub();

        _ = await Initializer(harness, settings).InitializeAsync();
        var keep = await harness.Db.ShopPresets.OrderBy(item => item.Id).FirstAsync();
        _ = await harness.Db.ShopPresets.Where(item => item.Id != keep.Id).ExecuteDeleteAsync();

        _ = await Initializer(harness, settings).InitializeAsync();

        Assert.Equal(1, await harness.Db.ShopPresets.CountAsync());
    }

    [Fact]
    public async Task 启动时会静默补记初始心意() {
        await using var harness = SqliteHarness.Create(createSchema: false);
        var settings = new SettingsStub();

        // 先建好表和两个账号，再塞一点「商城上线之前」的旧数据
        _ = await Initializer(harness, settings).InitializeAsync();
        var boy = await harness.Db.Users.SingleAsync(user => user.Role == UserRole.Boy);

        harness.Db.Heartbeats.AddRange(
            new Heartbeat { Role = UserRole.Boy, UserId = boy.Id, ClickDay = "2026-08-01" },
            new Heartbeat { Role = UserRole.Boy, UserId = boy.Id, ClickDay = "2026-08-02" });
        _ = await harness.Db.SaveChangesAsync();

        // 上一轮已经把标记写上了，这里换一份设置模拟「升级上来的老库」第一次启动
        var fresh = new SettingsStub();
        _ = await Initializer(harness, fresh).InitializeAsync();

        Assert.Equal(4, await Points(harness, fresh).GetBalanceAsync(boy.Id));
        Assert.True(await Points(harness, fresh).IsBackfilledAsync());
    }

    [Fact]
    public async Task 反复启动不会重复补记() {
        await using var harness = SqliteHarness.Create(createSchema: false);
        var settings = new SettingsStub();

        _ = await Initializer(harness, settings).InitializeAsync();
        var boy = await harness.Db.Users.SingleAsync(user => user.Role == UserRole.Boy);
        _ = harness.Db.Heartbeats.Add(new Heartbeat { Role = UserRole.Boy, UserId = boy.Id, ClickDay = "2026-08-01" });
        _ = await harness.Db.SaveChangesAsync();

        var upgraded = new SettingsStub();
        _ = await Initializer(harness, upgraded).InitializeAsync();
        _ = await Initializer(harness, upgraded).InitializeAsync();
        _ = await Initializer(harness, upgraded).InitializeAsync();

        Assert.Equal(2, await Points(harness, upgraded).GetBalanceAsync(boy.Id));
    }

    private static HeartPointService Points(SqliteHarness harness, SettingsStub settings) =>
        new(harness.Db, settings, TestDoubles.Clock());

    private static DatabaseInitializer Initializer(SqliteHarness harness, SettingsStub? settings = null) {
        var store = settings ?? new SettingsStub();
        return new DatabaseInitializer(
            harness.Db,
            store,
            Points(harness, store),
            new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration()),
            NullLogger<DatabaseInitializer>.Instance);
    }
}
