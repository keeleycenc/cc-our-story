// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;
using OurStory.Services.Cycles;
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
public class SettingsModel(
    ISettingsService settings,
    ActiveConfiguration configuration,
    ICycleInsightService insight,
    ICycleService cycles) : PageModel {
    /// <summary>
    /// 手动补写花信小结时的单次处理上限
    /// </summary>
    private const int CycleRefreshBatch = 20;

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
    /// 获取一个值，指示当前用户是否有权配置心意发放与价格区间
    /// </remarks>
    public bool CanEditHeartRules => User.Role() == UserRole.Boy;

    /// <summary>
    /// 获取当前实际生效的存储方式；OSS 配置不完整时可能与所选策略不同
    /// </summary>
    public string EffectiveDriverText =>
        configuration.Storage.EffectiveDriver == StorageDriver.AliyunOss ? "阿里云 OSS" : "本地目录";

    /// <summary>
    /// 获取配置文件的位置
    /// </summary>
    public string ConfigFilePath => configuration.FilePath;

    /// <summary>
    /// 获取一个值，指示邮件通知当前是否已经可用
    /// </summary>
    public bool EmailConfigured => configuration.Email.Enabled && configuration.Email.IsConfigured;

    /// <summary>
    /// 获取一个值，指示花信小结是否启用模型服务
    /// </summary>
    public bool CycleInsightConfigured => configuration.CycleInsight.IsUsable;

    /// <summary>
    /// 获取一个值，指示当前配置是否已保存 API Key
    /// </summary>
    public bool CycleInsightHasKey => !string.IsNullOrWhiteSpace(configuration.CycleInsight.ApiKey);

    /// <summary>
    /// 获取最近一次模型通道测试结果；尚未测试时为 <see langword="null"/>
    /// </summary>
    public CycleInsightProbe? CycleProbe { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) {
        var site = await settings.GetAsync(cancellationToken);
        var storage = configuration.Storage;
        var email = configuration.Email;
        var cycle = configuration.CycleInsight;

        Input = new InputModel {
            TimeZone = configuration.Site.TimeZone,
            StorageDriver = storage.Driver,
            OssRegion = storage.Oss.Region,
            OssBucket = storage.Oss.Bucket,
            OssAccessKeyId = storage.Oss.AccessKeyId,
            OssAccessKeySecret = storage.Oss.AccessKeySecret,
            OssPublicBaseUrl = storage.Oss.PublicBaseUrl,
            OssApiEndpoint = storage.Oss.ApiEndpoint,
            CycleInsightEnabled = cycle.Enabled,
            CycleInsightBaseUrl = cycle.BaseUrl,
            CycleInsightModel = cycle.Model,
            CycleInsightApiKey = string.Empty,
            CycleInsightTone = cycle.Tone,
            CycleInsightTimeoutSeconds = cycle.TimeoutSeconds,
            CycleInsightMaxOutputTokens = cycle.MaxOutputTokens,
            CycleInsightRefreshHours = cycle.RefreshHours,
            EmailEnabled = email.Enabled,
            EmailHost = email.Host,
            EmailPort = email.Port,
            EmailSecurity = email.Security,
            EmailUsername = email.Username,
            EmailPassword = string.Empty,
            EmailSenderEmail = email.SenderEmail,
            EmailSenderName = email.SenderName,
            EmailSiteBaseUrl = email.SiteBaseUrl,
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
            RewardAffinity = site.RewardAffinity,
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

        if (EmailConfigError() is { } emailProblem) {
            Error = emailProblem;
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
            site.RewardAffinity = Input.RewardAffinity;
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

        // 时区和附件存储写入配置文件；写入失败时向页面返回错误提示。
        if (!configuration.Update(SaveRuntimeOptions, out var error)) {
            Error = $"内容已经保存，但 {configuration.FilePath} 无法写入：{error}";
            return Page();
        }

        TempData["Flash"] = "设置已经保存。";
        return RedirectToPage();
    }

    /// <summary>
    /// 使用示例数据测试花信小结模型调用
    /// </summary>
    /// <remarks>
    /// 测试使用已保存的配置，配置修改后应先完成保存。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>带回执的页面响应</returns>
    public async Task<IActionResult> OnPostTestCycleAsync(CancellationToken cancellationToken) {
        try {
            CycleProbe = await insight.ProbeAsync(cancellationToken);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            CycleProbe = CycleInsightProbe.Failed($"模型调用发生异常：{exception.Message}");
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// 立即补充缺失或已失效的花信小结
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>补写结果</returns>
    public async Task<IActionResult> OnPostRefreshCycleAsync(CancellationToken cancellationToken) {
        if (!CycleInsightConfigured) {
            TempData["Flash"] = "模型通道尚未启用，当前小结由站内规则实时生成，无需执行补写。";
            return RedirectToPage();
        }

        var written = await cycles.RefreshSummariesAsync(CycleRefreshBatch, cancellationToken);
        TempData["Flash"] = written > 0
            ? $"已补写 {written} 条花信小结。"
            : "当前没有需要补写的小结，或模型本次未返回有效内容。";
        return RedirectToPage();
    }

    /// <summary>
    /// 验证心意规则范围；验证通过时返回 null
    /// </summary>
    private string? HeartRuleError() {
        if (!InRange(Input.RewardVisit, 0, 100)
            || !InRange(Input.RewardHeartbeat, 0, 100)
            || !InRange(Input.RewardMoment, 0, 100)
            || !InRange(Input.RewardAnniversary, 0, 100)) {
                return "四档心意奖励都要在 0 到 100 之间。";
        }

        if (!InRange(Input.RewardAffinity, HeartPointRules.MinAffinityReward, HeartPointRules.MaxReward)) {
            return $"心有灵犀答题奖励要在 {HeartPointRules.MinAffinityReward} 到 {HeartPointRules.MaxReward} 之间。";
        }

        if (!InRange(Input.ShopPriceMin, 1, 99999) || !InRange(Input.ShopPriceMax, 1, 99999)) {
            return "心愿的价格要在 1 到 99999 之间。";
        }

        if (Input.ShopPriceMax < Input.ShopPriceMin) {
            return "心愿最高价格不能低于最低价格。";
        }

        return InRange(Input.ShopListingDays, 1, 3650) && InRange(Input.ShopValidDays, 1, 3650)
            ? null
            : "上架天数和过期天数都要在 1 到 3650 之间。";
    }

    private static bool InRange(int value, int low, int high) => value >= low && value <= high;

    private string? EmailConfigError() {
        if (!string.IsNullOrWhiteSpace(Input.EmailSenderEmail) && !EmailOptions.IsValidAddress(Input.EmailSenderEmail)) {
            return "通知发送邮箱地址不合法。";
        }

        if (!string.IsNullOrWhiteSpace(Input.EmailSiteBaseUrl)
            && !EmailOptions.IsValidSiteBaseUrl(Input.EmailSiteBaseUrl)) {
            return "站点公开地址必须是完整的 http:// 或 https:// 地址。";
        }

        if (!Input.EmailEnabled) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(Input.EmailHost)) {
            return "启用邮件通知前请填写 SMTP Host。";
        }

        if (!EmailOptions.IsValidAddress(Input.EmailSenderEmail)) {
            return "启用邮件通知前请填写合法的通知发送邮箱。";
        }

        if (!EmailOptions.IsValidSiteBaseUrl(Input.EmailSiteBaseUrl)) {
            return "启用邮件通知前请填写站点公开地址，确保邮件详情链接可以直接访问。";
        }

        if (!string.IsNullOrWhiteSpace(Input.EmailUsername)
            && string.IsNullOrWhiteSpace(Input.EmailPassword)
            && string.IsNullOrWhiteSpace(configuration.Email.Password)) {
            return "SMTP 用户名已填写，请同时填写密码或邮箱授权码。";
        }

        return null;
    }

    private void SaveRuntimeOptions(OurStoryConfiguration next) {
        next.Site.TimeZone = string.IsNullOrWhiteSpace(Input.TimeZone) ? "Asia/Shanghai" : Input.TimeZone.Trim();

        next.Storage.Driver = Input.StorageDriver;
        next.Storage.Oss.Region = Trim(Input.OssRegion);
        next.Storage.Oss.Bucket = Trim(Input.OssBucket);
        next.Storage.Oss.AccessKeyId = Trim(Input.OssAccessKeyId);
        next.Storage.Oss.AccessKeySecret = Trim(Input.OssAccessKeySecret);
        next.Storage.Oss.PublicBaseUrl = Trim(Input.OssPublicBaseUrl).TrimEnd('/');
        next.Storage.Oss.ApiEndpoint = Trim(Input.OssApiEndpoint).TrimEnd('/');

        SaveEmailOptions(next.Email);
        SaveCycleInsightOptions(next.CycleInsight);
    }

    private void SaveCycleInsightOptions(CycleInsightOptions cycle) {
        cycle.Enabled = Input.CycleInsightEnabled;
        cycle.BaseUrl = Trim(Input.CycleInsightBaseUrl).TrimEnd('/');
        cycle.Model = Trim(Input.CycleInsightModel);
        cycle.Tone = Trim(Input.CycleInsightTone);
        cycle.TimeoutSeconds = Input.CycleInsightTimeoutSeconds;
        cycle.MaxOutputTokens = Input.CycleInsightMaxOutputTokens;
        cycle.RefreshHours = Input.CycleInsightRefreshHours;

        if (!string.IsNullOrWhiteSpace(Input.CycleInsightApiKey)) {
            cycle.ApiKey = Input.CycleInsightApiKey.Trim();
        }

        if (Input.CycleInsightClearApiKey) {
            cycle.ApiKey = string.Empty;
        }
    }

    private void SaveEmailOptions(EmailOptions email) {
        email.Enabled = Input.EmailEnabled;
        email.Host = Trim(Input.EmailHost);
        email.Port = Input.EmailPort;
        email.Security = Input.EmailSecurity;
        email.Username = Trim(Input.EmailUsername);
        email.SetPasswordIfProvided(Input.EmailPassword);

        email.SenderEmail = Trim(Input.EmailSenderEmail);
        email.SenderName = string.IsNullOrWhiteSpace(Input.EmailSenderName) ? "Our Story" : Input.EmailSenderName.Trim();
        email.SiteBaseUrl = Trim(Input.EmailSiteBaseUrl).TrimEnd('/');
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
        /// 获取或设置附件存储方式；留空表示在 OSS 配置完整时自动使用 OSS
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
        /// 获取或设置站点是否提供邮件通知
        /// </summary>
        public bool EmailEnabled { get; set; }

        /// <summary>
        /// 获取或设置 SMTP Host
        /// </summary>
        [StringLength(200)]
        public string? EmailHost { get; set; }

        /// <summary>
        /// 获取或设置 SMTP 端口
        /// </summary>
        [Range(1, 65535, ErrorMessage = "SMTP 端口要在 1 到 65535 之间")]
        public int EmailPort { get; set; } = 587;

        /// <summary>
        /// 获取或设置 SMTP 加密方式
        /// </summary>
        public EmailSecurity EmailSecurity { get; set; } = EmailSecurity.StartTls;

        /// <summary>
        /// 获取或设置 SMTP 用户名
        /// </summary>
        [StringLength(320)]
        public string? EmailUsername { get; set; }

        /// <summary>
        /// 获取或设置新 SMTP 密码；留空时保留已有值
        /// </summary>
        [StringLength(500)]
        public string? EmailPassword { get; set; }

        /// <summary>
        /// 获取或设置通知发送邮箱
        /// </summary>
        [StringLength(320)]
        public string? EmailSenderEmail { get; set; }

        /// <summary>
        /// 获取或设置发件人显示名称
        /// </summary>
        [StringLength(100)]
        public string? EmailSenderName { get; set; } = "Our Story";

        /// <summary>
        /// 获取或设置邮件详情链接使用的站点公开地址
        /// </summary>
        [StringLength(500)]
        public string? EmailSiteBaseUrl { get; set; }

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

        #region 花信小结

        /// <summary>
        /// 获取或设置一个值，指示是否让模型来写花信的周期小结
        /// </summary>
        public bool CycleInsightEnabled { get; set; }

        /// <summary>
        /// 获取或设置兼容 Responses 协议的服务地址
        /// </summary>
        [StringLength(300, ErrorMessage = "花信小结服务地址不能超过 300 个字符")]
        public string? CycleInsightBaseUrl { get; set; }

        /// <summary>
        /// 获取或设置模型名称
        /// </summary>
        [StringLength(120, ErrorMessage = "花信小结模型名称不能超过 120 个字符")]
        public string? CycleInsightModel { get; set; }

        /// <summary>
        /// 获取或设置新的 API Key；留空表示保持不变
        /// </summary>
        [StringLength(300, ErrorMessage = "花信小结 API Key 不能超过 300 个字符")]
        public string? CycleInsightApiKey { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示是否清除已保存的 API Key
        /// </summary>
        public bool CycleInsightClearApiKey { get; set; }

        /// <summary>
        /// 获取或设置附加在站点统一写作要求之后的语气偏好
        /// </summary>
        [StringLength(500, ErrorMessage = "花信小结语气偏好不能超过 500 个字符")]
        public string? CycleInsightTone { get; set; }

        /// <summary>
        /// 获取或设置单次调用超时秒数
        /// </summary>
        [Range(5, 300, ErrorMessage = "花信小结调用超时必须在 5 到 300 秒之间")]
        public int CycleInsightTimeoutSeconds { get; set; } = 45;

        /// <summary>
        /// 获取或设置单次调用允许生成的最大输出 Token 数
        /// </summary>
        [Range(64, 8192, ErrorMessage = "花信小结输出上限必须在 64 到 8192 Token 之间")]
        public int CycleInsightMaxOutputTokens { get; set; } = 8192;

        /// <summary>
        /// 获取或设置后台补写小结的间隔小时数
        /// </summary>
        [Range(1, 168, ErrorMessage = "花信小结补写间隔必须在 1 到 168 小时之间")]
        public int CycleInsightRefreshHours { get; set; } = 12;

        #endregion

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
        /// 获取或设置每次完成心有灵犀答题给多少心意
        /// </summary>
        [Range(HeartPointRules.MinAffinityReward, HeartPointRules.MaxReward)]
        public int RewardAffinity { get; set; } = 5;

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
