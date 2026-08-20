// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Abstractions;
using OurStory.Core.Models;
using OurStory.Core.Time;
using OurStory.Services.Moments;
using OurStory.Services.Storage;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Areas.Admin.Pages;

/// <summary>
/// 后台里贴图用的小图片库
/// </summary>
public class MediaModel(
    IAttachmentService attachments,
    IFileStorage storage,
    IMediaLibraryService library,
    MediaUrls media,
    SiteClock clock,
    IMarkdownRenderer markdown,
    ArticleMedia articleMedia) : PageModel {
    private const int PageSize = PageNumbers.AdminPageSize;

    /// <summary>
    /// 获取 DriverName
    /// </summary>
    public string DriverName => attachments.DriverName;

    /// <summary>
    /// 获取或设置 Items
    /// </summary>
    public PagedList<MediaItem> Items { get; private set; } = PagedList<MediaItem>.Empty(PageSize);

    /// <summary>
    /// 获取或设置 Error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 删除被拦截时，列出仍在使用图片的具体位置
    /// </summary>
    public IReadOnlyList<MediaReference> References { get; private set; } = [];

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Items = await ListFilesAsync(Request.PageNumber(), cancellationToken);

    /// <summary>
    /// 处理 Async(List{IFormFile}?, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostAsync(List<IFormFile>? files, CancellationToken cancellationToken) {
        var picked = files?.Where(item => item.Length > 0).ToList() ?? [];
        if (picked.Count == 0) {
            Error = "还没有选文件。";
            Items = await ListFilesAsync(Request.PageNumber(), cancellationToken);
            return Page();
        }

        var uploaded = 0;
        string? failure = null;

        // 有一张传不上去也别停：多半是这一张的格式不对，剩下的还能接着传
        foreach (var file in picked) {
            await using var stream = file.OpenReadStream();
            var result = await attachments.UploadAsync(stream, file.FileName, file.Length, cancellationToken);

            if (result.Success) {
                uploaded++;
            } else {
                failure ??= result.Error;
            }
        }

        if (failure is not null) {
            Error = uploaded == 0 ? failure : $"已上传 {uploaded} 张，部分失败：{failure}";
            Items = await ListFilesAsync(Request.PageNumber(), cancellationToken);
            return Page();
        }

        TempData["Flash"] = $"已上传 {uploaded} 张图片。";
        return RedirectToPage();
    }

    /// <summary>
    /// 编辑器里的「插图」按钮走这里，返回 JSON 给前端直接插进正文
    /// </summary>
    /// <param name="file"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken cancellationToken) {
        if (file is null || file.Length == 0) {
            return new JsonResult(new { ok = false, error = "还没有选文件。" }) { StatusCode = StatusCodes.Status400BadRequest };
        }

        await using var stream = file.OpenReadStream();
        var result = await attachments.UploadAsync(stream, file.FileName, file.Length, cancellationToken);

        return result.Success
            ? new JsonResult(new { ok = true, url = result.Url })
            : new JsonResult(new { ok = false, error = result.Error }) { StatusCode = StatusCodes.Status400BadRequest };
    }

    /// <summary>
    /// 确认未被业务内容引用后，删除原图及其派生缓存
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? objectKey, CancellationToken cancellationToken) {
        var result = await library.DeleteAsync(objectKey ?? string.Empty, cancellationToken);
        if (result.Success) {
            TempData["Flash"] = "图片已删除，相关缓存也已清理。";
            return Redirect($"/admin/media?page={Request.PageNumber()}");
        }

        Error = result.Error;
        References = result.References;
        Items = await ListFilesAsync(Request.PageNumber(), cancellationToken);
        return Page();
    }

    /// <summary>
    /// 使用与前台正文一致的规则生成 Markdown 预览
    /// </summary>
    public async Task<IActionResult> OnPostPreviewAsync(string? content, CancellationToken cancellationToken) =>
        new JsonResult(new { ok = true, html = await articleMedia.ShrinkImagesAsync(markdown.ToHtml(content), cancellationToken) });

    private async Task<PagedList<MediaItem>> ListFilesAsync(int page, CancellationToken cancellationToken) {
        var files = (await storage.ListAsync(cancellationToken))
            .OrderByDescending(file => file.LastModified)
            .ToList();

        var lastPage = Math.Max(1, (files.Count + PageSize - 1) / PageSize);
        page = Math.Min(page, lastPage);

        var items = files
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(file => {
                var url = storage.PublicUrl(file.ObjectKey);
                return new MediaItem(
                    file.ObjectKey,
                    url,
                    media.Cover(url),
                    media.Preview(url),
                    Path.GetFileName(file.ObjectKey),
                    file.Size,
                    file.LastModified,
                    clock.ToLocal(file.LastModified));
            })
            .ToList();

        return new PagedList<MediaItem>(items, page, PageSize, files.Count);
    }

    /// <summary>图片库里的一张图</summary>
    /// <param name="Url">原图的对外地址</param>
    /// <param name="ThumbUrl">列表里用的缩略图地址</param>
    /// <param name="PreviewUrl">查看器里等原图时先顶上的那一份，没裁过，比例和原图一致</param>
    /// <param name="Name">文件名</param>
    /// <param name="Size">字节数</param>
    public record MediaItem(
        string ObjectKey,
        string Url,
        string ThumbUrl,
        string PreviewUrl,
        string Name,
        long Size,
        DateTimeOffset UploadedAt,
        DateTime LocalUploadedAt) {
        /// <summary>
        /// 获取 SizeText
        /// </summary>
        public string SizeText => Size < 1024 * 1024
            ? $"{Size / 1024d:0.#} KB"
            : $"{Size / 1024d / 1024d:0.##} MB";

        /// <summary>
        /// 按站点时区显示的完整上传时间
        /// </summary>
        public string UploadedAtText => LocalUploadedAt.ToString("yyyy-MM-dd HH:mm");

        /// <summary>
        /// 窄卡片省略年份后的上传时间
        /// </summary>
        public string UploadedAtShortText => LocalUploadedAt.ToString("MM-dd HH:mm");
    }
}
