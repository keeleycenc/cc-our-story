// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Core.Options;
using OurStory.Services.Storage;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 图片资源地址转换服务。
/// 
/// 根据原始图片地址和目标规格生成对应的派生图片地址：
/// 本地存储通过站内媒体接口访问，OSS 存储通过图片处理参数生成，
/// 两种方式保持相同的尺寸约束和裁剪策略
/// </summary>
public sealed class MediaUrls(StoragePaths paths, ActiveConfiguration configuration) {
    private readonly string _uploads = $"/{paths.PublicBasePath.Trim('/')}/";

    /// <summary>
    /// 获取封面规格图片地址。
    /// </summary>
    /// <param name="url">原始图片地址，可以为空</param>
    /// <returns>
    /// 封面规格地址；
    /// 不支持生成派生图片时返回原始地址。
    /// </returns>
    public string Cover(string? url) => For(url, ImageVariant.Cover);

    /// <summary>
    /// 获取正文展示规格图片地址。
    /// </summary>
    /// <param name="url">原始图片地址，可以为空</param>
    /// <returns>
    /// 正文展示规格地址；
    /// 不支持生成派生图片时返回原始地址。
    /// </returns>
    public string Preview(string? url) => For(url, ImageVariant.Preview);

    /// <summary>
    /// 根据指定规格生成图片地址。
    /// </summary>
    /// <param name="url">原始图片地址，可以为空</param>
    /// <param name="variant">目标图片规格</param>
    /// <returns>
    /// 派生图片地址；
    /// 无法处理时返回原始地址。
    /// </returns>
    public string For(string? url, ImageVariant variant) {
        ArgumentNullException.ThrowIfNull(variant);

        if (string.IsNullOrEmpty(url)) {
            return string.Empty;
        }

        // 本地附件地址已包含公开路径，转换为媒体接口路径。
        if (url.StartsWith(_uploads, StringComparison.OrdinalIgnoreCase)) {
            return $"/media/{variant.Name}/{url[_uploads.Length..]}";
        }

        return OssUrl(url, variant) ?? url;
    }

    /// <summary>
    /// 判断图片地址是否支持生成派生规格。
    /// </summary>
    /// <param name="url">原始图片地址，可以为空</param>
    /// <returns>支持生成派生规格时返回 true</returns>
    public bool CanResize(string? url) => !string.IsNullOrEmpty(url) && For(url, ImageVariant.Preview) != url;

    /// <summary>
    /// 获取本地附件对应的对象键。
    /// </summary>
    /// <param name="url">原始图片地址，可以为空</param>
    /// <returns>
    /// 本地附件对象键；
    /// 非本地资源或地址不安全时返回 null。
    /// </returns>
    public string? LocalKey(string? url) {
        if (string.IsNullOrEmpty(url) || !url.StartsWith(_uploads, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var key = Uri.UnescapeDataString(url[_uploads.Length..]);
        return ObjectKeyFactory.IsSafe(key) ? key : null;
    }

    /// <remarks>
    /// auto-orient 必须在 resize 前执行，否则 OSS 不会根据 EXIF 方向修正图片。
    /// m_fill 对应本地裁剪模式，保持固定尺寸输出；
    /// m_lfit 对应等比例缩放，只限制最大宽度且不会放大图片
    /// </remarks>
    private static string Process(ImageVariant variant) => variant.Crop
        ? $"x-oss-process=image/auto-orient,1/resize,m_fill,w_{variant.Width},h_{variant.Height}/format,webp"
        : $"x-oss-process=image/auto-orient,1/resize,m_lfit,w_{variant.Width}/format,webp";

    private string? OssUrl(string url, ImageVariant variant) {
        var storage = configuration.Storage;

        if (storage.EffectiveDriver != StorageDriver.AliyunOss) {
            return null;
        }

        var origin = storage.Oss.PublicBaseUrl.TrimEnd('/');
        if (!url.StartsWith(origin, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return url + (url.Contains('?', StringComparison.Ordinal) ? '&' : '?') + Process(variant);
    }
}
