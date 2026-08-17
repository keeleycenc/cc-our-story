// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 要执行哪一条维护命令
/// </summary>
public enum MaintenanceAction {
    /// <summary>
    /// 列出所有账号信息
    /// </summary>
    ListAccounts,

    /// <summary>
    /// 重置指定账号的密码
    /// </summary>
    ResetPassword,

    /// <summary>
    /// 显示帮助信息
    /// </summary>
    Help
}

/// <summary>
/// 命令行维护命令
///
/// 站点没有「找回密码」的入口，
/// 所以口令重置放在命令行上：能敲到这条命令的人，本来就已经能读到数据库文件了
/// </summary>
/// <param name="Action">做什么</param>
/// <param name="UserName">要重置哪个账号</param>
/// <param name="NewPassword">指定的新口令；留空则随机生成一串</param>
public record MaintenanceCommand(MaintenanceAction Action, string UserName = "", string? NewPassword = null) {
    private const string ResetFlag = "--reset-password";
    private const string PasswordFlag = "--new-password";
    private const string ListFlag = "--list-accounts";

    /// <summary>
    /// 这几个开关是给我们自己用的，不能混进 Host 的配置里</summary>
    private static readonly string[] OwnFlags = [ResetFlag, PasswordFlag, ListFlag, LanBinding.Flag, "--help"];

    /// <summary>
    /// 后面不跟值的开关。摘除时不能顺手把下一个参数也吃掉
    /// </summary>
    /// <remarks><c>--lan</c> 的端口是可选的，跟不跟数字都得认</remarks>
    private static readonly string[] Standalone = [ListFlag, "--help", "-h"];

    /// <summary>
    /// 从命令行里解析出一条维护命令；没有就返回 null，照常启动站点
    /// </summary>
    public static MaintenanceCommand? Parse(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

        if (Array.Exists(args, arg => arg is "--help" or "-h")) {
            return new MaintenanceCommand(MaintenanceAction.Help);
        }

        if (Array.Exists(args, arg => string.Equals(arg, ListFlag, StringComparison.OrdinalIgnoreCase))) {
            return new MaintenanceCommand(MaintenanceAction.ListAccounts);
        }

        var userName = ValueOf(args, ResetFlag);
        return userName is null
            ? null
            : new MaintenanceCommand(MaintenanceAction.ResetPassword, userName, ValueOf(args, PasswordFlag));
    }

    /// <summary>
    /// 把我们自己的开关摘掉再交给 Host
    ///
    /// 不摘的话，命令行配置提供程序会把 <c>--reset-password boy</c> 当成一个配置项，
    /// 而末尾单独一个 <c>--reset-password</c> 会让它直接抛「参数格式无法识别」
    /// </summary>
    public static string[] StripFrom(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

        var kept = new List<string>(args.Length);
        for (var index = 0; index < args.Length; index++) {
            if (!IsOwnFlag(args[index])) {
                kept.Add(args[index]);
                continue;
            }

            if (!Array.Exists(Standalone, flag => string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                && index + 1 < args.Length
                && !args[index + 1].StartsWith('-')) {
                index++;
            }
        }

        return [.. kept];
    }

    private static bool IsOwnFlag(string arg) =>
        arg is "-h" || Array.Exists(OwnFlags, flag => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 取 <c>--开关 值</c> 里的那个值；开关不在或后面没跟值时返回 null
    /// </summary>
    private static string? ValueOf(string[] args, string flag) {
        for (var index = 0; index < args.Length - 1; index++) {
            if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase)
                && !args[index + 1].StartsWith('-')) {
                return args[index + 1];
            }
        }

        return null;
    }
}
