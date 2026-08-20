// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using OurStory.Core.Abstractions;
using OurStory.Core.Configuration;

namespace OurStory.Services.Storage;

// 本地目录存储。默认落在数据目录下的 uploads/，由静态文件中间件挂到 /uploads
public class LocalFileStorage(
    StoragePaths paths,
    ActiveConfiguration configuration,
    ILogger<LocalFileStorage> logger) : IFileStorage {
    public string DriverName => "本地目录";

    public async Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(content);

        var objectKey = ObjectKeyFactory.Create(configuration.Storage.Prefix, extension, DateTime.Now);
        var path = ResolvePath(objectKey) ?? throw new InvalidOperationException($"生成的附件路径不合法：{objectKey}");

        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);

        return objectKey;
    }

    public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default) {
        var path = ResolvePath(objectKey);
        if (path is null) {
            return Task.FromResult(false);
        }

        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }

            return Task.FromResult(true);
        } catch (IOException exception) {
            logger.LogWarning(exception, "删除本地附件失败：{ObjectKey}", objectKey);
            return Task.FromResult(false);
        } catch (UnauthorizedAccessException exception) {
            logger.LogWarning(exception, "没有权限删除本地附件：{ObjectKey}", objectKey);
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) {
        if (!Directory.Exists(paths.UploadsRoot)) {
            return Task.FromResult<IReadOnlyList<StoredFile>>([]);
        }

        var files = new DirectoryInfo(paths.UploadsRoot)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(file => new StoredFile(
                Path.GetRelativePath(paths.UploadsRoot, file.FullName).Replace('\\', '/'),
                file.Length,
                file.LastWriteTimeUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<StoredFile>>(files);
    }

    public string PublicUrl(string objectKey) =>
        $"{paths.PublicBasePath.TrimEnd('/')}/{EncodeKey(objectKey)}";

    private string? ResolvePath(string objectKey) {
        if (!ObjectKeyFactory.IsSafe(objectKey)) {
            return null;
        }

        var relative = objectKey.Replace('\\', '/').Trim('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(paths.UploadsRoot, relative);
    }

    internal static string EncodeKey(string objectKey) =>
        string.Join('/', objectKey.Trim('/').Split('/').Select(Uri.EscapeDataString));
}
