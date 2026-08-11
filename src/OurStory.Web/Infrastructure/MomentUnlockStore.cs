// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.DataProtection;
using System.Globalization;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 记住这台浏览器已经解开了哪几条上锁的记录。
///
/// 存在一个签名过的 Cookie 里，改不了也伪造不了；
/// 换台设备要重新输密码，这正是「上锁」想要的效果。
/// </summary>
public class MomentUnlockStore(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider provider) {
    private const string CookieName = "ourstory.unlocked";
    private const int MaxRemembered = 50;

    private readonly IDataProtector _protector = provider.CreateProtector("OurStory.MomentUnlock");

    /// <summary>
    /// 解锁edIds
    /// </summary>
    public IReadOnlySet<int> UnlockedIds() {
        var raw = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(raw)) {
            return new HashSet<int>();
        }

        try {
            var ids = _protector.Unprotect(raw)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0);

            return new HashSet<int>(ids);
        } catch (System.Security.Cryptography.CryptographicException) {
            return new HashSet<int>();
        }
    }

    /// <summary>
    /// 记住
    /// </summary>
    public void Remember(int momentId) {
        var context = httpContextAccessor.HttpContext;
        if (context is null) {
            return;
        }

        var ids = new List<int>(UnlockedIds()) { momentId };
        var payload = string.Join(',', ids.Distinct().TakeLast(MaxRemembered));

        context.Response.Cookies.Append(CookieName, _protector.Protect(payload), new CookieOptions {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }
}
