// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OurStory.Core;
using OurStory.Services.HeartPoints;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;
using System.Security.Claims;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 每天第一次打开站点的那一笔心意
/// </summary>
public class DailyVisitRewardTests {
    [Fact]
    public async Task 每天第一次打开站点拿到心意() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var (pipeline, services) = Build(harness);

        await pipeline(Request(services, Owner(boyId)));
        await pipeline(Request(services, Owner(boyId)));

        var entry = await harness.Db.HeartPointEntries.AsNoTracking().SingleAsync();
        Assert.Equal(3, entry.ChangeAmount);
        Assert.Equal(HeartPointReason.DailyVisit, entry.Reason);
        Assert.Equal(boyId, entry.UserId);
    }

    [Fact]
    public async Task 访客不签到() {
        await using var harness = SqliteHarness.Create();
        _ = await harness.SeedCoupleAsync();
        var (pipeline, services) = Build(harness);

        await pipeline(Request(services, new ClaimsPrincipal(new ClaimsIdentity())));

        Assert.Equal(0, await harness.Db.HeartPointEntries.CountAsync());
    }

    [Fact]
    public async Task 提交表单不签到() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var (pipeline, services) = Build(harness);

        var context = Request(services, Owner(boyId));
        context.Request.Method = HttpMethods.Post;
        await pipeline(context);

        Assert.Equal(0, await harness.Db.HeartPointEntries.CountAsync());
    }

    [Fact]
    public async Task 两个人各拿各的() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var (pipeline, services) = Build(harness);

        await pipeline(Request(services, Owner(boyId)));
        await pipeline(Request(services, Owner(girlId, UserRole.Girl)));

        Assert.Equal(2, await harness.Db.HeartPointEntries.CountAsync());
    }

    [Fact]
    public async Task 重启之后当天再来一次不重复记账也不留下写入错误() {
        var errors = new List<string>();
        await using var harness = SqliteHarness.Create(errors: errors);
        var (boyId, _) = await harness.SeedCoupleAsync();

        var (before, beforeServices) = Build(harness);
        await before(Request(beforeServices, Owner(boyId)));

        // 模拟站点重启后内存状态被清空，并在同一天再次处理首次访问。
        var (after, afterServices) = Build(harness);
        await after(Request(afterServices, Owner(boyId)));

        Assert.Equal(1, await harness.Db.HeartPointEntries.CountAsync());

        // 当日奖励已领取属于预期分支，不应通过唯一索引冲突进行判断。
        Assert.Empty(errors);
    }

    private static (RequestDelegate Pipeline, IServiceProvider Services) Build(SqliteHarness harness) {
        var settings = new SettingsStub();
        var clock = TestDoubles.Clock();

        var services = new ServiceCollection();
        _ = services.AddSingleton(clock);
        _ = services.AddSingleton<DailyVisitLedger>();
        _ = services.AddSingleton<ISettingsService>(settings);
        _ = services.AddSingleton<IHeartPointService>(new HeartPointService(harness.Db, settings, clock));

        var provider = services.BuildServiceProvider();
        return (new ApplicationBuilder(provider).UseDailyVisitReward().Build(), provider);
    }

    private static DefaultHttpContext Request(IServiceProvider services, ClaimsPrincipal user) {
        var context = new DefaultHttpContext { RequestServices = services, User = user };
        context.Request.Method = HttpMethods.Get;
        return context;
    }

    private static ClaimsPrincipal Owner(int userId, UserRole role = UserRole.Boy) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            "test"));
}
