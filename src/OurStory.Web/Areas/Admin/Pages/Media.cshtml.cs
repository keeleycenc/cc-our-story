// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Models;
using OurStory.Core.Options;
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
    CoverThumbnails thumbnails,
    StoragePaths paths,
    ActiveConfiguration configuration,
    IMarkdownRenderer markdown) : PageModel {
    private const int PageSize = PageNumbers.AdminPageSize;

    /// <summary>
    /// 获取 DriverName
    /// </summary>
    public string DriverName => attachments.DriverName;

    /// <summary>
    /// 获取 IsLocal
    /// </summary>
    public bool IsLocal => configuration.Storage.EffectiveDriver == StorageDriver.Local;

    /// <summary>
    /// 获取或设置 Items
    /// </summary>
    public PagedList<MediaItem> Items { get; private set; } = PagedList<MediaItem>.Empty(PageSize);

    /// <summary>
    /// 获取或设置 Error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public void OnGet() {
        Items = IsLocal ? ListLocalFiles(Request.PageNumber()) : PagedList<MediaItem>.Empty(PageSize);
    }

    /// <summary>
    /// 处理 Async(List{IFormFile}?, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostAsync(List<IFormFile>? files, CancellationToken cancellationToken) {
        var picked = files?.Where(item => item.Length > 0).ToList() ?? [];
        if (picked.Count == 0) {
            Error = "还没有选文件。";
            OnGet();
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
            OnGet();
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
    /// 使用与前台正文一致的规则生成 Markdown 预览
    /// </summary>
    public IActionResult OnPostPreview(string? content) =>
        new JsonResult(new { ok = true, html = markdown.ToHtml(content) });

    /// <summary>
    /// 本地存储时按页列出上传过的图片，新的排在前面。
    ///
    /// OSS 要额外调列举接口，而且真正需要翻旧图的场景很少，
    /// 所以那种情况下这一页只负责上传。
    /// </summary>
    private PagedList<MediaItem> ListLocalFiles(int page) {
        if (!Directory.Exists(paths.UploadsRoot)) {
            return PagedList<MediaItem>.Empty(PageSize);
        }

        var files = new DirectoryInfo(paths.UploadsRoot)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        var lastPage = Math.Max(1, (files.Count + PageSize - 1) / PageSize);
        page = Math.Min(page, lastPage);

        var items = files
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(file => {
                var url = storage.PublicUrl(Path.GetRelativePath(paths.UploadsRoot, file.FullName).Replace('\\', '/'));
                return new MediaItem(url, thumbnails.For(url), file.Name, file.Length);
            })
            .ToList();

        return new PagedList<MediaItem>(items, page, PageSize, files.Count);
    }

    /// <summary>图片库里的一张图</summary>
    /// <param name="Url">原图的对外地址</param>
    /// <param name="ThumbUrl">列表里用的缩略图地址</param>
    /// <param name="Name">文件名</param>
    /// <param name="Size">字节数</param>
    public record MediaItem(string Url, string ThumbUrl, string Name, long Size) {
        /// <summary>
        /// 获取 SizeText
        /// </summary>
        public string SizeText => Size < 1024 * 1024
            ? $"{Size / 1024d:0.#} KB"
            : $"{Size / 1024d / 1024d:0.##} MB";
    }
}
