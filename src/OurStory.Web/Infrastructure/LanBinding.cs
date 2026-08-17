// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Globalization;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// <c>--lan</c>：让局域网里的手机也能打开这个站点
/// </summary>
public static class LanBinding {
    /// <summary>
    /// 开关参数
    /// </summary>
    public const string Flag = "--lan";

    /// <summary>
    /// 没指定端口时用这个，和 launchSettings 里保持一致
    /// </summary>
    private const int DefaultPort = 5080;

    /// <summary>
    /// 命令行里有没有这个开关
    /// </summary>
    public static bool IsRequested(string[] args) {
        ArgumentNullException.ThrowIfNull(args);
        return Array.Exists(args, arg => string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 算出要监听的地址
    /// </summary>
    /// <param name="args">命令行参数，<c>--lan 8080</c> 可以直接指定端口</param>
    /// <param name="configuredUrls">当前配置里的地址，通常来自 launchSettings 或 <c>ASPNETCORE_URLS</c></param>
    /// <returns>形如 <c>http://0.0.0.0:5080</c> 的监听地址</returns>
    public static string Resolve(string[] args, string? configuredUrls) {
        ArgumentNullException.ThrowIfNull(args);

        var port = PortAfterFlag(args) ?? PortIn(configuredUrls) ?? DefaultPort;
        return $"http://0.0.0.0:{port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// 把内网里能用的地址列出来，启动时打印给人照着在手机上输
    /// </summary>
    public static IReadOnlyList<string> LocalAddresses(int port) =>
        [.. System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .Where(item => item.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(address => $"http://{address.Address}:{port.ToString(CultureInfo.InvariantCulture)}")];

    /// <summary>
    /// 取出监听地址里的端口，方便打印
    /// </summary>
    public static int PortOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Port : DefaultPort;

    private static int? PortAfterFlag(string[] args) {
        for (var index = 0; index < args.Length - 1; index++) {
            if (string.Equals(args[index], Flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[index + 1], CultureInfo.InvariantCulture, out var port)
                && port is > 0 and < 65536) {
                return port;
            }
        }

        return null;
    }

    /// <summary>
    /// 从 <c>http://localhost:5080;https://localhost:5081</c> 这种串里取第一个端口
    /// </summary>
    private static int? PortIn(string? urls) {
        if (string.IsNullOrWhiteSpace(urls)) {
            return null;
        }

        foreach (var candidate in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttp) {
                return parsed.Port;
            }
        }

        return null;
    }
}
