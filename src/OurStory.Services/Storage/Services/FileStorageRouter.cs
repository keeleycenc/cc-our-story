// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Core.Options;

namespace OurStory.Services.Storage;

/// <summary>
/// 按当前配置把附件读写转给本地目录或者 OSS
/// </summary>
/// <remarks>
/// 每次调用时才认驱动，所以后台把 OSS 参数填全、保存完就能直接传图，不用重启。
/// 选了 OSS 但参数没配全的一律退回本地
/// </remarks>
internal sealed class FileStorageRouter(
    ActiveConfiguration configuration,
    LocalFileStorage local,
    AliyunOssFileStorage oss) : IFileStorage {
    public string DriverName => Current.DriverName;

    private IFileStorage Current =>
        configuration.Storage.EffectiveDriver == StorageDriver.AliyunOss ? oss : local;

    public Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken = default) =>
        Current.SaveAsync(content, extension, contentType, cancellationToken);

    public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Current.DeleteAsync(objectKey, cancellationToken);

    public string PublicUrl(string objectKey) => Current.PublicUrl(objectKey);
}
