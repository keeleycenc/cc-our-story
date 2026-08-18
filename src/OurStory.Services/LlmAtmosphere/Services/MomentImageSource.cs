// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using OurStory.Core.Configuration;
using OurStory.Core.Entities;
using OurStory.Core.Options;
using OurStory.Services.Storage;
using System.Net;
using System.Text.RegularExpressions;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 读取点点滴滴中的图片并转换为 <c>data:</c> 内联资源，供支持图片理解的模型使用
/// </summary>
/// <remarks>
/// 本地附件优先读取宽度为 1440 的 WebP 派生图；OSS 图片则通过图片处理参数获取压缩后的版本。
/// 两种来源最终都会转换为适合模型分析的小尺寸图片，既避免依赖模型侧直接访问站点资源，
/// 也能减少原始大图带来的请求体积与传输开销。
/// </remarks>
internal sealed partial class MomentImageSource(
    StoragePaths paths,
    IThumbnailService thumbnails,
    IHttpClientFactory clients,
    ActiveConfiguration configuration,
    ILogger<MomentImageSource> logger) : IMomentImageSource {
    private const int MaxImageBytes = 4 * 1024 * 1024;  // 单张图的字节上限

    private readonly string _uploads = $"/{paths.PublicBasePath.Trim('/')}/";

    public async Task<IReadOnlyList<ResponsesImage>> CollectAsync(
        Moment moment,
        int max,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(moment);

        if (max <= 0) {
            return [];
        }

        var images = new List<ResponsesImage>();

        foreach (var url in Candidates(moment).Take(max)) {
            var image = await LoadAsync(url, cancellationToken);
            if (image is not null) {
                images.Add(image);
            }
        }

        return images;
    }

    #region 私有方法

    private static IEnumerable<string> Candidates(Moment moment) {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(moment.CoverUrl) && seen.Add(moment.CoverUrl)) {
            yield return moment.CoverUrl;
        }

        foreach (Match match in ImageSource().Matches(moment.ContentHtml ?? string.Empty)) {
            var url = WebUtility.HtmlDecode(match.Groups["value"].Value);
            if (url.Length > 0 && seen.Add(url)) {
                yield return url;
            }
        }
    }

    private async Task<ResponsesImage?> LoadAsync(string url, CancellationToken cancellationToken) {
        try {
            return LocalKey(url) is { } key
                ? await LocalAsync(key, cancellationToken)
                : await RemoteAsync(url, cancellationToken);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            logger.LogWarning(exception, "氛围组读取图片失败，这次改用纯文本：{Url}", url);
            return null;
        }
    }

    private async Task<ResponsesImage?> LocalAsync(string objectKey, CancellationToken cancellationToken) {
        var path = await thumbnails.EnsureAsync(objectKey, ImageVariant.Preview, cancellationToken);
        var type = "image/webp";

        if (path is null) {
            path = Path.Combine(paths.UploadsRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));
            type = ContentType(Path.GetExtension(path));

            if (type.Length == 0 || !File.Exists(path)) {
                return null;
            }
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Inline(bytes, type);
    }

    private async Task<ResponsesImage?> RemoteAsync(string url, CancellationToken cancellationToken) {
        if (!Uri.TryCreate(Resized(url), UriKind.Absolute, out var address)
            || address.Scheme is not ("http" or "https")) {
            return null;
        }

        try {
            using var response = await clients.CreateClient(ResponsesClient.HttpClientName)
                .GetAsync(address, cancellationToken);

            if (!response.IsSuccessStatusCode) {
                logger.LogWarning("氛围组取不到图片（{Status}）：{Url}", (int)response.StatusCode, url);
                return null;
            }

            var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return Inline(bytes, type);
        } catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) {
            logger.LogWarning(exception, "氛围组取图片超时或失败：{Url}", url);
            return null;
        }
    }

    private string Resized(string url) {
        var storage = configuration.Storage;
        if (storage.EffectiveDriver != StorageDriver.AliyunOss) {
            return url;
        }

        var origin = storage.Oss.PublicBaseUrl.TrimEnd('/');
        if (origin.Length == 0 || !url.StartsWith(origin, StringComparison.OrdinalIgnoreCase)) {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{url}{separator}x-oss-process=image/auto-orient,1/resize,m_lfit,w_{ImageVariant.Preview.Width}/format,webp";
    }

    private static ResponsesImage? Inline(byte[] bytes, string contentType) {
        if (bytes.Length is 0 or > MaxImageBytes) {
            return null;
        }

        return new ResponsesImage($"data:{contentType};base64,{Convert.ToBase64String(bytes)}");
    }

    private string? LocalKey(string url) {
        if (!url.StartsWith(_uploads, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var key = Uri.UnescapeDataString(url[_uploads.Length..]);
        return ObjectKeyFactory.IsSafe(key) ? key : null;
    }

    private static string ContentType(string extension) => extension.ToLowerInvariant() switch {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => string.Empty
    };

    [GeneratedRegex("""<img[^>]+src\s*=\s*["'](?<value>[^"']+)["']""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageSource();

    #endregion
}
