// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Data;
using OurStory.Services.HeartPoints;
using OurStory.Services.Settings;

namespace OurStory.Services.Shop;

internal class ShopService(
    OurStoryDbContext db,
    ISettingsService settings,
    IHeartPointService heartPoints,
    SiteClock clock) : IShopService {
    private const int DefaultPageSize = 12;

    public async Task<PagedList<ShopItemCard>> GetPageAsync(ShopQuery query, ShopViewer viewer, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(viewer);

        _ = await SweepExpiredAsync(cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = PageSizeOf(query);
        var source = Filter(query, viewer);

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            // 还能兑换的排在最前面，剩下的按发布时间倒着来
            .OrderBy(item => item.Status == ShopItemStatus.Listed ? 0 : 1)
            .ThenByDescending(item => item.ListedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(item => item.Seller)
            .Include(item => item.Buyer)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var site = await settings.GetAsync(cancellationToken);
        return new PagedList<ShopItemCard>([.. items.Select(item => ToCard(item, site))], page, pageSize, total);
    }

    public async Task<int> FindPageAsync(int itemId, ShopQuery query, ShopViewer viewer, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(viewer);

        _ = await SweepExpiredAsync(cancellationToken);

        var source = Filter(query, viewer);
        var target = await source
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);

        if (target is null) {
            return 0;
        }

        var group = target.Status == ShopItemStatus.Listed ? 0 : 1;
        var ahead = await source.CountAsync(
            item => (item.Status == ShopItemStatus.Listed ? 0 : 1) < group
                || ((item.Status == ShopItemStatus.Listed ? 0 : 1) == group
                    && (item.ListedAt > target.ListedAt
                        || (item.ListedAt == target.ListedAt && item.Id > target.Id))),
            cancellationToken);

        return (ahead / PageSizeOf(query)) + 1;
    }

    public async Task<IReadOnlyList<ShopItemCard>> GetWarehouseAsync(int userId, CancellationToken cancellationToken = default) {
        _ = await SweepExpiredAsync(cancellationToken);

        var items = await db.ShopItems
            .Where(item => item.BuyerId == userId)
            .Include(item => item.Seller)
            .Include(item => item.Buyer)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var site = await settings.GetAsync(cancellationToken);
        return Sort(items.Select(item => ToCard(item, site)));
    }

    public async Task<IReadOnlyList<ShopItemCard>> GetPromisesAsync(int userId, CancellationToken cancellationToken = default) {
        _ = await SweepExpiredAsync(cancellationToken);

        var items = await db.ShopItems
            .Where(item => item.SellerId == userId && item.BuyerId != null)
            .Include(item => item.Seller)
            .Include(item => item.Buyer)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var site = await settings.GetAsync(cancellationToken);
        return Sort(items.Select(item => ToCard(item, site)));
    }

    public async Task<int> CountOnSaleAsync(ShopViewer viewer, CancellationToken cancellationToken = default) {
        _ = await SweepExpiredAsync(cancellationToken);
        return await Visible(viewer).CountAsync(item => item.Status == ShopItemStatus.Listed, cancellationToken);
    }

    public async Task<ShopActionResult> PublishAsync(ShopPublishModel model, int sellerId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);

        var site = await settings.GetAsync(cancellationToken);
        var title = (model.Title ?? string.Empty).Trim();
        if (title.Length == 0) {
            return ShopActionResult.Fail("心愿得有个名字。");
        }

        if (model.Price < site.ShopPriceMin || model.Price > site.ShopPriceMax) {
            return ShopActionResult.Fail($"标价要在 {site.ShopPriceMin} 到 {site.ShopPriceMax} 心意之间。");
        }

        var listingDays = Math.Clamp(model.ListingDays, 1, 3650);
        var validDays = Math.Clamp(model.ValidDays, 1, 3650);

        // 预设只在发布这一刻起作用：内容抄一份进商品，之后改预设不会牵动已经发出去的心愿
        var presetId = model.PresetId is { } id && await db.ShopPresets.AnyAsync(preset => preset.Id == id, cancellationToken)
            ? id
            : (int?)null;

        var now = SiteClock.UtcNow;
        var item = new ShopItem {
            Title = title,
            Description = (model.Description ?? string.Empty).Trim(),
            CoverUrl = Trim(model.CoverUrl),
            Price = model.Price,
            IsPrivate = model.IsPrivate,
            RedeemMode = model.RedeemMode,
            Status = ShopItemStatus.Listed,
            SellerId = sellerId,
            PresetId = presetId,
            ListingDays = listingDays,
            ValidDays = validDays,
            ListedAt = now,
            ListingExpiresAt = now.AddDays(listingDays),
            CreatedAt = now,
            UpdatedAt = now
        };

        _ = db.ShopItems.Add(item);
        _ = await db.SaveChangesAsync(cancellationToken);
        return ShopActionResult.Ok($"「{item.Title}」已经上架，{listingDays} 天内等对方来兑换。");
    }

    public async Task<ShopActionResult> PurchaseAsync(int itemId, int buyerId, CancellationToken cancellationToken = default) {
        // 余额和状态都得在同一笔事务里定下来，否则连点两下就可能扣一次心意换两件东西
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var item = await db.ShopItems.FirstOrDefaultAsync(value => value.Id == itemId, cancellationToken);
        if (item is null) {
            return ShopActionResult.Fail("这件心愿不在了");
        }

        if (item.SellerId == buyerId) {
            return ShopActionResult.Fail("自己发布的心愿不能自己兑换");
        }

        if (item.Status != ShopItemStatus.Listed) {
            return ShopActionResult.Fail("这件心愿已经不在货架上了");
        }

        var now = SiteClock.UtcNow;
        if (item.ListingExpiresAt <= now) {
            item.Status = ShopItemStatus.ListingExpired;
            item.UpdatedAt = now;
            _ = await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ShopActionResult.Fail("这件心愿的上架时间已经过了，让对方重新发一次吧");
        }

        var balance = await heartPoints.GetBalanceAsync(buyerId, cancellationToken);
        if (balance < item.Price) {
            return ShopActionResult.Fail($"心意还差一点，这件需要 {item.Price}，你现在有 {balance}。");
        }

        item.Status = ShopItemStatus.Redeemed;
        item.BuyerId = buyerId;
        item.PurchasedAt = now;
        item.ExpiresAt = now.AddDays(item.ValidDays);
        item.UpdatedAt = now;

        _ = db.HeartPointEntries.Add(new HeartPointEntry {
            UserId = buyerId,
            ChangeAmount = -item.Price,
            Reason = HeartPointReason.Purchase,
            SourceKey = $"purchase:{item.Id}",
            Note = $"兑换「{item.Title}」",
            CreatedAt = now
        });

        _ = await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ShopActionResult.Ok($"「{item.Title}」已放入心愿仓库，可在 {item.ValidDays} 天内使用");
    }

    public async Task<ShopActionResult> RequestRedeemAsync(int itemId, int userId, CancellationToken cancellationToken = default) {
        var item = await db.ShopItems.FirstOrDefaultAsync(value => value.Id == itemId && value.BuyerId == userId, cancellationToken);
        if (item is null) {
            return ShopActionResult.Fail("这件心愿不在你的仓库里");
        }

        if (await ExpireIfDueAsync(item, cancellationToken)) {
            return ShopActionResult.Fail($"「{item.Title}」已过期，无法恢复使用");
        }

        if (item.Status != ShopItemStatus.Redeemed) {
            return ShopActionResult.Fail(item.Status == ShopItemStatus.PendingConfirm
                ? "已发起使用，请等待对方确认"
                : "这件心愿暂时无法使用");
        }

        var now = SiteClock.UtcNow;
        item.RedeemRequestedAt = now;
        item.UpdatedAt = now;

        if (item.RedeemMode == ShopRedeemMode.Instant) {
            item.Status = ShopItemStatus.Used;
            item.UsedAt = now;
            _ = await db.SaveChangesAsync(cancellationToken);
            return ShopActionResult.Ok($"「{item.Title}」已使用");
        }

        item.Status = ShopItemStatus.PendingConfirm;
        _ = await db.SaveChangesAsync(cancellationToken);
        return ShopActionResult.Ok($"「{item.Title}」已发起使用，待对方确认后完成核销");
    }

    public async Task<ShopActionResult> ConfirmRedeemAsync(int itemId, int userId, CancellationToken cancellationToken = default) {
        var item = await db.ShopItems.FirstOrDefaultAsync(value => value.Id == itemId && value.SellerId == userId, cancellationToken);
        if (item is null) {
            return ShopActionResult.Fail("这不是你发布的心愿");
        }

        if (await ExpireIfDueAsync(item, cancellationToken)) {
            return ShopActionResult.Fail($"「{item.Title}」已过期，无法恢复");
        }

        if (item.Status != ShopItemStatus.PendingConfirm) {
            return ShopActionResult.Fail("当前没有待确认的核销记录");
        }

        var now = SiteClock.UtcNow;
        item.Status = ShopItemStatus.Used;
        item.UsedAt = now;
        item.UpdatedAt = now;
        _ = await db.SaveChangesAsync(cancellationToken);

        return ShopActionResult.Ok($"「{item.Title}」已确认完成");
    }

    public async Task<ShopActionResult> CancelRedeemAsync(int itemId, int userId, CancellationToken cancellationToken = default) {
        var item = await db.ShopItems
            .FirstOrDefaultAsync(value => value.Id == itemId && (value.SellerId == userId || value.BuyerId == userId), cancellationToken);

        if (item is null) {
            return ShopActionResult.Fail("这件心愿和你没有关系");
        }

        if (await ExpireIfDueAsync(item, cancellationToken)) {
            return ShopActionResult.Fail($"「{item.Title}」已过期，无法恢复");
        }

        if (item.Status != ShopItemStatus.PendingConfirm) {
            return ShopActionResult.Fail("当前没有待确认的核销记录");
        }

        var now = SiteClock.UtcNow;
        item.Status = ShopItemStatus.Redeemed;
        item.RedeemRequestedAt = null;
        item.UpdatedAt = now;
        _ = await db.SaveChangesAsync(cancellationToken);

        return ShopActionResult.Ok($"「{item.Title}」已退回心愿仓库，可稍后再使用");
    }

    public async Task<int> SweepExpiredAsync(CancellationToken cancellationToken = default) {
        var now = SiteClock.UtcNow;

        var due = await db.ShopItems.AnyAsync(
            item => (item.Status == ShopItemStatus.Listed && item.ListingExpiresAt <= now)
                || ((item.Status == ShopItemStatus.Redeemed || item.Status == ShopItemStatus.PendingConfirm)
                    && item.ExpiresAt != null
                    && item.ExpiresAt <= now),
            cancellationToken);

        if (!due) {
            return 0;
        }

        var unsold = await db.ShopItems
            .Where(item => item.Status == ShopItemStatus.Listed && item.ListingExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, ShopItemStatus.ListingExpired)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        var stale = await db.ShopItems
            .Where(item => (item.Status == ShopItemStatus.Redeemed || item.Status == ShopItemStatus.PendingConfirm)
                && item.ExpiresAt != null
                && item.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, ShopItemStatus.Expired)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        return unsold + stale;
    }

    public async Task<IReadOnlyList<ShopPreset>> GetPresetsAsync(bool activeOnly, CancellationToken cancellationToken = default) {
        var query = activeOnly ? db.ShopPresets.Where(preset => preset.IsActive) : db.ShopPresets;
        return await query
            .OrderBy(preset => preset.SortOrder)
            .ThenBy(preset => preset.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ShopPreset> CreatePresetAsync(ShopPresetEditModel model, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);

        var preset = new ShopPreset {
            Title = model.Title.Trim(),
            Description = (model.Description ?? string.Empty).Trim(),
            CoverUrl = Trim(model.CoverUrl),
            RedeemMode = model.RedeemMode,
            SortOrder = model.SortOrder,
            IsActive = true,
            CreatedAt = SiteClock.UtcNow
        };

        _ = db.ShopPresets.Add(preset);
        _ = await db.SaveChangesAsync(cancellationToken);
        return preset;
    }

    public async Task<bool> SetPresetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default) {
        var preset = await db.ShopPresets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (preset is null) {
            return false;
        }

        preset.IsActive = isActive;
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeletePresetAsync(int id, CancellationToken cancellationToken = default) {
        var deleted = await db.ShopPresets.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    #region 私有方法

    private static int PageSizeOf(ShopQuery query) => query.PageSize < 1 ? DefaultPageSize : query.PageSize;

    private IQueryable<ShopItem> Visible(ShopViewer viewer) =>
        viewer.IsOwner ? db.ShopItems : db.ShopItems.Where(item => !item.IsPrivate);

    private IQueryable<ShopItem> Filter(ShopQuery query, ShopViewer viewer) {
        var source = Visible(viewer);
        if (query.Seller is { } seller) {
            source = source.Where(item => item.Seller!.Role == seller);
        }

        if (query.EndedOnly) {
            source = source.Where(item =>
                item.Status == ShopItemStatus.ListingExpired
                || item.Status == ShopItemStatus.Expired);
        } else if (query.Status is { } status) {
            source = source.Where(item => item.Status == status);
        }

        return source;
    }

    private async Task<bool> ExpireIfDueAsync(ShopItem item, CancellationToken cancellationToken) {
        if (item.Status is not (ShopItemStatus.Redeemed or ShopItemStatus.PendingConfirm)) {
            return item.Status == ShopItemStatus.Expired;
        }

        var now = SiteClock.UtcNow;
        if (item.ExpiresAt is not { } due || due > now) {
            return false;
        }

        item.Status = ShopItemStatus.Expired;
        item.UpdatedAt = now;
        _ = await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IReadOnlyList<ShopItemCard> Sort(IEnumerable<ShopItemCard> cards) =>
        [.. cards
            .OrderBy(card => card.Status switch {
                ShopItemStatus.PendingConfirm => 0,
                ShopItemStatus.Redeemed => 1,
                ShopItemStatus.Listed => 2,
                _ => 3
            })
            .ThenBy(card => card.DaysLeft ?? int.MaxValue)
            .ThenByDescending(card => card.Id)];

    private ShopItemCard ToCard(ShopItem item, SiteSettings site) {
        var deadline = item.Status switch {
            ShopItemStatus.Listed => item.ListingExpiresAt,
            ShopItemStatus.Redeemed or ShopItemStatus.PendingConfirm => item.ExpiresAt,
            _ => null
        };

        var sellerRole = item.Seller?.Role ?? UserRole.Guest;
        var buyerRole = item.Buyer?.Role;

        return new ShopItemCard(
            item.Id,
            item.Title,
            item.Description,
            item.CoverUrl ?? string.Empty,
            item.Price,
            item.IsPrivate,
            item.RedeemMode,
            item.Status,
            sellerRole,
            site.RoleName(sellerRole),
            buyerRole,
            buyerRole is { } role ? site.RoleName(role) : null,
            clock.ToLocal(item.ListedAt),
            deadline is { } due ? clock.ToLocal(due) : null,
            DaysLeft(deadline),
            item.UsedAt is { } used ? clock.ToLocal(used) : null);
    }

    private static int? DaysLeft(DateTimeOffset? deadline) {
        if (deadline is not { } due) {
            return null;
        }

        var remaining = due - SiteClock.UtcNow;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalDays);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    #endregion
}
