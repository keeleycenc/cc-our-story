// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Options;
using OurStory.Services.Moments;
using OurStory.Services.Storage;

namespace OurStory.Web.Areas.Admin.Pages;

/// <summary>后台里贴图用的小图片库。</summary>
public class MediaModel(
    IAttachmentService attachments,
    IFileStorage storage,
    StoragePaths paths,
    ActiveConfiguration configuration,
    IMarkdownRenderer markdown) : PageModel {
    private const int MaxListed = 60;

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
    public IReadOnlyList<MediaItem> Items { get; private set; } = [];

    /// <summary>
    /// 获取或设置 Error
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// 处理 GET 请求
    /// </summary>
    public void OnGet() {
        Items = IsLocal ? ListLocalFiles() : [];
    }

    /// <summary>
    /// 处理 Async(IFormFile?, CancellationToken) 的 POST 请求
    /// </summary>
    public async Task<IActionResult> OnPostAsync(IFormFile? file, CancellationToken cancellationToken) {
        if (file is null || file.Length == 0) {
            Error = "还没有选文件。";
            OnGet();
            return Page();
        }

        await using var stream = file.OpenReadStream();
        var result = await attachments.UploadAsync(stream, file.FileName, file.Length, cancellationToken);

        if (!result.Success) {
            Error = result.Error;
            OnGet();
            return Page();
        }

        TempData["Flash"] = $"上传好了：{result.Url}";
        return RedirectToPage();
    }

    /// <summary>编辑器里的「插图」按钮走这里，返回 JSON 给前端直接插进正文。</summary>
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

    /// <summary>使用与前台正文一致的规则生成 Markdown 预览。</summary>
    public IActionResult OnPostPreview(string? content) =>
        new JsonResult(new { ok = true, html = markdown.ToHtml(content) });

    /// <summary>
    /// 本地存储时列出最近上传的图片。
    ///
    /// OSS 要额外调列举接口，而且真正需要翻旧图的场景很少，
    /// 所以那种情况下这一页只负责上传。
    /// </summary>
    private List<MediaItem> ListLocalFiles() {
        if (!Directory.Exists(paths.UploadsRoot)) {
            return [];
        }

        return [.. new DirectoryInfo(paths.UploadsRoot)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(MaxListed)
            .Select(file => new MediaItem(
                storage.PublicUrl(Path.GetRelativePath(paths.UploadsRoot, file.FullName).Replace('\\', '/')),
                file.Name,
                file.Length))];
    }

    /// <summary>图片库里的一张图。</summary>
    /// <param name="Url">对外地址。</param>
    /// <param name="Name">文件名。</param>
    /// <param name="Size">字节数。</param>
    public record MediaItem(string Url, string Name, long Size) {
        /// <summary>
        /// 获取 SizeText
        /// </summary>
        public string SizeText => Size < 1024 * 1024
            ? $"{Size / 1024d:0.#} KB"
            : $"{Size / 1024d / 1024d:0.##} MB";
    }
}
