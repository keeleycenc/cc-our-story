// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Configuration;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages;

/// <summary>
/// 花信如期的模型小结配置页面
/// </summary>
public class CycleSettingsModel(ActiveConfiguration configuration) : PageModel {
    /// <summary>
    /// 获取或设置页面输入
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取保存失败或输入无效时的提示
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    public string ConfigFilePath => configuration.FilePath;

    /// <summary>
    /// 获取一个值，指示当前已保存配置是否可以调用模型
    /// </summary>
    public bool IsConfigured => configuration.CycleInsight.IsUsable;

    /// <summary>
    /// 获取一个值，指示当前配置是否已保存 API Key
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(configuration.CycleInsight.ApiKey);

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public void OnGet() => Load();

    /// <summary>
    /// 保存花信小结配置
    /// </summary>
    public IActionResult OnPost() {
        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values
                .SelectMany(state => state.Errors)
                .Select(error => error.ErrorMessage));
            return Page();
        }

        if (!configuration.Update(Save, out var error)) {
            Error = $"{ConfigFilePath} 无法写入：{error}";
            return Page();
        }

        TempData["Flash"] = "花信如期设置已经保存。";
        return RedirectToPage();
    }

    private void Load() {
        var options = configuration.CycleInsight;
        Input = new InputModel {
            Enabled = options.Enabled,
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            ApiKey = string.Empty,
            Tone = options.Tone,
            TimeoutSeconds = options.TimeoutSeconds,
            MaxOutputTokens = options.MaxOutputTokens,
            RefreshHours = options.RefreshHours
        };
    }

    private void Save(OurStoryConfiguration next) {
        var options = next.CycleInsight;
        options.Enabled = Input.Enabled;
        options.BaseUrl = Trim(Input.BaseUrl).TrimEnd('/');
        options.Model = Trim(Input.Model);
        options.Tone = Trim(Input.Tone);
        options.TimeoutSeconds = Input.TimeoutSeconds;
        options.MaxOutputTokens = Input.MaxOutputTokens;
        options.RefreshHours = Input.RefreshHours;

        if (!string.IsNullOrWhiteSpace(Input.ApiKey)) {
            options.ApiKey = Input.ApiKey.Trim();
        }
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim();

    /// <summary>
    /// 花信小结配置输入
    /// </summary>
    public sealed class InputModel {
        /// <summary>
        /// 获取或设置一个值，指示是否启用模型小结
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 获取或设置兼容 Responses 协议的服务地址
        /// </summary>
        [StringLength(300, ErrorMessage = "花信小结服务地址不能超过 300 个字符")]
        public string? BaseUrl { get; set; }

        /// <summary>
        /// 获取或设置模型名称
        /// </summary>
        [StringLength(120, ErrorMessage = "花信小结模型名称不能超过 120 个字符")]
        public string? Model { get; set; }

        /// <summary>
        /// 获取或设置新的 API Key；留空表示保持不变
        /// </summary>
        [StringLength(300, ErrorMessage = "花信小结 API Key 不能超过 300 个字符")]
        public string? ApiKey { get; set; }

        /// <summary>
        /// 获取或设置附加在站点统一写作要求之后的语气偏好
        /// </summary>
        [StringLength(500, ErrorMessage = "花信小结语气偏好不能超过 500 个字符")]
        public string? Tone { get; set; }

        /// <summary>
        /// 获取或设置单次调用超时秒数
        /// </summary>
        [Range(5, 300, ErrorMessage = "花信小结调用超时必须在 5 到 300 秒之间")]
        public int TimeoutSeconds { get; set; } = 45;

        /// <summary>
        /// 获取或设置单次调用允许生成的最大输出 Token 数
        /// </summary>
        [Range(64, 8192, ErrorMessage = "花信小结输出上限必须在 64 到 8192 Token 之间")]
        public int MaxOutputTokens { get; set; } = 8192;

        /// <summary>
        /// 获取或设置后台补写小结的间隔小时数
        /// </summary>
        [Range(1, 168, ErrorMessage = "花信小结补写间隔必须在 1 到 168 小时之间")]
        public int RefreshHours { get; set; } = 12;
    }
}
