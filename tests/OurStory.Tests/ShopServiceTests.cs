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

/// <summary>心愿商品的状态机与兑换规则。</summary>
public class ShopServiceTests {
    /// <summary>标价必须落在站点设置的区间里。</summary>
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

    /// <summary>自己发的心愿自己换不走。</summary>
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

    /// <summary>心意不够就换不了，也不该扣掉一部分。</summary>
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

    /// <summary>兑换成功后心意直接销毁，一分不进发布者的账。</summary>
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

    /// <summary>已经被换走的心愿不会再被换第二次。</summary>
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

    /// <summary>双方确认：持有人发起后进待履约，发布者点头才是已使用。</summary>
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

    /// <summary>立即使用：持有人按一下当场就是已使用。</summary>
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

    /// <summary>进了已使用就回不去了，谁按都一样。</summary>
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

    /// <summary>待履约可以退回仓库，两边都能按，退回后还能再发起。</summary>
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

    /// <summary>上架期满没人兑换就自动下架，之后也换不了了。</summary>
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

    /// <summary>兑换后拖过有效期就作废，待履约的也一样，不能再使用。</summary>
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

    /// <summary>「仅双方可见」的心愿不出现在访客那一份列表里。</summary>
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

    /// <summary>删掉预设不该动到已经用它发出去的心愿。</summary>
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

    /// <summary>发一件、换一件，返回这件心愿的编号。</summary>
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

    /// <summary>直接塞一笔进项，省得为了攒心意去点一堆爱心。</summary>
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
        new(harness.Db, new SettingsStub(), Points(harness), TestDoubles.Clock());

    #endregion
}
