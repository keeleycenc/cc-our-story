// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.HeartPoints;
using OurStory.Services.Shop;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Shop;

/// <summary>
/// 心意商城的后台
/// </summary>
public class IndexModel(IShopService shop, IHeartPointService heartPoints) : PageModel {
    private const int PageSize = PageNumbers.AdminPageSize;

    /// <summary>
    /// 获取或设置新建预设的表单
    /// </summary>
    [BindProperty]
    public PresetInput Preset { get; set; } = new();

    /// <summary>
    /// 获取当前这一页的心愿
    /// </summary>
    public PagedList<ShopItemCard> Items { get; private set; } = PagedList<ShopItemCard>.Empty(PageSize);

    /// <summary>
    /// 获取全部心愿预设，停用的也在里面
    /// </summary>
    public IReadOnlyList<ShopPreset> Presets { get; private set; } = [];

    /// <summary>
    /// 获取两个人的心意余额
    /// </summary>
    public IReadOnlyList<HeartPointBalance> Balances { get; private set; } = [];

    /// <summary>
    /// 获取当前筛选的发布者
    /// </summary>
    public UserRole? Seller { get; private set; }

    /// <summary>
    /// 获取当前筛选的状态
    /// </summary>
    public ShopItemStatus? Status { get; private set; }

    /// <summary>
    /// 获取或设置保存预设失败时的原因
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(string? seller, string? status, CancellationToken cancellationToken) =>
        await LoadAsync(seller, status, cancellationToken);

    /// <summary>
    /// 新建一个预设
    /// </summary>
    public async Task<IActionResult> OnPostPresetAsync(CancellationToken cancellationToken) {
        if (!ModelState.IsValid) {
            await LoadAsync(null, null, cancellationToken);
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            return Page();
        }

        _ = await shop.CreatePresetAsync(
            new ShopPresetEditModel {
                Title = Preset.Title,
                Description = Preset.Description ?? string.Empty,
                CoverUrl = Preset.CoverUrl,
                RedeemMode = Preset.RedeemMode,
                SortOrder = Preset.SortOrder
            },
            cancellationToken);

        TempData["Flash"] = "预设已经建好了。";
        return Redirect("/admin/shop");
    }

    /// <summary>
    /// 启用或停用一个预设
    /// </summary>
    public async Task<IActionResult> OnPostTogglePresetAsync(int id, bool active, CancellationToken cancellationToken) {
        _ = await shop.SetPresetActiveAsync(id, active, cancellationToken);
        TempData["Flash"] = active ? "预设已启用" : "预设已经停用";
        return Redirect("/admin/shop");
    }

    /// <summary>
    /// 删除一个预设，已经发出去的心愿不受影响
    /// </summary>
    public async Task<IActionResult> OnPostDeletePresetAsync(int id, CancellationToken cancellationToken) {
        _ = await shop.DeletePresetAsync(id, cancellationToken);
        TempData["Flash"] = "预设已删除，之前发布的心愿不受影响";
        return Redirect("/admin/shop");
    }

    /// <summary>
    /// 拼当前筛选下的地址，页面上的筛选按钮用它
    /// </summary>
    public string FilterUrl(UserRole? seller, ShopItemStatus? status) {
        var query = new List<string>(2);
        if (seller is { } role) {
            query.Add($"seller={role.ToString().ToLowerInvariant()}");
        }

        if (status is { } value) {
            query.Add($"status={value.ToString().ToLowerInvariant()}");
        }

        return query.Count == 0 ? "/admin/shop" : $"/admin/shop?{string.Join('&', query)}";
    }

    private async Task LoadAsync(string? seller, string? status, CancellationToken cancellationToken) {
        Seller = ParseSeller(seller);
        Status = Enum.TryParse<ShopItemStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;

        var page = Request.PageNumber();
        Items = await shop.GetPageAsync(
            new ShopQuery(page, PageSize, Seller, Status),
            new ShopViewer(User.Role(), User.UserId()),
            cancellationToken);

        Presets = await shop.GetPresetsAsync(activeOnly: false, cancellationToken);
        Balances = await heartPoints.GetBalancesAsync(cancellationToken);
    }

    private static UserRole? ParseSeller(string? value) => value?.ToLowerInvariant() switch {
        "boy" => UserRole.Boy,
        "girl" => UserRole.Girl,
        _ => null
    };

    /// <summary>
    /// 心愿预设的表单
    /// </summary>
    public class PresetInput {
        /// <summary>
        /// 获取或设置心愿名称
        /// </summary>
        [Required(ErrorMessage = "请填写心愿名称")]
        [StringLength(60)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置心愿描述
        /// </summary>
        [StringLength(300)]
        public string? Description { get; set; }

        /// <summary>
        /// 获取或设置封面图片地址
        /// </summary>
        [StringLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>
        /// 获取或设置建议的核销方式
        /// </summary>
        public ShopRedeemMode RedeemMode { get; set; } = ShopRedeemMode.MutualConfirm;

        /// <summary>
        /// 获取或设置排序值，小的排在前面
        /// </summary>
        [Range(0, 9999, ErrorMessage = "排序值要在 0 到 9999 之间")]
        public int SortOrder { get; set; }
    }
}
