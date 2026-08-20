// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Entities;
using OurStory.Core.Models;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace OurStory.Web.Areas.Admin.Pages;

/// <summary>
/// 表示 NotificationsModel
/// </summary>
/// <remarks>
/// 通知是各管各的：这一页上的每一项都只影响当前登录的这个人，
/// 对方的开关、对方的设备，在这里既看不到也改不了
/// </remarks>
public class NotificationsModel(
    INotificationService notifications,
    ISettingsService settings) : PageModel {
    /// <summary>
    /// 执行 Input 操作
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取或设置 Error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 获取这个账号已经授权过的设备
    /// </summary>
    public IReadOnlyList<PushDeviceCard> Devices { get; private set; } = [];

    /// <summary>
    /// 获取一个值，指示站点的 VAPID 密钥有没有备好
    /// </summary>
    public bool IsConfigured => notifications.IsConfigured;

    /// <summary>
    /// 获取对方在站点上的称呼，「发一句话给对方」那一块要用
    /// </summary>
    public string PartnerName { get; private set; } = "对方";

    /// <summary>
    /// 获取一个值，指示站点上还有没有另一个账号
    /// </summary>
    public bool HasPartner { get; private set; }

    /// <summary>
    /// 获取对方那头的通知状态：开没开、有几台设备
    /// </summary>
    public PartnerReadiness Partner { get; private set; } = PartnerReadiness.None;

    /// <summary>
    /// 获取对方当前收不到通知的原因；能收到时是 null
    /// </summary>
    public string? PartnerBlockedReason => !HasPartner
        ? "还没有绑定另一个账号哦"
        : !Partner.Enabled
            ? $"{PartnerName} 还没开启通知，将无法接收"
            : Partner.Devices == 0
                ? $"{PartnerName} 已经开启通知啦，但还没有设备绑定，先去授权一下吧"
                : null;

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) {
        if (User.UserId() is not { } userId) {
            return Forbid();
        }

        var setting = await notifications.GetSettingAsync(userId, cancellationToken);
        var preferences = NotificationPreferences.From(setting);

        Input = new InputModel {
            Enabled = preferences.Enabled,
            Moments = preferences.Moments,
            Anniversaries = preferences.Anniversaries,
            Shop = preferences.Shop,
            MissYou = preferences.MissYou,
            Comments = preferences.Comments,
            Affinity = preferences.Affinity,
            RemindAt = ToText(preferences.RemindMinutes)
        };

        await LoadAsync(userId, cancellationToken);
        return Page();
    }

    /// <summary>
    /// 处理 Async(CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        if (User.UserId() is not { } userId) {
            return Forbid();
        }

        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            await LoadAsync(userId, cancellationToken);
            return Page();
        }

        await notifications.SaveSettingAsync(
            userId,
            new NotificationPreferences {
                Enabled = Input.Enabled,
                Moments = Input.Moments,
                Anniversaries = Input.Anniversaries,
                Shop = Input.Shop,
                MissYou = Input.MissYou,
                Comments = Input.Comments,
                Affinity = Input.Affinity,
                RemindMinutes = ToMinutes(Input.RemindAt)
            },
            cancellationToken);

        TempData["Flash"] = "通知设置已经保存。";
        return RedirectToPage();
    }

    /// <summary>
    /// 处理 RemoveDevice(long, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostRemoveDeviceAsync(long deviceId, CancellationToken cancellationToken) {
        if (User.UserId() is not { } userId) {
            return Forbid();
        }

        TempData["Flash"] = await notifications.RemoveDeviceAsync(userId, deviceId, cancellationToken)
            ? "这台设备已经不再接收通知啦"
            : "没找到这台设备，可能已经被移除啦";

        return RedirectToPage();
    }

    #region 私有方法

    private async Task LoadAsync(int userId, CancellationToken cancellationToken) {
        Devices = await notifications.GetDevicesAsync(userId, cancellationToken);
        HasPartner = await notifications.GetPartnerIdAsync(userId, cancellationToken) is not null;

        if (!HasPartner) {
            return;
        }

        // 只有两行用户，「对方」就是另一个身份
        var site = await settings.GetAsync(cancellationToken);
        PartnerName = site.RoleName(User.Role() == UserRole.Boy ? UserRole.Girl : UserRole.Boy);
        Partner = await notifications.GetPartnerReadinessAsync(userId, cancellationToken);
    }

    private static string ToText(int minutes) =>
        TimeSpan.FromMinutes(Math.Clamp(minutes, 0, 1439)).ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static int ToMinutes(string? text) =>
        TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed.Hour * 60) + parsed.Minute
            : NotificationSetting.DefaultRemindMinutes;

    #endregion

    /// <summary>
    /// 表示 InputModel
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置通知服务的总开关
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 获取或设置是否接收点点滴滴的通知
        /// </summary>
        public bool Moments { get; set; } = true;

        /// <summary>
        /// 获取或设置是否接收纪念日的通知
        /// </summary>
        public bool Anniversaries { get; set; } = true;

        /// <summary>
        /// 获取或设置是否接收心意商城的通知
        /// </summary>
        public bool Shop { get; set; } = true;

        /// <summary>
        /// 获取或设置对方点了想你时要不要提醒
        /// </summary>
        public bool MissYou { get; set; } = true;

        /// <summary>
        /// 获取或设置点点滴滴下面来了新留言时要不要提醒
        /// </summary>
        public bool Comments { get; set; } = true;

        /// <summary>
        /// 获取或设置是否接收心有灵犀答题提醒
        /// </summary>
        public bool Affinity { get; set; } = true;

        /// <summary>
        /// 获取或设置纪念日提醒时间，形如 21:00，按站点时区理解
        /// </summary>
        [Required(ErrorMessage = "提醒时间不能为空")]
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "提醒时间要写成 21:00 这样")]
        public string RemindAt { get; set; } = "21:00";
    }
}
