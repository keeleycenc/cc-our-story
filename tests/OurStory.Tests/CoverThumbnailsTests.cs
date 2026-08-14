// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services.Storage;
using OurStory.Web.Infrastructure;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 封面地址换缩略图地址的规则测试
/// </summary>
public class CoverThumbnailsTests {
    private static readonly CoverThumbnails Thumbs = new(new StoragePaths("/data/uploads", "/data/thumbs", "/uploads"));

    /// <summary>本地附件换成 /thumbs 下同名的那一份。</summary>
    [Fact]
    public void 本地附件换成缩略图地址() =>
        Assert.Equal(
            "/thumbs/ourstory/public/2026/08/photo.png",
            Thumbs.For("/uploads/ourstory/public/2026/08/photo.png"));

    /// <summary>OSS 和站外的图不归我们压，原样返回。</summary>
    [Theory]
    [InlineData("https://img.example.com/2026/08/photo.png")]
    [InlineData("http://example.com/photo.jpg")]
    [InlineData("/static/theme-cover.png")]
    [InlineData("data:image/gif;base64,R0lGOD")]
    public void 站外地址原样返回(string url) =>
        Assert.Equal(url, Thumbs.For(url));

    /// <summary>没有封面时给空串，视图那边照旧按「没有封面」处理。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 没有封面时返回空串(string? url) =>
        Assert.Equal(string.Empty, Thumbs.For(url));

    /// <summary>前缀只认开头，路径中间出现 /uploads/ 不算。</summary>
    [Fact]
    public void 只认开头的前缀() =>
        Assert.Equal(
            "https://cdn.example.com/uploads/photo.png",
            Thumbs.For("https://cdn.example.com/uploads/photo.png"));
}
