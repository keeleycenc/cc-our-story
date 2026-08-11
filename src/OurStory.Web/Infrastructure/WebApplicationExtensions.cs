// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Services;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 表示 WebApplicationExtensions
/// </summary>
public static class WebApplicationExtensions {
    /// <summary>
    /// 在启动日志里交代这次的配置是从哪儿来的
    /// </summary>
    /// <remarks>
    /// 配置文件读坏了只会退回默认值、不会让站点起不来，所以更得说一声：
    /// 否则「OSS 怎么突然不生效了」这种事只能靠猜
    /// </remarks>
    public static void LogConfigurationSource(this WebApplication app, ConfigurationLoadResult result) {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(result);

        switch (result.Source) {
            case ConfigurationSource.Created:
                app.Logger.LogInformation("已生成配置文件 {Path}，按需要改完重启即可生效。", result.Path);
                break;
            case ConfigurationSource.Migrated:
                app.Logger.LogWarning(
                    "已把旧版的 appsettings.json / 环境变量搬进 {Path}，之后改配置认这一份就行，旧的可以删掉了。",
                    result.Path);
                break;
            case ConfigurationSource.Fallback:
                app.Logger.LogError(
                    "配置文件 {Path} 读不出来（{Error}），这次先用默认值启动 —— 改回合法的 JSON 再重启。",
                    result.Path,
                    result.Error);
                break;
            case ConfigurationSource.File:
            default:
                app.Logger.LogInformation("站点配置来自 {Path}。", result.Path);
                break;
        }
    }

    /// <summary>
    /// 启动时建表 / 升级表结构，并保证两个账号都在
    ///
    /// 放在这里而不是让人手动跑迁移：把发布包丢到目录里直接启动就能用，
    /// 这是整套方案最想要的那个体验。
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        var seeded = await initializer.InitializeAsync();

        foreach (var account in seeded) {
            app.Logger.LogWarning(
                "已创建 {Role} 账号：登录名 {UserName}，初始口令 {Password} —— 这串口令只在这里出现一次，登录后请到后台改掉。",
                account.Role,
                account.UserName,
                account.Password);
        }
    }
}
