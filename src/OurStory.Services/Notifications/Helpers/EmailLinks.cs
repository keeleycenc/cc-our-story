// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;

namespace OurStory.Services.Notifications;

/// <summary>
/// 把站内链接转换为邮件里可直接访问的绝对链接
/// </summary>
internal static class EmailLinks {
    /// <summary>
    /// 解析通知链接；找不到可信的站点根地址时返回 null
    /// </summary>
    public static string? Resolve(string? url, string? siteOrigin, ActiveConfiguration configuration) {
        if (string.IsNullOrWhiteSpace(url)) {
            return null;
        }

        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var absolute)
            && IsHttp(absolute)) {
            return absolute.AbsoluteUri;
        }

        var root = FirstHttpOrigin(
            siteOrigin,
            configuration.Email.SiteBaseUrl,
            configuration.Current.Push.Subject);

        if (root is null || !Uri.TryCreate(root, url.Trim(), out var combined)) {
            return null;
        }

        return combined.AbsoluteUri;
    }

    private static Uri? FirstHttpOrigin(params string?[] candidates) {
        foreach (var candidate in candidates) {
            if (Uri.TryCreate(candidate?.Trim(), UriKind.Absolute, out var uri)
                && IsHttp(uri)) {
                return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
            }
        }

        return null;
    }

    private static bool IsHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
