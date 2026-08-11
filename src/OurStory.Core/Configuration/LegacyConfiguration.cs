// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Helpers;
using OurStory.Core.Options;
using System.Globalization;
using System.Text.Json;

namespace OurStory.Core.Configuration;

/// <summary>
/// 把 1.0 版本的配置（发布包里的 appsettings.json、compose 里的一堆环境变量）搬进新的 ourstory.json
/// </summary>
/// <remarks>
/// 只在配置文件还不存在时跑一次。搬完之后旧的环境变量就可以从 compose / systemd 里删掉了
/// </remarks>
public static class LegacyConfiguration {
    /// <summary>
    /// 老版本的配置文件名，升级时会留在站点根目录里
    /// </summary>
    public const string LegacyFileName = "appsettings.json";

    /// <summary>
    /// 老版本用来指数据目录的环境变量
    /// </summary>
    public const string LegacyDataDirectoryVariable = "OurStory__DataDirectory";

    /// <summary>
    /// 老部署把数据目录配在哪儿了：先看环境变量，再看旧的 appsettings.json
    /// </summary>
    /// <remarks>
    /// 这一步不能省：老用户把数据目录改到别处、升级后要是当没看见，
    /// 站点会在默认位置建一个空库，看起来就像数据全丢了
    /// </remarks>
    public static string? FindDataDirectory(string? contentRootPath, Func<string, string?>? readVariable = null) {
        var read = readVariable ?? Environment.GetEnvironmentVariable;

        var fromEnvironment = read(LegacyDataDirectoryVariable);
        return !string.IsNullOrWhiteSpace(fromEnvironment)
            ? fromEnvironment
            : ReadLegacyFile(contentRootPath)?.OurStory?.DataDirectory is { } configured && !string.IsNullOrWhiteSpace(configured)
                ? configured
                : null;
    }

    /// <summary>
    /// 攒出一份等价的新配置；老部署什么都没配过就返回 <c>false</c>
    /// </summary>
    /// <param name="contentRootPath">站点根目录，旧的 appsettings.json 在这儿找</param>
    /// <param name="configuration">搬过来的配置</param>
    /// <param name="readVariable">读环境变量的方式，测试时替换掉</param>
    public static bool TryBuild(
        string? contentRootPath,
        out OurStoryConfiguration configuration,
        Func<string, string?>? readVariable = null) {
        var read = readVariable ?? Environment.GetEnvironmentVariable;

        var legacy = ReadLegacyFile(contentRootPath);
        var built = new OurStoryConfiguration {
            Site = legacy?.OurStory ?? new SiteOptions(),
            Storage = legacy?.Storage ?? new StorageOptions()
        };

        // 环境变量比旧文件更「新」：老 compose 就是拿它覆盖镜像里那份 appsettings.json 的
        var touched = legacy is not null;
        touched |= Apply(read, "OurStory__TimeZone", value => built.Site.TimeZone = value);
        touched |= Apply(read, "OurStory__DatabaseFileName", value => built.Site.DatabaseFileName = value);
        touched |= Apply(read, "OurStory__VisitorSecret", value => built.Site.VisitorSecret = value);
        touched |= Apply(read, "OurStory__Seed__BoyUserName", value => built.Site.Seed.BoyUserName = value);
        touched |= Apply(read, "OurStory__Seed__BoyPassword", value => built.Site.Seed.BoyPassword = value);
        touched |= Apply(read, "OurStory__Seed__GirlUserName", value => built.Site.Seed.GirlUserName = value);
        touched |= Apply(read, "OurStory__Seed__GirlPassword", value => built.Site.Seed.GirlPassword = value);

        touched |= ApplyLong(read, "Storage__MaxFileSize", value => built.Storage.MaxFileSize = value);

        // OSS 有两套写法：标准的 Storage__Oss__Bucket，和跟旧插件对齐的扁平 OSS_BUCKET。
        // 后者写在后面，同时配了就以它为准 —— 老 .env 里填的就是这一套
        touched |= Apply(read, "Storage__Prefix", value => built.Storage.Prefix = value);
        touched |= ApplyOss(read, "Storage__Oss__", built.Storage.Oss);

        touched |= Apply(read, "OSS_PREFIX", value => built.Storage.Prefix = value);
        touched |= ApplyOss(read, "OSS_", built.Storage.Oss, flat: true);

        // Driver 最后定：老版本的规则是「五项配全了就自动切 OSS」，
        // 新配置里 Driver 留空就是这个意思，所以只有显式配过 Local 才需要落到文件里
        var driver = read("Storage__Driver");
        if (!string.IsNullOrWhiteSpace(driver)) {
            touched = true;
            built.Storage.Driver = Enum.TryParse<StorageDriver>(driver, ignoreCase: true, out var parsed)
                ? parsed
                : null;
        }

        configuration = built;
        return touched;
    }

    #region 私有方法

    private static bool Apply(Func<string, string?> read, string variable, Action<string> assign) {
        var value = read(variable);
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        assign(value);
        return true;
    }

    private static bool ApplyLong(Func<string, string?> read, string variable, Action<long> assign) {
        var value = read(variable);
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) {
            return false;
        }

        assign(parsed);
        return true;
    }

    private static bool ApplyOss(Func<string, string?> read, string prefix, OssOptions oss, bool flat = false) {
        var touched = false;
        touched |= Apply(read, prefix + (flat ? "REGION" : "Region"), value => oss.Region = value);
        touched |= Apply(read, prefix + (flat ? "BUCKET" : "Bucket"), value => oss.Bucket = value);
        touched |= Apply(read, prefix + (flat ? "ACCESS_KEY_ID" : "AccessKeyId"), value => oss.AccessKeyId = value);
        touched |= Apply(read, prefix + (flat ? "ACCESS_KEY_SECRET" : "AccessKeySecret"), value => oss.AccessKeySecret = value);
        touched |= Apply(read, prefix + (flat ? "PUBLIC_BASE_URL" : "PublicBaseUrl"), value => oss.PublicBaseUrl = value);
        touched |= Apply(read, prefix + (flat ? "API_ENDPOINT" : "ApiEndpoint"), value => oss.ApiEndpoint = value);
        return touched;
    }

    private static LegacyFile? ReadLegacyFile(string? contentRootPath) {
        if (string.IsNullOrWhiteSpace(contentRootPath)) {
            return null;
        }

        var path = Path.Combine(contentRootPath, LegacyFileName);
        if (!File.Exists(path)) {
            return null;
        }

        try {
            var legacy = JsonFile.Read<LegacyFile>(path);
            return legacy is { OurStory: null, Storage: null } ? null : legacy;
        } catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) {
            // 读不动就当没有：这份文件已经是历史遗留，不值得让站点起不来
            return null;
        }
    }

    /// <summary>
    /// 老版本 appsettings.json 的形状，只认还在用的那几个节点
    /// </summary>
    private sealed class LegacyFile {
        public LegacySiteSection? OurStory { get; set; }

        public StorageOptions? Storage { get; set; }
    }

    /// <summary>
    /// 数据目录当年是配在这一节里的，新版本挪到了环境变量
    /// </summary>
    private sealed class LegacySiteSection : SiteOptions {
        public string? DataDirectory { get; set; }
    }

    #endregion
}
