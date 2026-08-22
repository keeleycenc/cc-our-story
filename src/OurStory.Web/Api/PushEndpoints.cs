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
public static class PushEndpoints {
    /// <summary>
    /// 接上通知相关的接口
    /// </summary>
    public static void MapPushEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/push").RequireAuthorization();

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
                        new { ok = false, message = "订阅凭证无效，请刷新页面重试" },
                        statusCode: StatusCodes.Status400BadRequest);
                }
            });

        _ = group.MapPost("/state", async (
            HttpContext context,
            EndpointInput input,
            INotificationService notifications,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                var ownership = await notifications.GetOwnershipAsync(userId, input.Endpoint ?? string.Empty, cancellationToken);

                return Results.Json(new {
                    ok = true,
                    state = ownership.ToString().ToLowerInvariant()
                });
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

        _ = group.MapPost("/test", async (
            HttpContext context,
            TestInput input,
            INotificationService notifications,
            ISettingsService settings,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                var site = await settings.GetAsync(cancellationToken);
                var result = await notifications.SendAsync(
                    new NotificationRequest(
                        NotificationTopic.Test,
                        new PushMessage(
                            $"{site.SiteTitle} 通知测试",
                            "本条消息用于确认通知通道已正常工作",
                            "/admin/notifications",
                            "push-test"),
                        TargetUserId: userId,
                        TargetDeviceId: input.DeviceId,
                        Channel: NotificationChannelKind.WebPush),
                    cancellationToken);

                return Results.Json(new {
                    ok = result.Sent > 0,
                    result.Sent,
                    result.Failed,
                    result.Dropped,
                    message = Explain(result, notifications.IsConfigured)
                });
            });

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
                        new { ok = false, message = "内容不能为空，请填写后再提交" },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (await notifications.GetPartnerIdAsync(userId, cancellationToken) is not { } partnerId) {
                    return Results.Json(new { ok = false, message = "缺少接收方账号，无法发送" });
                }

                var readiness = await notifications.GetPartnerReadinessAsync(userId, cancellationToken);
                if (!readiness.CanReceive) {
                    return Results.Json(new {
                        ok = false,
                        message = readiness.Enabled
                            ? "对方暂无可用的通知渠道"
                            : "对方已关闭通知，当前发送将无法送达"
                    });
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

                var delivered = result.WebPush.Sent + result.Email.Sent;
                var details = new List<string>();
                if (result.WebPush.Sent > 0) {
                    details.Add($"Web Push {result.WebPush.Sent} 台设备");
                }

                if (result.Email.Sent > 0) {
                    details.Add($"Email {result.Email.Sent} 封");
                }

                return Results.Json(new {
                    ok = delivered > 0,
                    message = delivered > 0
                        ? $"已送达：{string.Join("、", details)}"
                        : "对方未开启通知或没有可用渠道"
                });
            });
    }

    private static string Explain(NotificationDeliveryResult result, bool configured) {
        if (!configured) {
            return "站点还没配好通知密钥，去看看启动日志吧";
        }

        if (result.Sent > 0) {
            return $"已经送到 {result.Sent} 台设备上啦";
        }

        if (result.Total == 0) {
            return "你的账号还没绑定过设备，点击「开启通知」就好啦";
        }

        return result.Reason switch {
            PushFailureReason.Unreachable =>
                "推送网关不可达，通知发送失败。详情见站点日志",
            PushFailureReason.Unauthorized =>
                "推送网关拒绝本次请求，身份验证失败。请检查配置文件 Push 中的 Subject 和密钥对。详情见站点日志",
            PushFailureReason.Expired =>
                "设备订阅已失效，已清除记录。重新点击「开启通知」即可恢复",
            _ => "推送网关未接收通知，详情见站点日志"
        };
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

    /// <summary>
    /// 测试通知要发给哪一台设备
    /// </summary>
    /// <param name="DeviceId">目标设备编号；留空表示自己名下的所有设备</param>
    public sealed record TestInput(long? DeviceId);
}
