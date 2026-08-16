// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages;

/// <summary>
/// 表示 SettingsModel
/// </summary>
/// <remarks>
/// 页面上分两摊：内容类的存数据库（<see cref="ISettingsService"/>），
/// 时区和附件存储这类运行参数存配置文件（<see cref="ActiveConfiguration"/>）
/// </remarks>
public class SettingsModel(ISettingsService settings, ActiveConfiguration configuration) : PageModel {
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
    /// 获取一个值，指示当前登录的人能否改心意规则
    /// </summary>
    /// <remarks>
    /// 心意的发放和价格区间只让男主动：这类站点一般是男生搭给女生的
    /// </remarks>
    public bool CanEditHeartRules => User.Role() == UserRole.Boy;

    /// <summary>
    /// 获取当前实际生效的存储方式：OSS 参数没配全时它会和上面选的不一样</summary>
    public string EffectiveDriverText =>
        configuration.Storage.EffectiveDriver == Core.Options.StorageDriver.AliyunOss ? "阿里云 OSS" : "本地目录";

    /// <summary>
    /// 获取配置文件的位置
    /// </summary>
    public string ConfigFilePath => configuration.FilePath;

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        var site = await settings.GetAsync(cancellationToken);
        var storage = configuration.Storage;

        Input = new InputModel {
            TimeZone = configuration.Site.TimeZone,
            StorageDriver = storage.Driver,
            OssRegion = storage.Oss.Region,
            OssBucket = storage.Oss.Bucket,
            OssAccessKeyId = storage.Oss.AccessKeyId,
            OssAccessKeySecret = storage.Oss.AccessKeySecret,
            OssPublicBaseUrl = storage.Oss.PublicBaseUrl,
            OssApiEndpoint = storage.Oss.ApiEndpoint,
            SiteTitle = site.SiteTitle,
            SiteDescription = site.SiteDescription,
            BoyName = site.BoyName,
            GirlName = site.GirlName,
            BoyAvatar = site.BoyAvatar,
            GirlAvatar = site.GirlAvatar,
            BoySentence = site.BoySentence,
            GirlSentence = site.GirlSentence,
            LoveStartedAt = site.LoveStartedAt,
            HomeSentence = site.HomeSentence,
            DailyNote = site.DailyNote,
            LoveLetters = string.Join('\n', site.LoveLetters),
            ColorMode = site.ColorMode,
            MomentsPageSize = site.MomentsPageSize,
            HeartbeatDailyLimit = site.HeartbeatDailyLimit,
            CommentsRequireMail = site.CommentsRequireMail,
            AllowGuestComments = site.AllowGuestComments,
            RewardVisit = site.RewardVisit,
            RewardHeartbeat = site.RewardHeartbeat,
            RewardMoment = site.RewardMoment,
            RewardAnniversary = site.RewardAnniversary,
            ShopPriceMin = site.ShopPriceMin,
            ShopPriceMax = site.ShopPriceMax,
            ShopListingDays = site.ShopListingDays,
            ShopValidDays = site.ShopValidDays
        };
    }

    /// <summary>
    /// 处理 Async(CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            return Page();
        }

        var site = await settings.GetAsync(cancellationToken);

        site.SiteTitle = Input.SiteTitle;
        site.SiteDescription = Input.SiteDescription;
        site.BoyName = Input.BoyName;
        site.GirlName = Input.GirlName;
        site.BoyAvatar = Input.BoyAvatar ?? string.Empty;
        site.GirlAvatar = Input.GirlAvatar ?? string.Empty;
        site.BoySentence = Input.BoySentence;
        site.GirlSentence = Input.GirlSentence;
        site.LoveStartedAt = Input.LoveStartedAt;
        site.HomeSentence = Input.HomeSentence;
        site.DailyNote = Input.DailyNote;
        site.ColorMode = Input.ColorMode;
        site.MomentsPageSize = Input.MomentsPageSize;
        site.HeartbeatDailyLimit = Input.HeartbeatDailyLimit;
        site.CommentsRequireMail = Input.CommentsRequireMail;
        site.AllowGuestComments = Input.AllowGuestComments;

        if (CanEditHeartRules) {
            if (HeartRuleError() is { } problem) {
                Error = problem;
                return Page();
            }

            site.RewardVisit = Input.RewardVisit;
            site.RewardHeartbeat = Input.RewardHeartbeat;
            site.RewardMoment = Input.RewardMoment;
            site.RewardAnniversary = Input.RewardAnniversary;
            site.ShopPriceMin = Input.ShopPriceMin;
            site.ShopPriceMax = Input.ShopPriceMax;
            site.ShopListingDays = Input.ShopListingDays;
            site.ShopValidDays = Input.ShopValidDays;
        }

        // 情话在后台按「一行一句」编辑，存进去还是 JSON 数组
        var letters = (Input.LoveLetters ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        site.LoveLetters = letters.Count > 0 ? letters : SiteSettings.DefaultLoveLetters;

        await settings.SaveAsync(site, cancellationToken);

        // 时区和附件存储落在配置文件里，写不进去（比如只读挂载）提示
        if (!configuration.Update(SaveRuntimeOptions, out var error)) {
            Error = $"内容已经保存，但 {configuration.FilePath} 无法写入：{error}";
            return Page();
        }

        TempData["Flash"] = "设置已经保存。";
        return RedirectToPage();
    }

    /// <summary>
    /// 心意规则的范围检查，都合规就返回 null
    /// </summary>
    private string? HeartRuleError() {
        if (!InRange(Input.RewardVisit, 0, 100)
            || !InRange(Input.RewardHeartbeat, 0, 100)
            || !InRange(Input.RewardMoment, 0, 100)
            || !InRange(Input.RewardAnniversary, 0, 100)) {
            return "四档心意奖励都要在 0 到 100 之间。";
        }

        if (!InRange(Input.ShopPriceMin, 1, 99999) || !InRange(Input.ShopPriceMax, 1, 99999)) {
            return "心愿的价格要在 1 到 99999 之间。";
        }

        if (Input.ShopPriceMax < Input.ShopPriceMin) {
            return "心愿的最高价不能比最低价还低。";
        }

        return InRange(Input.ShopListingDays, 1, 3650) && InRange(Input.ShopValidDays, 1, 3650)
            ? null
            : "上架天数和过期天数都要在 1 到 3650 之间。";
    }

    private static bool InRange(int value, int low, int high) => value >= low && value <= high;

    private void SaveRuntimeOptions(OurStoryConfiguration next) {
        next.Site.TimeZone = string.IsNullOrWhiteSpace(Input.TimeZone) ? "Asia/Shanghai" : Input.TimeZone.Trim();

        next.Storage.Driver = Input.StorageDriver;
        next.Storage.Oss.Region = Trim(Input.OssRegion);
        next.Storage.Oss.Bucket = Trim(Input.OssBucket);
        next.Storage.Oss.AccessKeyId = Trim(Input.OssAccessKeyId);
        next.Storage.Oss.AccessKeySecret = Trim(Input.OssAccessKeySecret);
        next.Storage.Oss.PublicBaseUrl = Trim(Input.OssPublicBaseUrl).TrimEnd('/');
        next.Storage.Oss.ApiEndpoint = Trim(Input.OssApiEndpoint).TrimEnd('/');
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim();

    /// <summary>
    /// 表示 InputModel
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置站点时区，IANA 写法，例如 Asia/Shanghai
        /// </summary>
        [StringLength(64)]
        public string TimeZone { get; set; } = "Asia/Shanghai";

        /// <summary>
        /// 获取或设置附件存储方式；留空表示自动（OSS 参数填全了就走 OSS）
        /// </summary>
        public StorageDriver? StorageDriver { get; set; }

        /// <summary>
        /// 获取或设置 OSS 区域，例如 cn-shanghai
        /// </summary>
        [StringLength(64)]
        public string? OssRegion { get; set; }

        /// <summary>
        /// 获取或设置 OSS Bucket 名称
        /// </summary>
        [StringLength(64)]
        public string? OssBucket { get; set; }

        /// <summary>
        /// 获取或设置 OSS AccessKeyId
        /// </summary>
        [StringLength(128)]
        public string? OssAccessKeyId { get; set; }

        /// <summary>
        /// 获取或设置 OSS AccessKeySecret
        /// </summary>
        [StringLength(128)]
        public string? OssAccessKeySecret { get; set; }

        /// <summary>
        /// 获取或设置 OSS 公共访问基础地址，用于生成文件访问 URL
        /// </summary>
        [StringLength(200)]
        public string? OssPublicBaseUrl { get; set; }

        /// <summary>
        /// 获取或设置 OSS API Endpoint 地址
        /// </summary>
        [StringLength(200)]
        public string? OssApiEndpoint { get; set; }

        /// <summary>
        /// 获取或设置站点名称
        /// </summary>
        [Required(ErrorMessage = "站点名称不能为空")]
        [StringLength(60)]
        public string SiteTitle { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置站点描述信息
        /// </summary>
        [StringLength(120)]
        public string SiteDescription { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置男主名称
        /// </summary>
        [Required(ErrorMessage = "男主名字不能为空")]
        [StringLength(32)]
        public string BoyName { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置女主名称
        /// </summary>
        [Required(ErrorMessage = "女主名字不能为空")]
        [StringLength(32)]
        public string GirlName { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置男主头像地址
        /// </summary>
        [StringLength(500)]
        public string? BoyAvatar { get; set; }

        /// <summary>
        /// 获取或设置女主头像地址
        /// </summary>
        [StringLength(500)]
        public string? GirlAvatar { get; set; }

        /// <summary>
        /// 获取或设置男主寄语
        /// </summary>
        [StringLength(60)]
        public string BoySentence { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置女主寄语
        /// </summary>
        [StringLength(60)]
        public string GirlSentence { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置恋爱开始时间
        /// </summary>
        /// <remarks>格式钉到分钟的原因见 <c>Moments/Edit.cshtml.cs</c> 里的 MomentDate。</remarks>
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime LoveStartedAt { get; set; }

        /// <summary>
        /// 获取或设置首页展示语句
        /// </summary>
        [StringLength(200)]
        public string HomeSentence { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置每日记录内容
        /// </summary>
        [StringLength(400)]
        public string DailyNote { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置恋爱文字列表，每行一条
        /// </summary>
        public string? LoveLetters { get; set; }

        /// <summary>
        /// 获取或设置站点颜色模式，例如 auto、light、dark。
        /// </summary>
        public string ColorMode { get; set; } = "auto";

        /// <summary>
        /// 获取或设置回忆列表分页大小
        /// </summary>
        [Range(1, 100, ErrorMessage = "每页条数要在 1 到 100 之间")]
        public int MomentsPageSize { get; set; } = 10;

        /// <summary>
        /// 获取或设置每日心动记录最大数量
        /// </summary>
        [Range(1, 9999, ErrorMessage = "每日上限要在 1 到 9999 之间")]
        public int HeartbeatDailyLimit { get; set; } = 99;

        /// <summary>
        /// 获取或设置评论是否必须填写邮箱
        /// </summary>
        public bool CommentsRequireMail { get; set; }

        /// <summary>
        /// 获取或设置是否允许游客发表评论
        /// </summary>
        public bool AllowGuestComments { get; set; } = true;

        #region boy 特有

        /// <summary>
        /// 获取或设置当天第一次打开站点给多少心意
        /// </summary>
        public int RewardVisit { get; set; } = 3;

        /// <summary>
        /// 获取或设置当天第一次想你给多少心意
        /// </summary>
        public int RewardHeartbeat { get; set; } = 2;

        /// <summary>
        /// 获取或设置当天第一次发布点点滴滴给多少心意
        /// </summary>
        public int RewardMoment { get; set; } = 8;

        /// <summary>
        /// 获取或设置当天第一次发布纪念日给多少心意
        /// </summary>
        public int RewardAnniversary { get; set; } = 12;

        /// <summary>
        /// 获取或设置心愿的最低标价
        /// </summary>
        public int ShopPriceMin { get; set; } = 5;

        /// <summary>
        /// 获取或设置心愿的最高标价
        /// </summary>
        public int ShopPriceMax { get; set; } = 500;

        /// <summary>
        /// 获取或设置默认的上架天数
        /// </summary>
        public int ShopListingDays { get; set; } = 30;

        /// <summary>
        /// 获取或设置默认的兑换后有效天数
        /// </summary>
        public int ShopValidDays { get; set; } = 30;

        #endregion
    }
}
