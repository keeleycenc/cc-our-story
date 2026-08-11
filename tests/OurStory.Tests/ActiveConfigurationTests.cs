// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
using OurStory.Core.Configuration;
using OurStory.Core.Options;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 表示 ActiveConfigurationTests
/// </summary>
public sealed class ActiveConfigurationTests : IDisposable {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ourstory-active-" + Guid.NewGuid().ToString("n"));
    private readonly ConfigurationStore _store;
    private readonly ActiveConfiguration _configuration;

    /// <summary>
    /// 执行 ActiveConfigurationTests 操作
    /// </summary>
    public ActiveConfigurationTests() {
        _ = Directory.CreateDirectory(_root);
        _store = new ConfigurationStore(_root);
        _configuration = new ActiveConfiguration(_store, new OurStoryConfiguration());
    }

    /// <summary>
    /// 清掉临时目录
    /// </summary>
    public void Dispose() {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_root)) {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// 验证保存之后立刻生效并落盘()
    /// </summary>
    [Fact]
    public void 保存之后立刻生效并落盘() {
        Assert.True(_configuration.Update(next => next.Site.TimeZone = "Asia/Tokyo", out var error));
        Assert.Null(error);

        // 内存里的当场就是新的，不用重启
        Assert.Equal("Asia/Tokyo", _configuration.Site.TimeZone);

        // 文件里也是新的，重启之后还在
        Assert.Equal("Asia/Tokyo", _store.Load(_ => null).Configuration.Site.TimeZone);
    }

    /// <summary>
    /// 验证后台填全OSS参数后马上切到OSS()
    /// </summary>
    [Fact]
    public void 后台填全OSS参数后马上切到OSS() {
        Assert.Equal(StorageDriver.Local, _configuration.Storage.EffectiveDriver);

        _ = _configuration.Update(next => {
            next.Storage.Oss.Region = "cn-beijing";
            next.Storage.Oss.Bucket = "our-bucket";
            next.Storage.Oss.AccessKeyId = "id";
            next.Storage.Oss.AccessKeySecret = "secret";
            next.Storage.Oss.PublicBaseUrl = "https://img.example.com";
        }, out _);

        Assert.Equal(StorageDriver.AliyunOss, _configuration.Storage.EffectiveDriver);
    }

    /// <summary>
    /// 验证改动只落在副本上原来那份不受影响()
    /// </summary>
    [Fact]
    public void 改动只落在副本上原来那份不受影响() {
        var before = _configuration.Current;

        _ = _configuration.Update(next => next.Storage.Prefix = "ourstory/private", out _);

        Assert.Equal("ourstory/public", before.Storage.Prefix);
        Assert.Equal("ourstory/private", _configuration.Storage.Prefix);
    }

    /// <summary>
    /// 验证写不进文件时保持原样并说明原因()
    /// </summary>
    [Fact]
    public void 写不进文件时保持原样并说明原因() {
        // 配置文件的位置被一个目录占着，写不下去 —— 只读挂载也是同一类情况
        _ = Directory.CreateDirectory(_store.FilePath);

        Assert.False(_configuration.Update(next => next.Site.TimeZone = "Asia/Tokyo", out var error));
        Assert.NotNull(error);

        // 没写成就不能改内存里这份，否则页面显示的和实际跑的对不上
        Assert.Equal("Asia/Shanghai", _configuration.Site.TimeZone);
    }
}
