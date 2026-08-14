// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services.Storage;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 把封面地址换成对应的缩略图地址
/// </summary>
public sealed class CoverThumbnails(StoragePaths paths) {
    private readonly string _uploads = $"/{paths.PublicBasePath.Trim('/')}/";

    /// <summary>
    /// 取封面对应的缩略图地址，压不了的原样返回
    /// </summary>
    /// <param name="coverUrl">封面地址，可以为空</param>
    /// <returns>缩略图地址；不是本地附件时返回原地址</returns>
    public string For(string? coverUrl) {
        if (string.IsNullOrEmpty(coverUrl)) {
            return string.Empty;
        }

        return coverUrl.StartsWith(_uploads, StringComparison.OrdinalIgnoreCase)
            ? $"/thumbs/{coverUrl[_uploads.Length..]}"
            : coverUrl;
    }
}
