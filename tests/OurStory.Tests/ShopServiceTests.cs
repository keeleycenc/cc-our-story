// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Services.HeartPoints;
using OurStory.Services.Shop;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 心愿商品的状态机与兑换规则
/// </summary>
public class ShopServiceTests {
    [Fact]
    public async Task 标价超出区间时发布不了() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);

        var tooCheap = await shop.PublishAsync(Wish(price: 1), boyId);
        var tooDear = await shop.PublishAsync(Wish(price: 5000), boyId);

        Assert.False(tooCheap.Success);
        Assert.False(tooDear.Success);
        Assert.Equal(0, await harness.Db.ShopItems.CountAsync());
    }

    [Fact]
    public async Task 不能兑换自己发布的心愿() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        await GiveAsync(harness, boyId, 100);
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(), boyId);
        var item = await harness.Db.ShopItems.SingleAsync();

        var result = await shop.PurchaseAsync(item.Id, boyId);

        Assert.False(result.Success);
        Assert.Equal(ShopItemStatus.Listed, (await harness.Db.ShopItems.SingleAsync()).Status);
    }

    [Fact]
    public async Task 心意不够时兑换失败() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        await GiveAsync(harness, girlId, 10);
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(price: 30), boyId);
        var item = await harness.Db.ShopItems.SingleAsync();

        var result = await shop.PurchaseAsync(item.Id, girlId);

        Assert.False(result.Success);
        Assert.Equal(10, await Points(harness).GetBalanceAsync(girlId));
        Assert.Equal(ShopItemStatus.Listed, (await harness.Db.ShopItems.SingleAsync()).Status);
    }

    [Fact]
    public async Task 兑换只扣买家的心意不给卖家() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        await GiveAsync(harness, girlId, 50);
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(price: 30), boyId);
        var item = await harness.Db.ShopItems.SingleAsync();

        var result = await shop.PurchaseAsync(item.Id, girlId);
        var saved = await harness.Db.ShopItems.SingleAsync();

        Assert.True(result.Success);
        Assert.Equal(20, await Points(harness).GetBalanceAsync(girlId));
        Assert.Equal(0, await Points(harness).GetBalanceAsync(boyId));
        Assert.Equal(ShopItemStatus.Redeemed, saved.Status);
        Assert.Equal(girlId, saved.BuyerId);
        _ = Assert.NotNull(saved.ExpiresAt);
    }

    [Fact]
    public async Task 已兑换的心愿不能再兑换() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        await GiveAsync(harness, girlId, 100);
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(price: 30), boyId);
        var item = await harness.Db.ShopItems.SingleAsync();

        _ = await shop.PurchaseAsync(item.Id, girlId);
        var again = await shop.PurchaseAsync(item.Id, girlId);

        Assert.False(again.Success);
        Assert.Equal(70, await Points(harness).GetBalanceAsync(girlId));
    }

    [Fact]
    public async Task 双方确认要走完两步才算用掉() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var itemId = await RedeemedItemAsync(harness, boyId, girlId, ShopRedeemMode.MutualConfirm);
        var shop = Shop(harness);

        _ = await shop.RequestRedeemAsync(itemId, girlId);
        Assert.Equal(ShopItemStatus.PendingConfirm, await StatusAsync(harness, itemId));

        // 持有人自己确认不了，得由发布者点头
        var wrongHand = await shop.ConfirmRedeemAsync(itemId, girlId);
        Assert.False(wrongHand.Success);

        var done = await shop.ConfirmRedeemAsync(itemId, boyId);
        Assert.True(done.Success);
        Assert.Equal(ShopItemStatus.Used, await StatusAsync(harness, itemId));
    }

    [Fact]
    public async Task 立即使用一步到位() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var itemId = await RedeemedItemAsync(harness, boyId, girlId, ShopRedeemMode.Instant);

        var result = await Shop(harness).RequestRedeemAsync(itemId, girlId);

        Assert.True(result.Success);
        Assert.Equal(ShopItemStatus.Used, await StatusAsync(harness, itemId));
        _ = Assert.NotNull((await harness.Db.ShopItems.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task 已使用之后谁也撤不回() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var itemId = await RedeemedItemAsync(harness, boyId, girlId, ShopRedeemMode.Instant);
        var shop = Shop(harness);
        _ = await shop.RequestRedeemAsync(itemId, girlId);

        Assert.False((await shop.CancelRedeemAsync(itemId, girlId)).Success);
        Assert.False((await shop.CancelRedeemAsync(itemId, boyId)).Success);
        Assert.False((await shop.RequestRedeemAsync(itemId, girlId)).Success);
        Assert.Equal(ShopItemStatus.Used, await StatusAsync(harness, itemId));
    }

    [Fact]
    public async Task 待履约可以退回仓库() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var itemId = await RedeemedItemAsync(harness, boyId, girlId, ShopRedeemMode.MutualConfirm);
        var shop = Shop(harness);
        _ = await shop.RequestRedeemAsync(itemId, girlId);

        var back = await shop.CancelRedeemAsync(itemId, boyId);

        Assert.True(back.Success);
        Assert.Equal(ShopItemStatus.Redeemed, await StatusAsync(harness, itemId));
        Assert.Null((await harness.Db.ShopItems.SingleAsync()).RedeemRequestedAt);
        Assert.True((await shop.RequestRedeemAsync(itemId, girlId)).Success);
    }

    [Fact]
    public async Task 上架到期自动变成未兑换过期() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        await GiveAsync(harness, girlId, 100);
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(price: 30), boyId);

        var item = await harness.Db.ShopItems.SingleAsync();
        item.ListingExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _ = await harness.Db.SaveChangesAsync();

        var swept = await shop.SweepExpiredAsync();
        var result = await shop.PurchaseAsync(item.Id, girlId);

        Assert.Equal(1, swept);
        Assert.False(result.Success);
        Assert.Equal(ShopItemStatus.ListingExpired, await StatusAsync(harness, item.Id));
        Assert.Equal(100, await Points(harness).GetBalanceAsync(girlId));
    }

    [Fact]
    public async Task 兑换后到期作废且不能使用() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var itemId = await RedeemedItemAsync(harness, boyId, girlId, ShopRedeemMode.MutualConfirm);
        var shop = Shop(harness);
        _ = await shop.RequestRedeemAsync(itemId, girlId);

        var item = await harness.Db.ShopItems.SingleAsync();
        item.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _ = await harness.Db.SaveChangesAsync();

        _ = await shop.SweepExpiredAsync();

        Assert.Equal(ShopItemStatus.Expired, await StatusAsync(harness, itemId));
        Assert.False((await shop.ConfirmRedeemAsync(itemId, boyId)).Success);
        Assert.False((await shop.RequestRedeemAsync(itemId, girlId)).Success);
    }

    [Fact]
    public async Task 访客看不到仅双方可见的心愿() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(title: "公开的"), boyId);
        _ = await shop.PublishAsync(Wish(title: "只有我们", isPrivate: true), boyId);

        var guest = await shop.GetPageAsync(new ShopQuery(), ShopViewer.Guest);
        var owner = await shop.GetPageAsync(new ShopQuery(), new ShopViewer(UserRole.Girl, 2));

        _ = Assert.Single(guest.Items);
        Assert.Equal("公开的", guest.Items[0].Title);
        Assert.Equal(2, owner.Items.Count);
    }

    [Fact]
    public async Task 已结束筛选包含两种过期终态() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(title: "未兑换过期"), boyId);
        _ = await shop.PublishAsync(Wish(title: "兑换后过期"), boyId);
        _ = await shop.PublishAsync(Wish(title: "正常完成"), boyId);

        var items = await harness.Db.ShopItems.ToListAsync();
        items.Single(item => item.Title == "未兑换过期").Status = ShopItemStatus.ListingExpired;
        items.Single(item => item.Title == "兑换后过期").Status = ShopItemStatus.Expired;
        items.Single(item => item.Title == "正常完成").Status = ShopItemStatus.Used;
        _ = await harness.Db.SaveChangesAsync();

        var result = await shop.GetPageAsync(
            new ShopQuery(EndedOnly: true),
            new ShopViewer(UserRole.Girl, girlId));

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
            Assert.Contains(item.Status, new[] { ShopItemStatus.ListingExpired, ShopItemStatus.Expired }));
    }

    [Fact]
    public async Task 能算出心愿在商城的第几页() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);
        for (var index = 1; index <= 5; index++) {
            _ = await shop.PublishAsync(Wish(title: $"心愿 {index}"), boyId);
        }

        var viewer = new ShopViewer(UserRole.Girl, girlId);
        var query = new ShopQuery(PageSize: 2);
        var oldest = await harness.Db.ShopItems.AsNoTracking().OrderBy(item => item.Id).FirstAsync();

        var page = await shop.FindPageAsync(oldest.Id, query, viewer);

        Assert.Equal(3, page);
        var listed = await shop.GetPageAsync(query with { Page = page }, viewer);
        Assert.Contains(listed.Items, item => item.Id == oldest.Id);
    }

    [Fact]
    public async Task 定位已结束的心愿会落到后面的页() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);
        for (var index = 1; index <= 5; index++) {
            _ = await shop.PublishAsync(Wish(title: $"心愿 {index}"), boyId);
        }

        var newest = await harness.Db.ShopItems.OrderByDescending(item => item.Id).FirstAsync();
        newest.Status = ShopItemStatus.Used;
        _ = await harness.Db.SaveChangesAsync();

        var viewer = new ShopViewer(UserRole.Girl, girlId);
        var query = new ShopQuery(PageSize: 2);

        var page = await shop.FindPageAsync(newest.Id, query, viewer);

        Assert.Equal(3, page);
        var listed = await shop.GetPageAsync(query with { Page = page }, viewer);
        Assert.Contains(listed.Items, item => item.Id == newest.Id);
    }

    [Fact]
    public async Task 定位不到的心愿返回零() {
        await using var harness = SqliteHarness.Create();
        var (boyId, girlId) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);
        _ = await shop.PublishAsync(Wish(title: "只有我们", isPrivate: true), boyId);

        var secret = await harness.Db.ShopItems.AsNoTracking().SingleAsync();

        Assert.Equal(0, await shop.FindPageAsync(secret.Id, new ShopQuery(), ShopViewer.Guest));
        Assert.Equal(1, await shop.FindPageAsync(secret.Id, new ShopQuery(), new ShopViewer(UserRole.Girl, girlId)));
        Assert.Equal(0, await shop.FindPageAsync(secret.Id + 999, new ShopQuery(), new ShopViewer(UserRole.Girl, girlId)));
    }

    [Fact]
    public async Task 删除预设不影响已发布的心愿() {
        await using var harness = SqliteHarness.Create();
        var (boyId, _) = await harness.SeedCoupleAsync();
        var shop = Shop(harness);
        var preset = await shop.CreatePresetAsync(new ShopPresetEditModel { Title = "洗碗券", Description = "今晚我来洗" });

        var wish = Wish(title: "洗碗券");
        wish.PresetId = preset.Id;
        _ = await shop.PublishAsync(wish, boyId);

        Assert.True(await shop.DeletePresetAsync(preset.Id));

        var item = await harness.Db.ShopItems.AsNoTracking().SingleAsync();
        Assert.Equal("洗碗券", item.Title);
        Assert.Null(item.PresetId);
        Assert.Equal(ShopItemStatus.Listed, item.Status);
    }

    #region 辅助

    private static ShopPublishModel Wish(string title = "洗碗券", int price = 30, bool isPrivate = false) => new() {
        Title = title,
        Description = "今晚的碗我来洗",
        Price = price,
        IsPrivate = isPrivate,
        RedeemMode = ShopRedeemMode.MutualConfirm,
        ListingDays = 30,
        ValidDays = 30
    };

    private static async Task<int> RedeemedItemAsync(SqliteHarness harness, int sellerId, int buyerId, ShopRedeemMode mode) {
        await GiveAsync(harness, buyerId, 100);

        var shop = Shop(harness);
        var wish = Wish(price: 30);
        wish.RedeemMode = mode;
        _ = await shop.PublishAsync(wish, sellerId);

        var item = await harness.Db.ShopItems.AsNoTracking().SingleAsync();
        _ = await shop.PurchaseAsync(item.Id, buyerId);
        return item.Id;
    }

    private static async Task GiveAsync(SqliteHarness harness, int userId, int amount) {
        _ = harness.Db.HeartPointEntries.Add(new HeartPointEntry {
            UserId = userId,
            ChangeAmount = amount,
            Reason = HeartPointReason.DailyHeartbeat,
            SourceKey = "seed:" + Guid.NewGuid().ToString("n")[..8],
            Note = "测试进项"
        });

        _ = await harness.Db.SaveChangesAsync();
    }

    private static async Task<ShopItemStatus> StatusAsync(SqliteHarness harness, int itemId) =>
        (await harness.Db.ShopItems.AsNoTracking().SingleAsync(item => item.Id == itemId)).Status;

    private static HeartPointService Points(SqliteHarness harness) =>
        new(harness.Db, new SettingsStub(), TestDoubles.Clock());

    private static ShopService Shop(SqliteHarness harness) =>
        new(harness.Db, new SettingsStub(), Points(harness), TestDoubles.Notifications(), TestDoubles.Clock());

    #endregion
}
