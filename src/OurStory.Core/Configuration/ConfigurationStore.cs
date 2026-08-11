// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Helpers;
using System.Text.Json;

namespace OurStory.Core.Configuration;

/// <summary>
/// 站点配置的落盘位置与读写
/// </summary>
/// <remarks>
/// 配置文件放在数据目录里（默认 <c>App_Data/ourstory.json</c>，容器里是 <c>/data/ourstory.json</c>），
/// 和数据库、附件、密钥待在一起：发布包整个覆盖、镜像重新构建，都不会碰到它
/// </remarks>
/// <param name="dataDirectory">数据目录的绝对路径</param>
/// <param name="configFilePath">配置文件路径，留空则取数据目录下的 <see cref="FileName"/></param>
/// <param name="contentRootPath">站点根目录，只用来找老版本留下的 appsettings.json</param>
public sealed class ConfigurationStore(string dataDirectory, string? configFilePath = null, string? contentRootPath = null) {
    private readonly string? _contentRootPath = contentRootPath;  // 站点根目录

    /// <summary>
    /// 配置文件名
    /// </summary>
    public const string FileName = "ourstory.json";

    /// <summary>
    /// 数据目录，没配就是站点目录下的 App_Data
    /// </summary>
    public const string DataDirectoryVariable = "OURSTORY_DATA_DIR";

    /// <summary>
    /// 直接指定配置文件的完整路径，方便容器里从宿主机挂一份进来
    /// </summary>
    public const string ConfigFileVariable = "OURSTORY_CONFIG_FILE";

    /// <summary>
    /// 数据目录的默认名字，相对站点根目录
    /// </summary>
    public const string DefaultDataDirectoryName = "App_Data";

    /// <summary>
    /// 获取数据目录：数据库、附件、密钥和这份配置都在里面
    /// </summary>
    public string DataDirectory { get; } = dataDirectory;

    /// <summary>
    /// 获取配置文件的完整路径
    /// </summary>
    public string FilePath { get; } = string.IsNullOrWhiteSpace(configFilePath)
            ? Path.Combine(dataDirectory, FileName)
            : Path.GetFullPath(configFilePath);

    /// <summary>
    /// 按环境变量和站点根目录定出数据目录与配置文件的位置
    /// </summary>
    public static ConfigurationStore Create(string contentRootPath, Func<string, string?>? readVariable = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        var read = readVariable ?? Environment.GetEnvironmentVariable;

        return new ConfigurationStore(
            ResolveDataDirectory(contentRootPath, read),
            read(ConfigFileVariable),
            contentRootPath);
    }

    /// <summary>
    /// 数据目录支持写相对路径（相对站点根目录），也支持写绝对路径
    /// </summary>
    public static string ResolveDataDirectory(string contentRootPath, Func<string, string?>? readVariable = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        var read = readVariable ?? Environment.GetEnvironmentVariable;

        var configured = read(DataDirectoryVariable);
        if (string.IsNullOrWhiteSpace(configured)) {
            configured = LegacyConfiguration.FindDataDirectory(contentRootPath, read) ?? DefaultDataDirectoryName;
        }

        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(contentRootPath, configured));
    }

    /// <summary>
    /// 读配置。文件不在就用默认值并生成一份模板，读坏了也只是退回默认值 —— 配置从来不该让站点起不来
    /// </summary>
    public ConfigurationLoadResult Load(Func<string, string?>? readVariable = null) {
        if (!File.Exists(FilePath) && TryMigrate(readVariable, out var migrated)) {
            return new ConfigurationLoadResult(migrated, FilePath, ConfigurationSource.Migrated);
        }

        if (!File.Exists(FilePath)) {
            var defaults = new OurStoryConfiguration();
            _ = TrySave(defaults, out _);
            return new ConfigurationLoadResult(defaults, FilePath, ConfigurationSource.Created);
        }

        try {
            var configuration = JsonFile.Read<OurStoryConfiguration>(FilePath) ?? new OurStoryConfiguration();
            return new ConfigurationLoadResult(configuration, FilePath, ConfigurationSource.File);
        } catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) {
            return new ConfigurationLoadResult(
                new OurStoryConfiguration(),
                FilePath,
                ConfigurationSource.Fallback,
                exception.Message);
        }
    }

    /// <summary>
    /// 把配置写回文件
    /// </summary>
    /// <returns>写成功返回 <c>true</c>；只读挂载之类写不动的情况返回 <c>false</c>，不抛异常</returns>
    public bool TrySave(OurStoryConfiguration configuration, out string? error) {
        ArgumentNullException.ThrowIfNull(configuration);

        try {
            JsonFile.Write(FilePath, configuration);
            error = null;
            return true;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException) {
            error = exception.Message;
            return false;
        }
    }

    private bool TryMigrate(Func<string, string?>? readVariable, out OurStoryConfiguration configuration) {
        if (!LegacyConfiguration.TryBuild(_contentRootPath, out configuration, readVariable)) {
            return false;
        }

        // 搬过来的东西写不进去也不要紧：这次照样按老配置跑，下次启动再搬一遍
        _ = TrySave(configuration, out _);
        return true;
    }
}
