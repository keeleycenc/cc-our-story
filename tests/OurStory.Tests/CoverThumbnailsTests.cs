// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Core.Options;
using OurStory.Services.Storage;
using OurStory.Web.Infrastructure;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 封面地址换缩略图地址的规则测试
/// </summary>
public class CoverThumbnailsTests {
    private const string OssOrigin = "https://img.example.com";
    private const string OssProcess = "?x-oss-process=image/resize,m_fill,w_720,h_540/format,webp";

    /// <summary>本地附件换成 /thumbs 下同名的那一份。</summary>
    [Fact]
    public void 本地附件换成缩略图地址() =>
        Assert.Equal(
            "/thumbs/ourstory/public/2026/08/photo.png",
            Local().For("/uploads/ourstory/public/2026/08/photo.png"));

    /// <summary>存本地时，站外地址没人能压，原样返回。</summary>
    [Theory]
    [InlineData("https://img.example.com/2026/08/photo.png")]
    [InlineData("http://example.com/photo.jpg")]
    [InlineData("/static/theme-cover.png")]
    [InlineData("data:image/gif;base64,R0lGOD")]
    public void 存本地时站外地址原样返回(string url) =>
        Assert.Equal(url, Local().For(url));

    /// <summary>没有封面时给空串，视图那边照旧按「没有封面」处理。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 没有封面时返回空串(string? url) =>
        Assert.Equal(string.Empty, Local().For(url));

    /// <summary>前缀只认开头，路径中间出现 /uploads/ 不算。</summary>
    [Fact]
    public void 只认开头的前缀() =>
        Assert.Equal(
            "https://cdn.example.com/uploads/photo.png",
            Local().For("https://cdn.example.com/uploads/photo.png"));

    /// <summary>切到 OSS 之后，自家 Bucket 的图挂上 OSS 的处理参数，交给阿里云压。</summary>
    [Fact]
    public void 走OSS时挂上图片处理参数() =>
        Assert.Equal(
            $"{OssOrigin}/ourstory/public/2026/08/photo.png{OssProcess}",
            Oss().For($"{OssOrigin}/ourstory/public/2026/08/photo.png"));

    /// <summary>尺寸和本地那套压出来的必须是同一个，否则两边取景就对不上了。</summary>
    [Fact]
    public void OSS参数用的是同一个尺寸() {
        var url = Oss().For($"{OssOrigin}/photo.png");

        Assert.Contains($"w_{ThumbnailSize.Width}", url, StringComparison.Ordinal);
        Assert.Contains($"h_{ThumbnailSize.Height}", url, StringComparison.Ordinal);
    }

    /// <summary>地址上已经有查询串时用 &amp; 接着拼，别拼出第二个问号。</summary>
    [Fact]
    public void 已有查询串时用与号拼接() =>
        Assert.Equal(
            $"{OssOrigin}/photo.png?v=2&{OssProcess[1..]}",
            Oss().For($"{OssOrigin}/photo.png?v=2"));

    /// <summary>站外的图不归我们压，挂上参数只会给别人的 CDN 添乱。</summary>
    [Fact]
    public void 走OSS时站外地址仍旧原样返回() =>
        Assert.Equal(
            "https://other.example.org/photo.png",
            Oss().For("https://other.example.org/photo.png"));

    /// <summary>切到 OSS 之后，早先存的本地地址照旧走自己的 /thumbs。</summary>
    [Fact]
    public void 走OSS时旧的本地封面仍走本地缩略图() =>
        Assert.Equal(
            "/thumbs/ourstory/public/2026/08/old.png",
            Oss().For("/uploads/ourstory/public/2026/08/old.png"));

    /// <summary>OSS 参数没配全时 EffectiveDriver 退回本地，不该再挂处理参数。</summary>
    [Fact]
    public void OSS没配全时不挂参数() {
        var configuration = new OurStoryConfiguration();
        configuration.Storage.Driver = StorageDriver.AliyunOss;
        configuration.Storage.Oss.PublicBaseUrl = OssOrigin;   // 只填了域名，密钥那几项都缺

        var thumbs = Build(configuration);

        Assert.Equal($"{OssOrigin}/photo.png", thumbs.For($"{OssOrigin}/photo.png"));
    }

    private static CoverThumbnails Local() => Build(new OurStoryConfiguration());

    private static CoverThumbnails Oss() {
        var configuration = new OurStoryConfiguration();
        configuration.Storage.Driver = StorageDriver.AliyunOss;
        configuration.Storage.Oss.Region = "cn-beijing";
        configuration.Storage.Oss.Bucket = "our-story";
        configuration.Storage.Oss.AccessKeyId = "id";
        configuration.Storage.Oss.AccessKeySecret = "secret";
        configuration.Storage.Oss.PublicBaseUrl = OssOrigin;

        return Build(configuration);
    }

    private static CoverThumbnails Build(OurStoryConfiguration configuration) =>
        new(new StoragePaths("/data/uploads", "/data/thumbs", "/uploads"),
            new ActiveConfiguration(new ConfigurationStore("."), configuration));
}
