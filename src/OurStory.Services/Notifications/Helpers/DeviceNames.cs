// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Notifications;

/// <summary>
/// 从 User-Agent 里猜一个人看得懂的设备名
/// </summary>
/// <remarks>
/// 后台的设备列表要让人认出「哪台是我的旧手机」
/// </remarks>
internal static class DeviceNames {
    /// <summary>
    /// 猜一个设备名，猜不出来时返回「某台设备」
    /// </summary>
    public static string Guess(string? userAgent) {
        var agent = (userAgent ?? string.Empty).Trim();
        if (agent.Length == 0) {
            return "某台设备";
        }

        var platform = Platform(agent);
        var browser = Browser(agent);

        return platform is null
            ? browser ?? "某台设备"
            : browser is null ? platform : $"{platform} · {browser}";
    }

    private static string? Platform(string agent) {
        // iPadOS 的 Safari 会把自己报成 Macintosh，所以先看有没有触摸端的特征
        if (Has(agent, "iPhone")) {
            return "iPhone";
        }

        if (Has(agent, "iPad")) {
            return "iPad";
        }

        if (Has(agent, "Android")) {
            return "Android";
        }

        if (Has(agent, "Windows")) {
            return "Windows";
        }

        if (Has(agent, "Mac OS X") || Has(agent, "Macintosh")) {
            return "Mac";
        }

        return Has(agent, "Linux") ? "Linux" : null;
    }

    private static string? Browser(string agent) {
        // 顺序要紧：Edge 和 Chrome 的 UA 里都有 Chrome，Chrome 里又有 Safari
        if (Has(agent, "Edg/") || Has(agent, "EdgiOS") || Has(agent, "EdgA")) {
            return "Edge";
        }

        if (Has(agent, "OPR/") || Has(agent, "Opera")) {
            return "Opera";
        }

        if (Has(agent, "Firefox") || Has(agent, "FxiOS")) {
            return "Firefox";
        }

        if (Has(agent, "CriOS") || Has(agent, "Chrome")) {
            return "Chrome";
        }

        return Has(agent, "Safari") ? "Safari" : null;
    }

    private static bool Has(string agent, string token) =>
        agent.Contains(token, StringComparison.OrdinalIgnoreCase);
}
