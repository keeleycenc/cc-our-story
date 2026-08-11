// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Core.Options;
using Xunit;

namespace OurStory.Tests;

/// <summary>
/// 表示 ConfigurationStoreTests
/// </summary>
public sealed class ConfigurationStoreTests : IDisposable {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ourstory-config-" + Guid.NewGuid().ToString("n"));

    /// <summary>
    /// 执行 ConfigurationStoreTests 操作
    /// </summary>
    public ConfigurationStoreTests() => Directory.CreateDirectory(_root);

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
    /// 验证配置文件不存在时用默认值并生成一份模板()
    /// </summary>
    [Fact]
    public void 配置文件不存在时用默认值并生成一份模板() {
        var store = new ConfigurationStore(_root);

        var result = store.Load(_ => null);

        Assert.Equal(ConfigurationSource.Created, result.Source);
        Assert.Equal("Asia/Shanghai", result.Configuration.Site.TimeZone);
        Assert.True(File.Exists(store.FilePath));
        Assert.Contains("Asia/Shanghai", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证写下去的配置读得回来()
    /// </summary>
    [Fact]
    public void 写下去的配置读得回来() {
        var store = new ConfigurationStore(_root);
        var configuration = new OurStoryConfiguration {
            Site = new SiteOptions { TimeZone = "Europe/Paris", Seed = new SeedAccountOptions { BoyUserName = "阿泽" } },
            Storage = new StorageOptions { Driver = StorageDriver.Local, Prefix = "ourstory/private" }
        };

        Assert.True(store.TrySave(configuration, out var error));
        Assert.Null(error);

        var result = store.Load(_ => null);

        Assert.Equal(ConfigurationSource.File, result.Source);
        Assert.Equal("Europe/Paris", result.Configuration.Site.TimeZone);
        Assert.Equal("阿泽", result.Configuration.Site.Seed.BoyUserName);
        Assert.Equal(StorageDriver.Local, result.Configuration.Storage.Driver);
        Assert.Equal("ourstory/private", result.Configuration.Storage.Prefix);
    }

    /// <summary>
    /// 验证枚举和中文都按原样写进文件()
    /// </summary>
    [Fact]
    public void 枚举和中文都按原样写进文件() {
        // 这份文件是给人手改的：Driver 写成 0 或者中文变成 阿 都没法看
        var store = new ConfigurationStore(_root);
        var configuration = new OurStoryConfiguration {
            Site = new SiteOptions { Seed = new SeedAccountOptions { BoyUserName = "阿泽" } },
            Storage = new StorageOptions { Driver = StorageDriver.AliyunOss }
        };

        _ = store.TrySave(configuration, out _);
        var text = File.ReadAllText(store.FilePath);

        Assert.Contains("\"AliyunOss\"", text, StringComparison.Ordinal);
        Assert.Contains("阿泽", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证配置文件坏掉时退回默认值而不是起不来()
    /// </summary>
    [Fact]
    public void 配置文件坏掉时退回默认值而不是起不来() {
        var store = new ConfigurationStore(_root);
        File.WriteAllText(store.FilePath, "{ 这不是 JSON");

        var result = store.Load(_ => null);

        Assert.Equal(ConfigurationSource.Fallback, result.Source);
        Assert.NotNull(result.Error);
        Assert.Equal("Asia/Shanghai", result.Configuration.Site.TimeZone);

        // 坏文件必须原样留着：直接覆盖掉，人就再也找不回自己填过什么了
        Assert.Equal("{ 这不是 JSON", File.ReadAllText(store.FilePath));
    }

    /// <summary>
    /// 验证认得旧版留在站点根目录的appsettings()
    /// </summary>
    [Fact]
    public void 认得旧版留在站点根目录的appsettings() {
        WriteLegacyAppSettings("""
            {
              "OurStory": {
                "TimeZone": "Asia/Tokyo",
                "Seed": { "BoyUserName": "kee" }
              },
              "Storage": { "Prefix": "old/prefix" }
            }
            """);
        var store = new ConfigurationStore(_root, configFilePath: null, contentRootPath: _root);

        var result = store.Load(_ => null);

        Assert.Equal(ConfigurationSource.Migrated, result.Source);
        Assert.Equal("Asia/Tokyo", result.Configuration.Site.TimeZone);
        Assert.Equal("kee", result.Configuration.Site.Seed.BoyUserName);
        Assert.Equal("old/prefix", result.Configuration.Storage.Prefix);

        // 搬完就落盘，下次启动直接读新文件
        Assert.Equal(ConfigurationSource.File, store.Load(_ => null).Source);
    }

    /// <summary>
    /// 验证认得旧版那套扁平的OSS环境变量()
    /// </summary>
    [Fact]
    public void 认得旧版那套扁平的OSS环境变量() {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["OSS_REGION"] = "cn-beijing",
            ["OSS_BUCKET"] = "our-bucket",
            ["OSS_ACCESS_KEY_ID"] = "id",
            ["OSS_ACCESS_KEY_SECRET"] = "secret",
            ["OSS_PUBLIC_BASE_URL"] = "https://img.example.com",
            ["OurStory__Seed__GirlPassword"] = "从前的口令"
        };
        var store = new ConfigurationStore(_root, configFilePath: null, contentRootPath: _root);

        var result = store.Load(name => environment.GetValueOrDefault(name));

        Assert.Equal(ConfigurationSource.Migrated, result.Source);
        Assert.Equal("our-bucket", result.Configuration.Storage.Oss.Bucket);
        Assert.Equal("从前的口令", result.Configuration.Site.Seed.GirlPassword);

        // 老规矩：五项配全了就走 OSS，配置文件里不必显式写 Driver
        Assert.Null(result.Configuration.Storage.Driver);
        Assert.Equal(StorageDriver.AliyunOss, result.Configuration.Storage.EffectiveDriver);
    }

    /// <summary>
    /// 验证环境变量盖过旧appsettings()
    /// </summary>
    [Fact]
    public void 环境变量盖过旧appsettings() {
        WriteLegacyAppSettings("""{ "OurStory": { "TimeZone": "Asia/Tokyo" } }""");
        var store = new ConfigurationStore(_root, configFilePath: null, contentRootPath: _root);

        var result = store.Load(name => name == "OurStory__TimeZone" ? "Europe/Paris" : null);

        Assert.Equal("Europe/Paris", result.Configuration.Site.TimeZone);
    }

    /// <summary>
    /// 验证老部署没配过任何东西时就当全新装()
    /// </summary>
    [Fact]
    public void 老部署没配过任何东西时就当全新装() {
        var store = new ConfigurationStore(_root, configFilePath: null, contentRootPath: _root);

        Assert.Equal(ConfigurationSource.Created, store.Load(_ => null).Source);
    }

    /// <summary>
    /// 验证数据目录优先认新的环境变量()
    /// </summary>
    [Fact]
    public void 数据目录优先认新的环境变量() {
        var absolute = Path.Combine(_root, "elsewhere");
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["OURSTORY_DATA_DIR"] = absolute,
            ["OurStory__DataDirectory"] = Path.Combine(_root, "legacy")
        };

        Assert.Equal(absolute, ConfigurationStore.ResolveDataDirectory(_root, name => environment.GetValueOrDefault(name)));
    }

    /// <summary>
    /// 验证数据目录认得旧环境变量和旧appsettings()
    /// </summary>
    [Fact]
    public void 数据目录认得旧环境变量和旧appsettings() {
        // 老用户把数据挪到过别处，升级后必须还找得回去，否则看起来就像数据全没了
        WriteLegacyAppSettings("""{ "OurStory": { "DataDirectory": "OldData" } }""");

        Assert.Equal(
            Path.Combine(_root, "OldData"),
            ConfigurationStore.ResolveDataDirectory(_root, _ => null));

        Assert.Equal(
            Path.Combine(_root, "legacy"),
            ConfigurationStore.ResolveDataDirectory(
                _root,
                name => name == "OurStory__DataDirectory" ? Path.Combine(_root, "legacy") : null));
    }

    /// <summary>
    /// 验证什么都没配时数据目录落在站点目录下(string)
    /// </summary>
    [Fact]
    public void 什么都没配时数据目录落在站点目录下() {
        Assert.Equal(Path.Combine(_root, "App_Data"), ConfigurationStore.ResolveDataDirectory(_root, _ => null));
    }

    /// <summary>
    /// 验证配置文件的位置能单独指定()
    /// </summary>
    [Fact]
    public void 配置文件的位置能单独指定() {
        // 容器里常见的做法：数据在卷里，配置从宿主机挂一份进来
        var mounted = Path.Combine(_root, "mounted.json");
        var store = ConfigurationStore.Create(
            _root,
            name => name == "OURSTORY_CONFIG_FILE" ? mounted : null);

        _ = store.Load(_ => null);

        Assert.Equal(mounted, store.FilePath);
        Assert.True(File.Exists(mounted));
    }

    private void WriteLegacyAppSettings(string json) =>
        File.WriteAllText(Path.Combine(_root, LegacyConfiguration.LegacyFileName), json);
}

/// <summary>
/// 表示 StorageOptionsTests
/// </summary>
public sealed class StorageOptionsTests {
    /// <summary>
    /// 验证OSS没配全就一律走本地()
    /// </summary>
    [Fact]
    public void OSS没配全就一律走本地() {
        var options = new StorageOptions { Driver = StorageDriver.AliyunOss };
        options.Oss.Bucket = "our-bucket";

        Assert.Equal(StorageDriver.Local, options.EffectiveDriver);
    }

    /// <summary>
    /// 验证不写Driver时配全了就自动走OSS()
    /// </summary>
    [Fact]
    public void 不写Driver时配全了就自动走OSS() {
        var options = Configured();

        Assert.Equal(StorageDriver.AliyunOss, options.EffectiveDriver);
    }

    /// <summary>
    /// 验证显式写了Local就守着本地不动()
    /// </summary>
    [Fact]
    public void 显式写了Local就守着本地不动() {
        // 参数先填好、暂时还不切过去，这种情况得留得住
        var options = Configured();
        options.Driver = StorageDriver.Local;

        Assert.Equal(StorageDriver.Local, options.EffectiveDriver);
    }

    private static StorageOptions Configured() {
        var options = new StorageOptions();
        options.Oss.Region = "cn-beijing";
        options.Oss.Bucket = "our-bucket";
        options.Oss.AccessKeyId = "id";
        options.Oss.AccessKeySecret = "secret";
        options.Oss.PublicBaseUrl = "https://img.example.com";
        return options;
    }
}
