// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Models;
using OurStory.Services.Affinity;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Affinity;

/// <summary>
/// 获取心有灵犀题库管理页面模型
/// </summary>
/// <param name="affinity">获取心有灵犀服务</param>
public class IndexModel(IAffinityService affinity) : PageModel {
    /// <summary>
    /// 获取或设置题目输入模型
    /// </summary>
    [BindProperty]
    public QuestionInput Input { get; set; } = new();

    /// <summary>
    /// 获取题目列表
    /// </summary>
    public IReadOnlyList<AffinityQuestionCard> Questions { get; private set; } = [];

    /// <summary>
    /// 获取正在编辑的题目标识
    /// </summary>
    public int? EditingId { get; private set; }

    /// <summary>
    /// 获取错误信息
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 获取页面数据
    /// </summary>
    public async Task OnGetAsync(int? edit, CancellationToken cancellationToken) {
        if (edit is { } id && await affinity.GetQuestionAsync(id, cancellationToken) is { } question) {
            EditingId = id;
            Input = new QuestionInput {
                Id = id,
                Text = question.Text,
                Category = question.Category,
                Options = string.Join(Environment.NewLine, question.Options),
                IsActive = question.IsActive
            };
        }

        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// 保存题目
    /// </summary>
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken) {
        var options = SplitOptions(Input.Options);
        if (options.Count is < 2 or > 8) {
            ModelState.AddModelError(nameof(Input.Options), "请填写 2 到 8 个不重复选项，每行一个");
        }

        if (!ModelState.IsValid) {
            EditingId = Input.Id;
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            await LoadAsync(cancellationToken);
            return Page();
        }

        try {
            _ = await affinity.SaveQuestionAsync(Input.Id, new AffinityQuestionEditModel {
                Text = Input.Text,
                Category = Input.Category,
                Options = options,
                IsActive = Input.IsActive
            }, cancellationToken);
        } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) {
            EditingId = Input.Id;
            Error = exception.Message;
            await LoadAsync(cancellationToken);
            return Page();
        }

        TempData["Flash"] = Input.Id is null ? "新题目已经加入题库。" : "题目已经更新，历史快照不会受影响。";
        return RedirectToPage();
    }

    /// <summary>
    /// 设置题目启用状态
    /// </summary>
    public async Task<IActionResult> OnPostToggleAsync(int id, bool active, CancellationToken cancellationToken) {
        _ = await affinity.SetQuestionActiveAsync(id, active, cancellationToken);
        TempData["Flash"] = active ? "题目已启用。" : "题目已停用，不再参与每日抽取。";
        return RedirectToPage();
    }

    /// <summary>
    /// 删除题目
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken) {
        _ = await affinity.DeleteQuestionAsync(id, cancellationToken);
        TempData["Flash"] = "题目已删除，已经产生的答题历史仍会保留。";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken) =>
        Questions = await affinity.GetQuestionsAsync(cancellationToken);

    private static IReadOnlyList<string> SplitOptions(string? value) => [.. (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// 获取或设置题目输入数据
    /// </summary>
    public class QuestionInput {
        /// <summary>
        /// 获取或设置题目标识
        /// </summary>
        public int? Id { get; set; }

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
        /// 获取或设置题目选项内容
        /// </summary>
        [Required(ErrorMessage = "请填写选项")]
        public string Options { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置题目是否启用
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
