// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace OurStory.Services.Storage;

// 缩略图只对本地目录有意义：OSS 那边的图片库不需要列清单
internal sealed class ThumbnailService(StoragePaths paths, ILogger<ThumbnailService> logger) : IThumbnailService {
    private const int Width = 480;
    private const int Height = 360;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly DecoderOptions Decoding = new() {
        MaxFrames = 1
    };

    public async Task<string?> EnsureAsync(string objectKey, CancellationToken cancellationToken = default) {
        if (!ObjectKeyFactory.IsSafe(objectKey)) {
            return null;
        }

        var relative = objectKey.Replace('\\', '/').Trim('/').Replace('/', Path.DirectorySeparatorChar);
        var source = Path.Combine(paths.UploadsRoot, relative);
        if (!File.Exists(source)) {
            return null;
        }

        // 连着原来的扩展名再加一层 .webp：不同格式的同名文件不会撞在一起
        var cached = Path.Combine(paths.ThumbnailsRoot, relative + ".webp");
        if (IsFresh(cached, source)) {
            return cached;
        }

        await Gate.WaitAsync(cancellationToken);

        try {
            if (IsFresh(cached, source)) {
                return cached;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(cached)!);

            using var image = await Image.LoadAsync(Decoding, source, cancellationToken);
            image.Mutate(context => context.Resize(new ResizeOptions {
                Size = new Size(Width, Height),
                Mode = ResizeMode.Crop
            }));

            await image.SaveAsWebpAsync(cached, cancellationToken);
            return cached;
        } catch (Exception exception) when (
            exception is ImageFormatException
                or ImageProcessingException
                or NotSupportedException
                or IOException
                or UnauthorizedAccessException) {
            logger.LogWarning(exception, "生成缩略图失败：{ObjectKey}", objectKey);
            return null;
        } finally {
            _ = Gate.Release();
        }
    }

    private static bool IsFresh(string cached, string source) =>
        File.Exists(cached) && File.GetLastWriteTimeUtc(cached) >= File.GetLastWriteTimeUtc(source);
}
