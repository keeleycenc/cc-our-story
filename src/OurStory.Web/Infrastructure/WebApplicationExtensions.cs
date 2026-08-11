// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 表示 WebApplicationExtensions
/// </summary>
public static class WebApplicationExtensions {
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
