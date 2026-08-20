// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Affinity;

/// <summary>
/// 创建后立即封存的心有灵犀题目
/// </summary>
public class CreateModel(IAffinityService affinity) : PageModel {
    /// <summary>
    /// 获取或设置题目输入模型
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取错误信息
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 初始化创建页面
    /// </summary>
    public void OnGet() {
        Input.RewardPoints = 5;
        Input.Type = AffinityQuestionType.SingleChoice;
    }

    /// <summary>
    /// 异步提交题目创建请求
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>页面响应结果</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken) {
        var options = SplitOptions(Input.Options);
        if (options.Count is < 2 or > 8) {
            ModelState.AddModelError(nameof(Input.Options), "请填写 2 到 8 个不重复选项，每行一个");
        }

        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            return Page();
        }

        try {
            _ = await affinity.CreateQuestionAsync(new AffinityQuestionCreateModel {
                Text = Input.Text,
                Category = Input.Category,
                Type = Input.Type,
                Options = options,
                RewardPoints = Input.RewardPoints
            }, cancellationToken);
        } catch (ArgumentException exception) {
            Error = exception.Message;
            return Page();
        }

        TempData["Flash"] = "题目已经封存。再次打开列表时，只会看到不泄漏内容的记录信息。";
        return Redirect("/admin/affinity");
    }

    /// <summary>
    /// 解析题目选项
    /// </summary>
    /// <param name="value">原始选项文本</param>
    /// <returns>去重后的选项列表</returns>
    private static IReadOnlyList<string> SplitOptions(string? value) => [.. (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// 获取或设置题目创建输入
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置题目内容
        /// </summary>
        [Required(ErrorMessage = "请填写题目")]
        [StringLength(300, MinimumLength = 2, ErrorMessage = "题目应为 2 到 300 字")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置题目分类
        /// </summary>
        [Required(ErrorMessage = "请填写分类")]
        [StringLength(30, ErrorMessage = "分类不能超过 30 字")]
        public string Category { get; set; } = "日常";

        /// <summary>
        /// 获取或设置题目类型
        /// </summary>
        public AffinityQuestionType Type { get; set; } = AffinityQuestionType.SingleChoice;

        /// <summary>
        /// 获取或设置题目选项文本
        /// </summary>
        [Required(ErrorMessage = "请填写选项")]
        [StringLength(1000)]
        public string Options { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置答题奖励值
        /// </summary>
        [Range(HeartPointRules.MinReward, HeartPointRules.MaxReward, ErrorMessage = "答题奖励应为 0 到 100 心意")]
        public int RewardPoints { get; set; } = 5;
    }
}
