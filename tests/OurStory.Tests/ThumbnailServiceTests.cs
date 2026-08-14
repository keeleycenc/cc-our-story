// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Services.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 图片库缩略图生成测试
/// </summary>
public sealed class ThumbnailServiceTests : IDisposable {
    private const string Key = "ourstory/public/2026/08/photo.png";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ourstory-thumbs", Guid.NewGuid().ToString("n"));
    private readonly StoragePaths _paths;
    private readonly ThumbnailService _service;

    /// <summary>每个测试都在自己的临时目录里跑，互不干扰。</summary>
    public ThumbnailServiceTests() {
        var uploads = Path.Combine(_root, "uploads");
        var thumbs = Path.Combine(_root, "thumbs");

        _ = Directory.CreateDirectory(uploads);
        _ = Directory.CreateDirectory(thumbs);

        _paths = new StoragePaths(uploads, thumbs, "/uploads");
        _service = new ThumbnailService(_paths, NullLogger<ThumbnailService>.Instance);
    }

    /// <summary>长截图会被压成固定尺寸的小图，正是列表卡顿的那个源头。</summary>
    [Fact]
    public async Task 长截图会被压成固定尺寸() {
        Write(Key, 1200, 4000);

        var thumb = await _service.EnsureAsync(Key);

        Assert.NotNull(thumb);
        Assert.True(File.Exists(thumb));

        var info = await Image.IdentifyAsync(thumb!);
        Assert.Equal(480, info.Width);
        Assert.Equal(360, info.Height);
    }

    /// <summary>缩略图要比原图小得多，否则这一趟就白做了。</summary>
    [Fact]
    public async Task 缩略图明显小于原图() {
        Write(Key, 1200, 4000);

        var thumb = await _service.EnsureAsync(Key);

        var original = new FileInfo(Path.Combine(_paths.UploadsRoot, "ourstory", "public", "2026", "08", "photo.png"));
        Assert.True(new FileInfo(thumb!).Length < original.Length / 4);
    }

    /// <summary>动图只取第一帧：列表里要的是「这是张什么图」，不是让它一直转。</summary>
    [Fact]
    public async Task 动图只保留第一帧() {
        const string key = "ourstory/public/2026/08/loop.gif";
        WriteAnimation(key, 6);

        var thumb = await _service.EnsureAsync(key);

        using var image = await Image.LoadAsync(thumb!);
        Assert.Equal(1, image.Frames.Count);
    }

    /// <summary>第二次直接用缓存好的那份，不再重压一遍。</summary>
    [Fact]
    public async Task 已经压过的不会重做() {
        Write(Key, 800, 600);
        var first = await _service.EnsureAsync(Key);
        var stamp = File.GetLastWriteTimeUtc(first!);

        var second = await _service.EnsureAsync(Key);

        Assert.Equal(first, second);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(second!));
    }

    /// <summary>原图换成了别的内容，缩略图得跟着重做。</summary>
    [Fact]
    public async Task 原图变了会重新生成() {
        Write(Key, 800, 600);
        var first = await _service.EnsureAsync(Key);
        var stamp = File.GetLastWriteTimeUtc(first!);

        // 时间戳的精度有限，明确把原图推到之后，避免同一刻分不出先后
        Write(Key, 400, 300);
        File.SetLastWriteTimeUtc(Path.Combine(_paths.UploadsRoot, "ourstory", "public", "2026", "08", "photo.png"), stamp.AddSeconds(5));

        var second = await _service.EnsureAsync(Key);

        Assert.True(File.GetLastWriteTimeUtc(second!) > stamp);
    }

    /// <summary>带 .. 的对象键会被挡下来，不能顺着它摸到 uploads 外面去。</summary>
    [Theory]
    [InlineData("../../secrets.png")]
    [InlineData("ourstory/../../secrets.png")]
    [InlineData("")]
    public async Task 越界的对象键一律不处理(string key) =>
        Assert.Null(await _service.EnsureAsync(key));

    /// <summary>原图都不在了，交回 null 让调用方决定怎么兜。</summary>
    [Fact]
    public async Task 原图不存在时返回空() =>
        Assert.Null(await _service.EnsureAsync("ourstory/public/2026/08/missing.png"));

    /// <summary>删掉这次测试用的临时目录。</summary>
    public void Dispose() {
        try {
            if (Directory.Exists(_root)) {
                Directory.Delete(_root, true);
            }
        } catch (IOException) {
            // 临时目录留着就留着，不值得让测试为它失败
        }
    }

    private void Write(string key, int width, int height) {
        var path = Path.Combine(_paths.UploadsRoot, key.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var image = new Image<Rgba32>(width, height);
        image.Save(path);
    }

    private void WriteAnimation(string key, int frames) {
        var path = Path.Combine(_paths.UploadsRoot, key.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var image = new Image<Rgba32>(120, 90);
        for (var index = 1; index < frames; index++) {
            using var frame = new Image<Rgba32>(120, 90);
            _ = image.Frames.AddFrame(frame.Frames.RootFrame);
        }

        image.Save(path);
    }
}
