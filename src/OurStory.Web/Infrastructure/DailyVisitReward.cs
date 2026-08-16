// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Time;
using OurStory.Services.HeartPoints;
using System.Collections.Concurrent;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 记录每个用户当天是否已经领取过每日来访心意
/// </summary>
public sealed class DailyVisitLedger {
    private readonly ConcurrentDictionary<int, string> _given = new();

    /// <summary>
    /// 判断用户当天是否已经领取过这笔心意
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="day">站点时区对应的日期，格式如 2026-08-16</param>
    /// <returns>当天已经领取过则返回 true</returns>
    public bool AlreadyGiven(int userId, string day) =>
        _given.TryGetValue(userId, out var last) && string.Equals(last, day, StringComparison.Ordinal);

    /// <summary>
    /// 记录用户当天已经领取过心意；下一次记录会覆盖之前的日期
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="day">站点时区对应的日期，格式如 2026-08-16</param>
    public void Remember(int userId, string day) => _given[userId] = day;
}

/// <summary>
/// 用户每天第一次来访时，记一笔心意
/// </summary>
public static class DailyVisitReward {
    /// <summary>
    /// 启用每日首次来访奖励；需放在 UseAuthentication 之后，以便识别当前用户
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>当前应用构建器</returns>
    public static IApplicationBuilder UseDailyVisitReward(this IApplicationBuilder app) {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) => {
            await AwardAsync(context);
            await next(context);
        });
    }

    private static async Task AwardAsync(HttpContext context) {
        if (!HttpMethods.IsGet(context.Request.Method)
            || context.User.Role() is not (UserRole.Boy or UserRole.Girl)
            || context.User.UserId() is not { } userId) {
            return;
        }

        var ledger = context.RequestServices.GetRequiredService<DailyVisitLedger>();
        var day = context.RequestServices.GetRequiredService<SiteClock>().TodayKey;
        if (ledger.AlreadyGiven(userId, day)) {
            return;
        }

        var heartPoints = context.RequestServices.GetRequiredService<IHeartPointService>();
        _ = await heartPoints.AwardDailyAsync(userId, HeartPointReason.DailyVisit, day, context.RequestAborted);
        ledger.Remember(userId, day);
    }
}
