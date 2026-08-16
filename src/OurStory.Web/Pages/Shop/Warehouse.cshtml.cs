// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;
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
/// 心愿仓库
/// </summary>
[Authorize]
public class WarehouseModel(
    IShopService shop,
    IHeartPointService heartPoints,
    ISettingsService settingsService) : PageModel {
    private const int LedgerPageSize = 20;

    /// <summary>
    /// 获取站点设置
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 获取自己兑换到手的心愿
    /// </summary>
    public IReadOnlyList<ShopItemCard> Holdings { get; private set; } = [];

    /// <summary>
    /// 获取要由自己去履约的心愿
    /// </summary>
    public IReadOnlyList<ShopItemCard> Promises { get; private set; } = [];

    /// <summary>
    /// 获取自己的心意账单
    /// </summary>
    public PagedList<HeartPointRecord> Ledger { get; private set; } = PagedList<HeartPointRecord>.Empty(LedgerPageSize);

    /// <summary>
    /// 获取自己现在有多少心意
    /// </summary>
    public int Balance { get; private set; }

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
    public async Task<IActionResult> OnGetAsync(int? focus, CancellationToken cancellationToken) {
        Flash = TempData["Flash"] as string;
        Error = TempData["ShopError"] as string;

        var result = await LoadAsync(cancellationToken);

        if (focus is { } wish
            && (Holdings.Any(item => item.Id == wish)
                || Promises.Any(item => item.Id == wish && item.Status == ShopItemStatus.PendingConfirm))) {
            Focus = wish;
        }

        return result;
    }

    /// <summary>
    /// 持有人发起使用
    /// </summary>
    public Task<IActionResult> OnPostUseAsync(int id, CancellationToken cancellationToken) =>
        ActAsync(userId => shop.RequestRedeemAsync(id, userId, cancellationToken));

    /// <summary>
    /// 发布者确认已经做到了
    /// </summary>
    public Task<IActionResult> OnPostConfirmAsync(int id, CancellationToken cancellationToken) =>
        ActAsync(userId => shop.ConfirmRedeemAsync(id, userId, cancellationToken));

    /// <summary>
    /// 撤回一次核销申请
    /// </summary>
    public Task<IActionResult> OnPostCancelAsync(int id, CancellationToken cancellationToken) =>
        ActAsync(userId => shop.CancelRedeemAsync(id, userId, cancellationToken));

    private async Task<IActionResult> ActAsync(Func<int, Task<ShopActionResult>> action) {
        if (User.UserId() is not { } userId) {
            return Forbid();
        }

        var result = await action(userId);
        TempData[result.Success ? "Flash" : "ShopError"] = result.Message;
        return Redirect("/shop/warehouse");
    }

    private async Task<IActionResult> LoadAsync(CancellationToken cancellationToken) {
        if (User.UserId() is not { } userId) {
            return Forbid();
        }

        Site = await settingsService.GetAsync(cancellationToken);
        Holdings = await shop.GetWarehouseAsync(userId, cancellationToken);
        Promises = await shop.GetPromisesAsync(userId, cancellationToken);
        Balance = await heartPoints.GetBalanceAsync(userId, cancellationToken);
        Ledger = await heartPoints.GetRecordsAsync(userId, Request.PageNumber(), LedgerPageSize, cancellationToken);
        return Page();
    }
}
