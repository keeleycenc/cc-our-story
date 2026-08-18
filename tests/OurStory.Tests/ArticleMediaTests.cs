// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OurStory.Core.Configuration;
using OurStory.Services.Storage;
using OurStory.Web.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 正文配图改写规则的测试
/// </summary>
public sealed class ArticleMediaTests : IDisposable {
    private const string Key = "ourstory/public/2026/08/photo.png";
    private const string Url = "/uploads/ourstory/public/2026/08/photo.png";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ourstory-article", Guid.NewGuid().ToString("n"));
    private readonly ArticleMedia _article;

    /// <summary>每个测试都在自己的临时目录里跑，正文里引的那张图是真的存在的。</summary>
    public ArticleMediaTests() {
        var uploads = Path.Combine(_root, "uploads");
        var thumbs = Path.Combine(_root, "thumbs");

        _ = Directory.CreateDirectory(uploads);
        _ = Directory.CreateDirectory(thumbs);

        var path = Path.Combine(uploads, Key.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var image = new Image<Rgba32>(1600, 800)) {
            image.Save(path);
        }

        var paths = new StoragePaths(uploads, thumbs, "/uploads");
        var configuration = new ActiveConfiguration(new ConfigurationStore("."), new OurStoryConfiguration());

        _article = new ArticleMedia(
            new MediaUrls(paths, configuration),
            new ThumbnailService(paths, new MemoryCache(new MemoryCacheOptions()), NullLogger<ThumbnailService>.Instance),
            // 站点把整个 Unicode 都放行了，中文照原样输出，这里跟着来
            HtmlEncoder.Create(UnicodeRanges.All));
    }

    /// <summary>独占一段的图换成 figure，页面上显示的是压过的那一份。</summary>
    [Fact]
    public async Task 独占一段的图换成figure() {
        var html = await RenderAsync($"""<p><img src="{Url}" alt="海边" /></p>""");

        Assert.Contains("<figure class=\"article-figure\">", html, StringComparison.Ordinal);
        Assert.Contains("/media/preview/ourstory/public/2026/08/photo.png", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"海边\"", html, StringComparison.Ordinal);
    }

    /// <summary>原图地址只挂在 data-full 上，点开查看器才去取它。</summary>
    [Fact]
    public async Task 原图地址交给查看器() {
        var html = await RenderAsync($"""<p><img src="{Url}" alt="" /></p>""");

        Assert.Contains($"data-full=\"{Url}\"", html, StringComparison.Ordinal);
        Assert.Contains("data-lightbox=\"moment-x\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"/media/preview/", html, StringComparison.Ordinal);
    }

    /// <summary>视口附近才加载：原生懒加载，外加提前写死的宽高。</summary>
    [Fact]
    public async Task 懒加载并占好位置() {
        var html = await RenderAsync($"""<p><img src="{Url}" alt="" /></p>""");

        Assert.Contains("loading=\"lazy\"", html, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", html, StringComparison.Ordinal);
        Assert.Contains("width=\"1600\" height=\"800\"", html, StringComparison.Ordinal);
        Assert.Contains("--figure-ratio:1600/800", html, StringComparison.Ordinal);
    }

    /// <summary>骨架屏认的是 data-cover，和封面共用 core.js 里那一段。</summary>
    [Fact]
    public async Task 挂上骨架屏的标记() =>
        Assert.Contains("data-cover", await RenderAsync($"""<p><img src="{Url}" alt="" /></p>"""), StringComparison.Ordinal);

    /// <summary>同一篇里的图编上号，查看器里就能左右翻。</summary>
    [Fact]
    public async Task 同一篇里的图依次编号() {
        var html = await RenderAsync($"""<p><img src="{Url}" alt="" /></p><p><img src="{Url}" alt="" /></p>""");

        Assert.Contains("data-index=\"0\"", html, StringComparison.Ordinal);
        Assert.Contains("data-index=\"1\"", html, StringComparison.Ordinal);
    }

    /// <summary>title 既做图注也做查看器里的说明。</summary>
    [Fact]
    public async Task Title当图注() {
        var html = await RenderAsync($"""<p><img src="{Url}" alt="" title="那年夏天" /></p>""");

        Assert.Contains("<figcaption>那年夏天</figcaption>", html, StringComparison.Ordinal);
        Assert.Contains("data-caption=\"那年夏天\"", html, StringComparison.Ordinal);
    }

    /// <summary>混在文字里的图只包一层按钮，不能把 figure 塞进 p 里。</summary>
    [Fact]
    public async Task 行内的图不套figure() {
        var html = await RenderAsync($"""<p>今天<img src="{Url}" alt="" />真好</p>""");

        Assert.DoesNotContain("<figure", html, StringComparison.Ordinal);
        Assert.Contains("class=\"article-figure-frame\"", html, StringComparison.Ordinal);
    }

    /// <summary>作者自己给图套了链接，那是要跳去别处，别抢过来当查看器用。</summary>
    [Fact]
    public async Task 套了链接的图原样留着() {
        var html = await RenderAsync($"""<p><a href="https://example.com"><img src="{Url}" alt="" /></a></p>""");

        Assert.DoesNotContain("data-lightbox", html, StringComparison.Ordinal);
        Assert.Contains($"""<img src="{Url}" alt="" />""", html, StringComparison.Ordinal);
    }

    /// <summary>站外的图压不了，也就没有小图可给，原样输出。</summary>
    [Fact]
    public async Task 站外的图原样输出() {
        var html = await RenderAsync("""<p><img src="https://other.example.org/a.png" alt="" /></p>""");

        Assert.DoesNotContain("data-lightbox", html, StringComparison.Ordinal);
    }

    /// <summary>正文里根本没有图时，一个字都不改。</summary>
    [Fact]
    public async Task 没有图时原样返回() =>
        Assert.Equal("<p>只有文字</p>", await RenderAsync("<p>只有文字</p>"));

    /// <summary>空正文交回空内容，模板那边照旧按「没写」处理。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task 空正文返回空内容(string? html) =>
        Assert.Equal(string.Empty, await RenderAsync(html));

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

    private async Task<string> RenderAsync(string? html) {
        var content = await _article.RenderAsync(html, "moment-x");
        await using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
