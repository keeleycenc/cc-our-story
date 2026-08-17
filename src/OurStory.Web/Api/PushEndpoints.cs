// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Models;
using OurStory.Services.Notifications;
using OurStory.Services.Settings;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Api;

/// <summary>
/// 通知的接口：登记设备、注销设备、试发一条、给对方发一句话
/// </summary>
/// <remarks>
/// 这几条都只给登录用户用。授权本身必须由用户在页面上点一下才能发起，
/// 这是浏览器的硬性要求，服务端这边只负责把订阅存下来
/// </remarks>
public static class PushEndpoints {
    /// <summary>
    /// 接上通知相关的接口
    /// </summary>
    public static void MapPushEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/push").RequireAuthorization();

        // 前端订阅时要拿它当 applicationServerKey，没有密钥就直说，页面好给个提示
        _ = group.MapGet("/key", (INotificationService notifications) =>
            Results.Json(new { ok = notifications.IsConfigured, key = notifications.PublicKey }));

        _ = group.MapPost("/subscribe", async (
            HttpContext context,
            PushDeviceRegistration registration,
            INotificationService notifications,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                try {
                    var device = await notifications.RegisterDeviceAsync(
                        userId,
                        registration with { UserAgent = context.Request.Headers.UserAgent.ToString() },
                        $"{context.Request.Scheme}://{context.Request.Host}",
                        cancellationToken);

                    return Results.Json(new { ok = true, device = device.DeviceName });
                } catch (Exception exception) when (exception is ArgumentException or FormatException) {
                    return Results.Json(
                        new { ok = false, message = "这份订阅看起来不太对，刷新页面再试一次。" },
                        statusCode: StatusCodes.Status400BadRequest);
                }
            });

        _ = group.MapPost("/unsubscribe", async (
            HttpContext context,
            EndpointInput input,
            INotificationService notifications,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                var removed = await notifications.RemoveDeviceAsync(userId, input.Endpoint ?? string.Empty, cancellationToken);
                return Results.Json(new { ok = removed });
            });

        // 通知测试：只发给自己，而且不看那四个勾，用来确认这条链路本身通不通
        _ = group.MapPost("/test", async (
            HttpContext context,
            INotificationService notifications,
            ISettingsService settings,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                var site = await settings.GetAsync(cancellationToken);
                var result = await notifications.SendAsync(
                    NotificationRequest.ToUser(
                        NotificationTopic.Test,
                        userId,
                        new PushMessage(
                            $"{site.SiteTitle} 通知测试",
                            "能看到这一条，说明这台设备的通知已经通了。",
                            "/admin/notifications",
                            "push-test")),
                    cancellationToken);

                return Results.Json(new {
                    ok = result.Sent > 0,
                    result.Sent,
                    result.Failed,
                    result.Dropped,
                    message = Explain(result, notifications.IsConfigured)
                });
            });

        // 给对方发一句话：人主动按下的发送键，所以不看对方勾了哪几项，
        // 但对方把通知总开关关掉时依然收不到
        _ = group.MapPost("/send", async (
            HttpContext context,
            MessageInput input,
            INotificationService notifications,
            ISettingsService settings,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                var body = (input.Body ?? string.Empty).Trim();
                if (body.Length == 0) {
                    return Results.Json(
                        new { ok = false, message = "总得写点什么。" },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (await notifications.GetPartnerIdAsync(userId, cancellationToken) is not { } partnerId) {
                    return Results.Json(new { ok = false, message = "还没有另一个账号，发不出去。" });
                }

                var site = await settings.GetAsync(cancellationToken);
                var result = await notifications.SendAsync(
                    NotificationRequest.ToUser(
                        NotificationTopic.Direct,
                        partnerId,
                        new PushMessage(
                            $"{site.RoleName(context.User.Role())}说",
                            // 长度由通知服务统一剪，这里不必自己动手切
                            body,
                            "/",
                            // 时间戳让每条都是新的一条，不会把上一句盖掉
                            $"direct-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}")),
                    cancellationToken);

                return Results.Json(new {
                    ok = result.Sent > 0,
                    message = result.Sent > 0
                        ? $"已经送到对方的 {result.Sent} 台设备。"
                        : "对方还没有开启通知，或者没有可用的设备。"
                });
            });
    }

    private static string Explain(PushDeliveryResult result, bool configured) {
        if (!configured) {
            return "站点还没有生成通知密钥，看看启动日志里说了什么。";
        }

        if (result.Sent > 0) {
            return $"已经发往 {result.Sent} 台设备，稍等一下就能看到。";
        }

        return result.Total == 0
            ? "这个账号还没有授权过任何设备，先在下面点「在这台设备上开启」。"
            : "一台都没发出去，设备可能已经撤销了授权，重新开启一次试试。";
    }

    /// <summary>
    /// 注销设备时提交的推送地址
    /// </summary>
    /// <param name="Endpoint">要注销的推送地址</param>
    public sealed record EndpointInput(string? Endpoint);

    /// <summary>
    /// 发给对方的一句话
    /// </summary>
    /// <param name="Body">正文</param>
    public sealed record MessageInput(string? Body);
}
