// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Core.Options;
using OurStory.Services.Storage;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 把封面地址换成对应的缩略图地址
/// </summary>
public sealed class CoverThumbnails(StoragePaths paths, ActiveConfiguration configuration) {
    // m_fill 的取景和本地那套 ResizeMode.Crop 一致；动图它默认也只取第一帧。
    // auto-orient 要排在 resize 前面：OSS 默认不理会 EXIF 方向
    private static readonly string OssProcess =
        $"x-oss-process=image/auto-orient,1/resize,m_fill,w_{ThumbnailSize.Width},h_{ThumbnailSize.Height}/format,webp";

    private readonly string _uploads = $"/{paths.PublicBasePath.Trim('/')}/";

    /// <summary>
    /// 取封面对应的缩略图地址，压不了的原样返回
    /// </summary>
    /// <param name="coverUrl">封面地址，可以为空</param>
    /// <returns>缩略图地址；两种存储都压不了时返回原地址</returns>
    public string For(string? coverUrl) {
        if (string.IsNullOrEmpty(coverUrl)) {
            return string.Empty;
        }

        // 本地附件的地址是 PublicUrl 拼出来的，后半截已经转义过，原样接到 /thumbs 后面就行
        if (coverUrl.StartsWith(_uploads, StringComparison.OrdinalIgnoreCase)) {
            return $"/thumbs/{coverUrl[_uploads.Length..]}";
        }

        return OssUrl(coverUrl) ?? coverUrl;
    }

    private string? OssUrl(string coverUrl) {
        var storage = configuration.Storage;

        // 参数没配全时 EffectiveDriver 会退回本地，这里也就不会走进来
        if (storage.EffectiveDriver != StorageDriver.AliyunOss) {
            return null;
        }

        var origin = storage.Oss.PublicBaseUrl.TrimEnd('/');
        if (!coverUrl.StartsWith(origin, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return coverUrl + (coverUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?') + OssProcess;
    }
}
