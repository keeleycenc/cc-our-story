// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services.Notifications;

namespace OurStory.Web.Infrastructure;

/// <summary>
/// 守着待发队列，把排进来的通知一条条送出去
/// </summary>
internal sealed class NotificationWorker(
    INotificationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<NotificationWorker> logger) : BackgroundService {
    /// <summary>
    /// 执行后台循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await foreach (var request in queue.ReadAllAsync(stoppingToken)) {
            try {
                // 每条通知开一个新作用域：DbContext 是按请求存活的，后台没有请求
                await using var scope = scopes.CreateAsyncScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var result = await notifications.SendAsync(request, stoppingToken);
                if (result.Total > 0) {
                    logger.LogInformation(
                        "通知「{Title}」：Web Push 送达 {PushSent} 台、失败 {PushFailed} 台、清理 {Dropped} 台；Email 送达 {EmailSent} 封、失败 {EmailFailed} 封。",
                        request.Message.Title,
                        result.WebPush.Sent,
                        result.WebPush.Failed,
                        result.WebPush.Dropped,
                        result.Email.Sent,
                        result.Email.Failed);
                }
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogError(exception, "发送通知「{Title}」时出错。", request.Message.Title);
            }
        }
    }
}
