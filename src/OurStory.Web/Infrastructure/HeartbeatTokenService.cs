// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.DataProtection;
using OurStory.Services.Heartbeats;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 首页爱心那颗按钮带的令牌。
///
/// 页面渲染时签一个和当前身份绑定的串，提交时验回来：
/// 别的站点拿不到这个串，也就没法替你去点。
/// 令牌有效期 12 小时，页面开太久再点会提示刷新，和原来的行为一致。
/// </summary>
public class HeartbeatTokenService(IDataProtectionProvider provider) {
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("OurStory.Heartbeat").ToTimeLimitedDataProtector();

    /// <summary>
    /// 判断sue
    /// </summary>
    public string Issue(VisitorIdentity who) => _protector.Protect(Key(who), Lifetime);

    /// <summary>
    /// 验证
    /// </summary>
    public bool Validate(string? token, VisitorIdentity who) {
        if (string.IsNullOrWhiteSpace(token)) {
            return false;
        }

        try {
            return string.Equals(_protector.Unprotect(token), Key(who), StringComparison.Ordinal);
        } catch (System.Security.Cryptography.CryptographicException) {
            // 过期、被改过、或者换了密钥环，一律当作无效
            return false;
        }
    }

    private static string Key(VisitorIdentity who) =>
        $"{who.Role}:{who.UserId?.ToString() ?? who.VisitorHash}";
}
