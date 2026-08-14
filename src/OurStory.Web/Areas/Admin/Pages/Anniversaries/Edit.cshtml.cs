// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core;
using OurStory.Services.Anniversaries;
using OurStory.Web.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace OurStory.Web.Areas.Admin.Pages.Anniversaries;

/// <summary>新建或编辑纪念日。</summary>
public class EditModel(IAnniversaryService anniversaries) : PageModel {
    /// <summary>获取或设置表单输入。</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>获取当前纪念日编号。</summary>
    public int? AnniversaryId { get; private set; }

    /// <summary>获取是否正在创建。</summary>
    public bool IsNew => AnniversaryId is null;

    /// <summary>处理 GET 请求。</summary>
    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken) {
        AnniversaryId = id;
        if (id is null) {
            return Page();
        }

        var item = await anniversaries.GetByIdAsync(id.Value, cancellationToken);
        if (item is null) {
            return NotFound();
        }

        Input = new InputModel {
            Title = item.Title,
            AnniversaryDate = item.AnniversaryDate,
            Note = item.Note,
            CoverUrl = item.CoverUrl,
            Kind = item.Kind,
            RepeatYearly = item.RepeatYearly,
            IsPrivate = item.IsPrivate
        };
        return Page();
    }

    /// <summary>保存表单。</summary>
    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken) {
        AnniversaryId = id;
        if (!ModelState.IsValid) {
            return Page();
        }

        var model = new AnniversaryEditModel {
            Title = Input.Title,
            AnniversaryDate = Input.AnniversaryDate,
            Note = Input.Note,
            CoverUrl = Input.CoverUrl,
            Kind = Input.Kind,
            RepeatYearly = Input.RepeatYearly,
            IsPrivate = Input.IsPrivate
        };

        if (id is null) {
            _ = await anniversaries.CreateAsync(model, User.UserId(), cancellationToken);
            TempData["Flash"] = "纪念日已创建。";
            return Redirect("/admin/anniversaries");
        }

        if (!await anniversaries.UpdateAsync(id.Value, model, cancellationToken)) {
            return NotFound();
        }

        TempData["Flash"] = "纪念日已更新。";
        return Redirect("/admin/anniversaries");
    }

    /// <summary>纪念日表单模型。</summary>
    public class InputModel {
        /// <summary>获取或设置名称。</summary>
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength(80)]
        public string Title { get; set; } = string.Empty;

        /// <summary>获取或设置日期。</summary>
        [DataType(DataType.Date)]
        public DateOnly AnniversaryDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>获取或设置简短故事。</summary>
        [StringLength(8000)]
        public string? Note { get; set; }

        /// <summary>获取或设置封面图地址。</summary>
        [StringLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>获取或设置分类。</summary>
        public AnniversaryKind Kind { get; set; } = AnniversaryKind.Love;

        /// <summary>获取或设置是否每年重复。</summary>
        public bool RepeatYearly { get; set; } = true;

        /// <summary>获取或设置是否仅情侣双方可见。</summary>
        public bool IsPrivate { get; set; }
    }
}
