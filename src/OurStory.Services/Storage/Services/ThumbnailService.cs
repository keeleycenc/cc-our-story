// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace OurStory.Services.Storage;

// 派生图只对本地目录有意义：OSS 那边交给它自己的图片处理参数
internal sealed class ThumbnailService(
    StoragePaths paths,
    IMemoryCache cache,
    ILogger<ThumbnailService> logger) : IThumbnailService {
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly DecoderOptions Decoding = new() {
        MaxFrames = 1
    };

    public async Task<string?> EnsureAsync(string objectKey, ImageVariant? variant = null, CancellationToken cancellationToken = default) {
        var wanted = variant ?? ImageVariant.Cover;
        var source = SourcePath(objectKey);
        if (source is null) {
            return null;
        }

        // 规格各占一个目录，连着原来的扩展名再加一层 .webp：
        // 不同格式的同名文件不会撞在一起
        var cached = Path.Combine(paths.ThumbnailsRoot, wanted.Name, Relative(objectKey) + ".webp");
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

            image.Mutate(context => {
                _ = context.AutoOrient();

                if (wanted.Crop) {
                    _ = context.Resize(new ResizeOptions {
                        Size = new Size(wanted.Width, wanted.Height),
                        Mode = ResizeMode.Crop
                    });
                    return;
                }

                // 只压过宽的那些，本来就窄的原样留着，别放大糊了
                if (context.GetCurrentSize().Width > wanted.Width) {
                    _ = context.Resize(wanted.Width, 0);
                }
            });

            await image.SaveAsWebpAsync(cached, cancellationToken);
            return cached;
        } catch (Exception exception) when (
            exception is ImageFormatException
                or ImageProcessingException
                or NotSupportedException
                or IOException
                or UnauthorizedAccessException) {
            logger.LogWarning(exception, "生成派生图失败：{ObjectKey}", objectKey);
            return null;
        } finally {
            _ = Gate.Release();
        }
    }

    public async Task<ImageSize?> MeasureAsync(string objectKey, CancellationToken cancellationToken = default) {
        var source = SourcePath(objectKey);
        if (source is null) {
            return null;
        }

        var key = $"image-size:{source}:{File.GetLastWriteTimeUtc(source).Ticks}";
        if (cache.TryGetValue<ImageSize?>(key, out var hit)) {
            return hit;
        }

        ImageSize? size = null;

        try {
            var info = await Image.IdentifyAsync(source, cancellationToken);
            size = Swapped(info) ? new ImageSize(info.Height, info.Width) : new ImageSize(info.Width, info.Height);
        } catch (Exception exception) when (
            exception is ImageFormatException
                or NotSupportedException
                or IOException
                or UnauthorizedAccessException) {
            logger.LogWarning(exception, "读取图片尺寸失败：{ObjectKey}", objectKey);
        }

        _ = cache.Set(key, size, TimeSpan.FromHours(12));
        return size;
    }

    private static bool Swapped(ImageInfo info) =>
        info.Metadata.ExifProfile is { } exif
            && exif.TryGetValue(ExifTag.Orientation, out var orientation)
            && orientation.Value is 5 or 6 or 7 or 8;

    private string? SourcePath(string objectKey) {
        if (!ObjectKeyFactory.IsSafe(objectKey)) {
            return null;
        }

        var source = Path.Combine(paths.UploadsRoot, Relative(objectKey));
        return File.Exists(source) ? source : null;
    }

    private static string Relative(string objectKey) =>
        objectKey.Replace('\\', '/').Trim('/').Replace('/', Path.DirectorySeparatorChar);

    private static bool IsFresh(string cached, string source) =>
        File.Exists(cached) && File.GetLastWriteTimeUtc(cached) >= File.GetLastWriteTimeUtc(source);
}
