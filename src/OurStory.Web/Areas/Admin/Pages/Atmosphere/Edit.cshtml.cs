// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Configuration;
using OurStory.Core.Options;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Atmosphere;

/// <summary>
/// 添加或修改一个氛围组角色
/// </summary>
public class EditModel(ActiveConfiguration configuration) : PageModel {
    /// <summary>
    /// 获取或设置表单输入
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取一个值，指示这是在改一个已有的角色
    /// </summary>
    public bool IsExisting { get; private set; }

    /// <summary>
    /// 获取一个值，指示这个角色已经存过 Key
    /// </summary>
    /// <remarks>存过就不再回显，留空即保持原样，改 Key 才需要重新填一遍</remarks>
    public bool HasApiKey { get; private set; }

    /// <summary>
    /// 获取或设置保存没成功时的原因
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    /// <param name="id">要改的角色标识；留空表示新建</param>
    /// <returns>页面结果</returns>
    public IActionResult OnGet(string? id) {
        if (string.IsNullOrWhiteSpace(id)) {
            return Page();
        }

        if (configuration.LlmAtmosphere.Find(id) is not { } member) {
            return RedirectToPage("Index");
        }

        IsExisting = true;
        HasApiKey = !string.IsNullOrWhiteSpace(member.ApiKey);

        Input = new InputModel {
            Id = member.Id,
            Name = member.Name,
            BaseUrl = member.BaseUrl,
            Model = member.Model,
            AvatarUrl = member.AvatarUrl,
            Prompt = member.Prompt,
            Enabled = member.Enabled,
            AllowImages = member.AllowImages,
            CommentChance = member.CommentChance,
            ReplyChance = member.ReplyChance,
            DelayMinMinutes = member.DelayMinMinutes,
            DelayMaxMinutes = member.DelayMaxMinutes,
            MaxOutputTokens = member.MaxOutputTokens
        };

        return Page();
    }

    /// <summary>
    /// 保存这个角色
    /// </summary>
    /// <returns>保存结果</returns>
    public IActionResult OnPost() {
        var existing = configuration.LlmAtmosphere.Find(Input.Id);
        IsExisting = existing is not null;
        HasApiKey = !string.IsNullOrWhiteSpace(existing?.ApiKey);

        if (!ModelState.IsValid) {
            Error = string.Join("；", ModelState.Values.SelectMany(state => state.Errors).Select(error => error.ErrorMessage));
            return Page();
        }

        if (Input.DelayMaxMinutes < Input.DelayMinMinutes) {
            Error = "最长等待不能比最短等待还短。";
            return Page();
        }

        if (!HasApiKey && string.IsNullOrWhiteSpace(Input.ApiKey)) {
            Error = "得先填一个 API Key，这个角色才调得起来。";
            return Page();
        }

        if (!configuration.Update(Save, out var error)) {
            Error = $"{configuration.FilePath} 无法写入：{error}";
            return Page();
        }

        TempData["Flash"] = IsExisting ? "角色已经保存。" : "角色已经添加。";
        return RedirectToPage("Index");
    }

    #region 私有方法

    private void Save(OurStoryConfiguration next) {
        var options = next.LlmAtmosphere;
        var member = options.Find(Input.Id);

        if (member is null) {
            member = new LlmAtmosphereMember { Id = options.NewId() };
            options.Members.Add(member);
        }

        member.Name = Input.Name.Trim();
        member.BaseUrl = Trim(Input.BaseUrl).TrimEnd('/');
        member.Model = Trim(Input.Model);
        member.AvatarUrl = Trim(Input.AvatarUrl);
        member.Prompt = (Input.Prompt ?? string.Empty).Trim();
        member.Enabled = Input.Enabled;
        member.AllowImages = Input.AllowImages;
        member.CommentChance = Input.CommentChance;
        member.ReplyChance = Input.ReplyChance;
        member.DelayMinMinutes = Input.DelayMinMinutes;
        member.DelayMaxMinutes = Input.DelayMaxMinutes;
        member.MaxOutputTokens = Input.MaxOutputTokens;

        if (!string.IsNullOrWhiteSpace(Input.ApiKey)) {
            member.ApiKey = Input.ApiKey.Trim();
        }
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim();

    #endregion

    /// <summary>
    /// 一个角色的全部可配项
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置角色标识；新建时为空，由服务端发一个
        /// </summary>
        [StringLength(64)]
        public string? Id { get; set; }

        /// <summary>
        /// 获取或设置角色名字
        /// </summary>
        [Required(ErrorMessage = "得给这位朋友起个名字")]
        [StringLength(32)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置服务地址
        /// </summary>
        [Required(ErrorMessage = "服务地址不能为空")]
        [StringLength(300)]
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置模型名称
        /// </summary>
        [Required(ErrorMessage = "模型名称不能为空")]
        [StringLength(120)]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置 API Key；改已有角色时留空表示不动
        /// </summary>
        [StringLength(300)]
        public string? ApiKey { get; set; }

        /// <summary>
        /// 获取或设置头像地址
        /// </summary>
        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// 获取或设置人设
        /// </summary>
        [StringLength(2000)]
        public string? Prompt { get; set; }

        /// <summary>
        /// 获取或设置是否启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 获取或设置能不能看图
        /// </summary>
        public bool AllowImages { get; set; }

        /// <summary>
        /// 获取或设置留言触发概率
        /// </summary>
        [Range(0, 100, ErrorMessage = "留言概率要在 0 到 100 之间")]
        public int CommentChance { get; set; } = 60;

        /// <summary>
        /// 获取或设置回复触发概率
        /// </summary>
        [Range(0, 100, ErrorMessage = "回复概率要在 0 到 100 之间")]
        public int ReplyChance { get; set; } = 70;

        /// <summary>
        /// 获取或设置最短等待分钟数
        /// </summary>
        [Range(0, 1440, ErrorMessage = "最短等待要在 0 到 1440 分钟之间")]
        public int DelayMinMinutes { get; set; } = 3;

        /// <summary>
        /// 获取或设置最长等待分钟数
        /// </summary>
        [Range(0, 1440, ErrorMessage = "最长等待要在 0 到 1440 分钟之间")]
        public int DelayMaxMinutes { get; set; } = 90;

        /// <summary>
        /// 获取或设置一条留言最多写多少个 token
        /// </summary>
        [Range(32, 4096, ErrorMessage = "输出上限要在 32 到 4096 之间")]
        public int MaxOutputTokens { get; set; } = 1024;
    }
}
