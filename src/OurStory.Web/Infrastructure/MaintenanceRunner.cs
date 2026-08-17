// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Data;
using OurStory.Services;
using OurStory.Services.Accounts;
using OurStory.Services.Settings;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 表示 MaintenanceRunner
/// </summary>
public static class MaintenanceRunner {
    /// <summary>执行一条维护命令然后退出，不会开始监听端口。返回进程退出码。</summary>
    public static async Task<int> ExecuteAsync(this WebApplication app, MaintenanceCommand command) {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(command);

        if (command.Action == MaintenanceAction.Help) {
            PrintHelp();
            return 0;
        }

        using var scope = app.Services.CreateScope();

        // 先把表结构补齐，不然对着一个空文件重置口令会直接报表不存在
        _ = await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        // 本地跑一个、容器里还跑一个是常态，两边是两个完全独立的库。
        // 不把路径打出来的话，很容易在这个库上改口令、去另一个站点上登录。
        var db = scope.ServiceProvider.GetRequiredService<OurStoryDbContext>();
        Console.WriteLine();
        Console.WriteLine($"数据库：{db.Database.GetDbConnection().DataSource}");

        var users = scope.ServiceProvider.GetRequiredService<IUserService>();

        // 页面上的称呼存在设置表里，账号自己不带名字，列清单时现查一次
        var site = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetAsync();

        return command.Action switch {
            MaintenanceAction.ListAccounts => await ListAsync(users, site),
            MaintenanceAction.ResetPassword => await ResetAsync(users, site, command),
            _ => 0
        };
    }

    private static async Task<int> ListAsync(IUserService users, SiteSettings site) {
        var accounts = await users.ListAsync();

        Console.WriteLine();
        Console.WriteLine("站点里的账号：");
        foreach (var account in accounts) {
            Console.WriteLine(
                $"  {account.UserName,-12} {RoleName(account.Role),-4} {site.RoleName(account.Role),-12} " +
                $"最近登录 {account.LastLoginAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "从来没有"}");
        }

        Console.WriteLine();
        Console.WriteLine("重置口令： --reset-password <登录名>");
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> ResetAsync(IUserService users, SiteSettings site, MaintenanceCommand command) {
        var user = await users.FindByNameAsync(command.UserName);
        if (user is null) {
            Console.Error.WriteLine($"没有叫「{command.UserName}」的账号。");
            _ = await ListAsync(users, site);
            return 1;
        }

        if (command.NewPassword is { } given && given.Length < 8) {
            Console.Error.WriteLine("新口令至少 8 位。");
            return 1;
        }

        // 没指定就随机生成：省得有人图省事敲个 123456
        var password = command.NewPassword ?? PasswordHasher.GenerateReadablePassword();
        await users.ResetPasswordAsync(user.Id, password);

        Console.WriteLine();
        Console.WriteLine($"账号 {user.UserName}（{RoleName(user.Role)}）的口令已经重置为：");
        Console.WriteLine();
        Console.WriteLine($"    {password}");
        Console.WriteLine();
        Console.WriteLine("这串口令只在这里出现一次，登录后请到后台「账号」页改成你自己记得住的。");
        Console.WriteLine();
        return 0;
    }

    private static void PrintHelp() {
        Console.WriteLine("""

            CC Our Story

            直接启动站点：
              OurStory.Web
              --lan [端口]                         同时听局域网，同 Wi-Fi 下移动端可访问

            维护命令（执行完就退出，不会启动站点）：
              --list-accounts                     列出所有账号
              --reset-password <登录名>            重置口令，随机生成一串并打印出来
              --reset-password <登录名> --new-password <口令>
                                                  重置成指定的口令，至少 8 位
              --help                              这份说明

            """);
    }

    private static string RoleName(UserRole role) => role switch {
        UserRole.Boy => "男主",
        UserRole.Girl => "女主",
        _ => "访客"
    };
}
