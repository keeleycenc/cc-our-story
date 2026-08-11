// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Options;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 表示 ConfigurationExtensions
/// </summary>
public static class ConfigurationExtensions {
    /// <summary>
    /// 认一份扁平的 OSS_* 环境变量。
    ///
    /// 旧版插件就是用这几个变量配 OSS 的，很多部署的 .env / docker-compose 里已经写好了，
    /// 这里直接翻译成 Storage:Oss:* ，原样搬过来就能继续用。
    /// 标准的 Storage__Oss__Bucket 写法优先级更高，两种都支持。
    /// </summary>
    public static void AddOssEnvironmentVariables(this IConfigurationManager configuration) {
        ArgumentNullException.ThrowIfNull(configuration);

        var map = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["OSS_REGION"] = "Storage:Oss:Region",
            ["OSS_BUCKET"] = "Storage:Oss:Bucket",
            ["OSS_ACCESS_KEY_ID"] = "Storage:Oss:AccessKeyId",
            ["OSS_ACCESS_KEY_SECRET"] = "Storage:Oss:AccessKeySecret",
            ["OSS_PUBLIC_BASE_URL"] = "Storage:Oss:PublicBaseUrl",
            ["OSS_API_ENDPOINT"] = "Storage:Oss:ApiEndpoint",
            ["OSS_PREFIX"] = "Storage:Prefix"
        };

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (variable, key) in map) {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(configuration[key])) {
                values[key] = value;
            }
        }

        if (values.Count > 0) {
            _ = configuration.AddInMemoryCollection(values);
        }

        // 和旧插件一致：五项参数都齐了就默认走 OSS，显式配过 Driver 的以配置为准
        var alreadyChosen = !string.IsNullOrWhiteSpace(configuration[$"{StorageOptions.SectionName}:Driver"])
            && !string.Equals(configuration[$"{StorageOptions.SectionName}:Driver"], "Local", StringComparison.OrdinalIgnoreCase);

        var oss = configuration.GetSection($"{StorageOptions.SectionName}:Oss").Get<OssOptions>() ?? new OssOptions();
        if (!alreadyChosen && oss.IsConfigured && Environment.GetEnvironmentVariable("Storage__Driver") is null) {
            _ = configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) {
                [$"{StorageOptions.SectionName}:Driver"] = nameof(StorageDriver.AliyunOss)
            });
        }
    }
}
