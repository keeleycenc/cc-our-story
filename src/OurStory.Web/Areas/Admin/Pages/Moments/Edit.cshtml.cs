// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Core.Time;
using OurStory.Services.Moments;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Moments;

/// <summary>
/// 表示 EditModel
/// </summary>
public class EditModel(IMomentService moments, SiteClock clock) : PageModel {
    /// <summary>
    /// 执行 Input 操作
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// 获取或设置 MomentId
    /// </summary>
    public int? MomentId { get; private set; }

    /// <summary>
    /// 获取或设置 Slug
    /// </summary>
    public string? Slug { get; private set; }

    /// <summary>
    /// 获取 IsNew
    /// </summary>
    public bool IsNew => MomentId is null;

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken) {
        if (id is null) {
            Input = new InputModel { MomentDate = clock.LocalNow };
            return Page();
        }

        var moment = await moments.GetByIdAsync(id.Value, cancellationToken);
        if (moment is null) {
            return NotFound();
        }

        MomentId = moment.Id;
        Slug = moment.Slug;
        Input = new InputModel {
            Title = moment.Title,
            Slug = moment.Slug,
            Content = moment.Content,
            Summary = moment.Summary,
            CoverUrl = moment.CoverUrl,
            Mood = moment.Mood,
            Location = moment.Location,
            MomentDate = clock.ToLocal(moment.MomentDate),
            Password = moment.Password,
            Status = moment.Status,
            AllowComment = moment.AllowComment
        };

        return Page();
    }

    /// <summary>
    /// 处理 Async(int?, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken) {
        MomentId = id;

        if (!ModelState.IsValid) {
            return Page();
        }

        var model = new MomentEditModel {
            Title = Input.Title,
            Slug = Input.Slug,
            Content = Input.Content,
            Summary = Input.Summary,
            CoverUrl = Input.CoverUrl,
            Mood = Input.Mood,
            Location = Input.Location,
            MomentDate = Input.MomentDate,
            Password = Input.Password,
            Status = Input.Status,
            AllowComment = Input.AllowComment
        };

        if (id is null) {
            var authorId = User.UserId() ?? 0;
            _ = await moments.CreateAsync(model, authorId, cancellationToken);
            TempData["Flash"] = "记录已经保存好了。快去看看吧";
            return Redirect("/admin/moments");
        }

        if (!await moments.UpdateAsync(id.Value, model, cancellationToken)) {
            return NotFound();
        }

        TempData["Flash"] = "改动已经保存。";
        return Redirect("/admin/moments");
    }

    /// <summary>
    /// 表示 InputModel
    /// </summary>
    public class InputModel {
        /// <summary>
        /// 获取或设置 Title
        /// </summary>
        [Required(ErrorMessage = "标题不能为空")]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置 Slug
        /// </summary>
        [StringLength(80)]
        public string? Slug { get; set; }

        /// <summary>
        /// 获取或设置 Content
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置 Summary
        /// </summary>
        [StringLength(400)]
        public string? Summary { get; set; }

        /// <summary>
        /// 获取或设置 CoverUrl
        /// </summary>
        [StringLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>
        /// 获取或设置 Mood
        /// </summary>
        [StringLength(32)]
        public string? Mood { get; set; }

        /// <summary>
        /// 获取或设置 Location
        /// </summary>
        [StringLength(120)]
        public string? Location { get; set; }

        /// <summary>
        /// 获取或设置 MomentDate
        /// </summary>
        /// <remarks>
        /// 点点滴滴和纪念日都只记录发生日期，使用同一套日期控件。
        /// </remarks>
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime MomentDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 获取或设置 Password
        /// </summary>
        [StringLength(128)]
        public string? Password { get; set; }

        /// <summary>
        /// 获取或设置 Status
        /// </summary>
        public MomentStatus Status { get; set; } = MomentStatus.Published;

        /// <summary>
        /// 获取或设置 AllowComment
        /// </summary>
        public bool AllowComment { get; set; } = true;
    }
}
