// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.HeartPoints;
using OurStory.Services.Settings;
using OurStory.Services.Shop;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Pages.Shop;

/// <summary>
/// 心意商城
/// </summary>
public class IndexModel(
    IShopService shop,
    IHeartPointService heartPoints,
    ISettingsService settingsService) : PageModel {
    private const int PageSize = 12;

    /// <summary>
    /// 获取站点设置
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 获取当前这一页的心愿
    /// </summary>
    public PagedList<ShopItemCard> Items { get; private set; } = PagedList<ShopItemCard>.Empty(PageSize);

    /// <summary>
    /// 获取自己现在有多少心意
    /// </summary>
    public int Balance { get; private set; }

    /// <summary>
    /// 获取当前筛选的发布者
    /// </summary>
    public UserRole? Seller { get; private set; }

    /// <summary>
    /// 获取上架中的心愿有几件
    /// </summary>
    public int OnSaleCount { get; private set; }

    /// <summary>
    /// 获取要定位过去的心愿 ID，为空表示这次不用定位
    /// </summary>
    public int? Focus { get; private set; }

    /// <summary>
    /// 获取或设置操作之后要说的一句话
    /// </summary>
    public string? Flash { get; private set; }

    /// <summary>
    /// 获取或设置操作没做成的原因
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(string? seller, int? focus, CancellationToken cancellationToken) {
        Flash = TempData["Flash"] as string;
        Error = TempData["ShopError"] as string;
        await LoadAsync(seller, focus, cancellationToken);
    }

    /// <summary>
    /// 兑换一件心愿
    /// </summary>
    public async Task<IActionResult> OnPostBuyAsync(int id, string? seller, CancellationToken cancellationToken) {
        if (User.UserId() is not { } buyerId) {
            return Forbid();
        }

        var result = await shop.PurchaseAsync(id, buyerId, cancellationToken);
        TempData[result.Success ? "Flash" : "ShopError"] = result.Message;
        return Redirect(FilterUrl(ParseSeller(seller)));
    }

    /// <summary>
    /// 拼当前筛选下的地址
    /// </summary>
    public string FilterUrl(UserRole? seller) =>
        seller is { } role ? $"/shop?seller={role.ToString().ToLowerInvariant()}" : "/shop";

    /// <summary>
    /// 这件心愿当前访问者能不能兑换
    /// </summary>
    public bool CanBuy(ShopItemCard card) {
        ArgumentNullException.ThrowIfNull(card);
        return User.IsOwner() && card.IsOnSale && card.SellerRole != User.Role() && Balance >= card.Price;
    }

    /// <summary>
    /// 不能兑换的时候，卡片上说一句为什么
    /// </summary>
    public string BuyHint(ShopItemCard card) {
        ArgumentNullException.ThrowIfNull(card);

        if (!User.IsOwner()) {
            return "登录后才能兑换";
        }

        if (card.SellerRole == User.Role()) {
            return "你发布的心愿";
        }

        if (!card.IsOnSale) {
            return ShopDisplay.StatusName(card.Status);
        }

        return $"还差 {card.Price - Balance} 心意";
    }

    private async Task LoadAsync(string? seller, int? focus, CancellationToken cancellationToken) {
        Site = await settingsService.GetAsync(cancellationToken);
        Seller = ParseSeller(seller);

        var viewer = new ShopViewer(User.Role(), User.UserId());
        var page = Request.PageNumber();

        if (focus is { } wish) {
            var found = await shop.FindPageAsync(wish, new ShopQuery(1, PageSize, Seller), viewer, cancellationToken);
            if (found > 0) {
                page = found;
                Focus = wish;
            }
        }

        Items = await shop.GetPageAsync(new ShopQuery(page, PageSize, Seller), viewer, cancellationToken);
        OnSaleCount = await shop.CountOnSaleAsync(viewer, cancellationToken);
        Balance = User.UserId() is { } id ? await heartPoints.GetBalanceAsync(id, cancellationToken) : 0;
    }

    private static UserRole? ParseSeller(string? value) => value?.ToLowerInvariant() switch {
        "boy" => UserRole.Boy,
        "girl" => UserRole.Girl,
        _ => null
    };
}
