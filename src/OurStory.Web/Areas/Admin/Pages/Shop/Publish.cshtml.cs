// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.HeartPoints;
using OurStory.Services.Settings;
using OurStory.Services.Shop;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Shop;

/// <summary>
/// 发布一件心愿
/// </summary>
public class PublishModel(IShopService shop, IHeartPointService heartPoints, ISettingsService settings) : PageModel {
    /// <summary>
    /// 获取或设置表单输入
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取站点设置，价格上下限和默认天数都从这里来
    /// </summary>
    public SiteSettings Site { get; private set; } = new();

    /// <summary>
    /// 获取可以选的心愿预设
    /// </summary>
    public IReadOnlyList<ShopPreset> Presets { get; private set; } = [];

    /// <summary>
    /// 获取自己现在有多少心意
    /// </summary>
    public int Balance { get; private set; }

    /// <summary>
    /// 获取或设置发布没成功时的原因
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        await LoadAsync(cancellationToken);

        Input.Price = Math.Clamp(30, Site.ShopPriceMin, Site.ShopPriceMax);
        Input.ListingDays = Site.ShopListingDays;
        Input.ValidDays = Site.ShopValidDays;
    }

    /// <summary>
    /// 保存表单
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        await LoadAsync(cancellationToken);

        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            return Page();
        }

        if (User.UserId() is not { } sellerId) {
            return Forbid();
        }

        var result = await shop.PublishAsync(
            new ShopPublishModel {
                Title = Input.Title,
                Description = Input.Description ?? string.Empty,
                CoverUrl = Input.CoverUrl,
                Price = Input.Price,
                IsPrivate = Input.IsPrivate,
                RedeemMode = Input.RedeemMode,
                ListingDays = Input.ListingDays,
                ValidDays = Input.ValidDays,
                PresetId = Input.PresetId
            },
            sellerId,
            cancellationToken);

        if (!result.Success) {
            Error = result.Message;
            return Page();
        }

        TempData["Flash"] = result.Message;
        return Redirect("/admin/shop");
    }

    private async Task LoadAsync(CancellationToken cancellationToken) {
        Site = await settings.GetAsync(cancellationToken);
        Presets = await shop.GetPresetsAsync(activeOnly: true, cancellationToken);
        Balance = User.UserId() is { } id ? await heartPoints.GetBalanceAsync(id, cancellationToken) : 0;
    }

    /// <summary>
    /// 心愿发布表单
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置心愿名称
        /// </summary>
        [Required(ErrorMessage = "心愿得有个名字哦~")]
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
        /// 获取或设置兑换需要的心意
        /// </summary>
        [Range(1, 99999, ErrorMessage = "标价范围不合法，范围 1 ~ 99999")]
        public int Price { get; set; } = 30;

        /// <summary>
        /// 获取或设置是否仅情侣双方可见
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        /// 获取或设置核销方式
        /// </summary>
        public ShopRedeemMode RedeemMode { get; set; } = ShopRedeemMode.MutualConfirm;

        /// <summary>
        /// 获取或设置上架有效期天数
        /// </summary>
        [Range(1, 3650, ErrorMessage = "上架时间要在 1 到 3650 天之间")]
        public int ListingDays { get; set; } = 30;

        /// <summary>
        /// 获取或设置兑换后的有效期天数
        /// </summary>
        [Range(1, 3650, ErrorMessage = "过期时间要在 1 到 3650 天之间")]
        public int ValidDays { get; set; } = 30;

        /// <summary>
        /// 获取或设置选中的预设
        /// </summary>
        public int? PresetId { get; set; }
    }
}
