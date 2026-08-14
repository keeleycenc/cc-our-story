// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Storage;

/// <summary>
/// 本地附件在磁盘和 URL 上分别落在哪里，由 Web 层在启动时算好
/// </summary>
public class StoragePaths(string uploadsRoot, string thumbnailsRoot, string publicBasePath) {
    /// <summary>
    /// 获取上传目录的绝对路径
    /// </summary>
    public string UploadsRoot { get; } = uploadsRoot;

    /// <summary>
    /// 获取缩略图缓存目录的绝对路径
    /// </summary>
    /// <remarks>
    /// 在 uploads 外面：这里全是照着原图现生成的副本，
    /// 整个目录删掉也只是下次访问时重新生成一遍，不会丢失
    /// </remarks>
    public string ThumbnailsRoot { get; } = thumbnailsRoot;

    /// <summary>
    /// 获取对外的 URL 前缀，默认 /uploads
    /// </summary>
    public string PublicBasePath { get; } = publicBasePath;
}
