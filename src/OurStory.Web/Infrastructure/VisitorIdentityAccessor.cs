// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services;
using OurStory.Services.Heartbeats;
using OurStory.Services.Settings;
using System.Security.Cryptography;
using System.Text;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 判断「这次请求是谁发来的」
///
/// 登录了就是男主或女主，身份完全由服务端的票据决定，前端改不了；
/// 没登录的一律是访客，用一串哈希区分不同的人 —— 存的不是原始 IP，
/// 但同一个访客在同一台设备上是稳定的。
/// </summary>
public class VisitorIdentityAccessor(IHttpContextAccessor httpContextAccessor, ISettingsService settings) {
    private VisitorIdentity? _cached;

    /// <summary>
    /// 获取（异步）
    /// </summary>
    public async Task<VisitorIdentity> GetAsync(CancellationToken cancellationToken = default) {
        if (_cached is { } cached) {
            return cached;
        }

        var context = httpContextAccessor.HttpContext;
        var principal = context?.User;

        if (principal.IsOwner() && principal.UserId() is { } userId) {
            var identity = new VisitorIdentity(principal.Role(), userId, string.Empty);
            _cached = identity;
            return identity;
        }

        var secret = await settings.GetRawAsync(DatabaseInitializer.VisitorSecretKey, cancellationToken) ?? string.Empty;
        var guest = VisitorIdentity.Guest(Fingerprint(context, secret));
        _cached = guest;
        return guest;
    }

    private static string Fingerprint(HttpContext? context, string secret) {
        var ip = context?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var agent = context?.Request.Headers.UserAgent.ToString() ?? string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ip}|{agent}|{secret}"));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }
}
