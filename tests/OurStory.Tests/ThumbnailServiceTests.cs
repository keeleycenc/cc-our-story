// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Services.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
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
        _service = new ThumbnailService(
            _paths,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ThumbnailService>.Instance);
    }

    /// <summary>长截图会被压成固定尺寸的小图，正是列表卡顿的那个源头。</summary>
    [Fact]
    public async Task 长截图会被压成固定尺寸() {
        Write(Key, 1200, 4000);

        var thumb = await _service.EnsureAsync(Key);

        Assert.NotNull(thumb);
        Assert.True(File.Exists(thumb));

        var info = await Image.IdentifyAsync(thumb!);
        Assert.Equal(720, info.Width);
        Assert.Equal(540, info.Height);
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

    /// <summary>手机拍的竖图靠 EXIF 摆正，缩略图得先摆正再裁，不能躺着出来。</summary>
    [Fact]
    public async Task 按EXIF方向摆正后再裁切() {
        // 竖着拍的照片就是这样：像素 1200x900 横着存，左半边红右半边蓝，
        // 靠 Orientation=6（顺时针转 90°）才是拍的时候看到的样子。
        // 摆正后红的那半边在上，没摆正则一直在左边
        const string key = "ourstory/public/2026/08/portrait.jpg";
        WriteRotated(key, 1200, 900);

        var thumb = await _service.EnsureAsync(key);

        using var image = await Image.LoadAsync<Rgba32>(thumb!);

        Assert.Equal(720, image.Width);
        Assert.Equal(540, image.Height);

        // webp 是有损的，比通道大小就够了，不必对着具体数值较真
        var top = image[360, 20];
        var bottom = image[360, 520];

        Assert.True(top.R > top.B, $"上半边该是红的，实际 {top}");
        Assert.True(bottom.B > bottom.R, $"下半边该是蓝的，实际 {bottom}");
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

    /// <summary>正文那一档不裁，只把过宽的压到限制以内，比例得原样留着。</summary>
    [Fact]
    public async Task 正文配图只限宽不裁切() {
        Write(Key, 3000, 1000);

        var preview = await _service.EnsureAsync(Key, ImageVariant.Preview);

        var info = await Image.IdentifyAsync(preview!);
        Assert.Equal(ImageVariant.Preview.Width, info.Width);
        Assert.Equal(ImageVariant.Preview.Width / 3, info.Height);
    }

    /// <summary>本来就没超宽的别放大，放大只会糊。</summary>
    [Fact]
    public async Task 窄图不会被放大() {
        Write(Key, 600, 400);

        var preview = await _service.EnsureAsync(Key, ImageVariant.Preview);

        var info = await Image.IdentifyAsync(preview!);
        Assert.Equal(600, info.Width);
        Assert.Equal(400, info.Height);
    }

    /// <summary>两档规格各存各的，不能互相顶掉对方那份缓存。</summary>
    [Fact]
    public async Task 两档规格各存一份() {
        Write(Key, 2000, 1500);

        var cover = await _service.EnsureAsync(Key, ImageVariant.Cover);
        var preview = await _service.EnsureAsync(Key, ImageVariant.Preview);

        Assert.NotEqual(cover, preview);
        Assert.True(File.Exists(cover));
        Assert.True(File.Exists(preview));
    }

    /// <summary>原图删除后，两档派生图都不能留在缓存目录里。</summary>
    [Fact]
    public async Task 清理会删除这张图的全部派生缓存() {
        Write(Key, 2000, 1500);
        var cover = await _service.EnsureAsync(Key, ImageVariant.Cover);
        var preview = await _service.EnsureAsync(Key, ImageVariant.Preview);

        await _service.ClearAsync(Key);

        Assert.False(File.Exists(cover));
        Assert.False(File.Exists(preview));
    }

    /// <summary>正文里的图要先占好位置，所以得量得出原图尺寸。</summary>
    [Fact]
    public async Task 量得出原图尺寸() {
        Write(Key, 1280, 960);

        var size = await _service.MeasureAsync(Key);

        Assert.Equal(new ImageSize(1280, 960), size);
    }

    /// <summary>竖着拍的照片报的必须是摆正之后的尺寸，否则占位框会横过来。</summary>
    [Fact]
    public async Task 竖拍照片报摆正后的尺寸() {
        const string key = "ourstory/public/2026/08/portrait.jpg";
        WriteRotated(key, 1200, 900);

        var size = await _service.MeasureAsync(key);

        Assert.Equal(new ImageSize(900, 1200), size);
    }

    /// <summary>量不着的交回 null，模板那边退回默认比例。</summary>
    [Fact]
    public async Task 原图不在时量不出尺寸() =>
        Assert.Null(await _service.MeasureAsync("ourstory/public/2026/08/missing.png"));

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

    // 左半边红、右半边蓝，再打上 Orientation=6：摆正后红的那半边应该在上面
    private void WriteRotated(string key, int width, int height) {
        var path = Path.Combine(_paths.UploadsRoot, key.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor => {
            for (var y = 0; y < accessor.Height; y++) {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) {
                    row[x] = x < width / 2 ? Color.Red : Color.Blue;
                }
            }
        });

        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)ExifOrientationMode.RightTop);

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
