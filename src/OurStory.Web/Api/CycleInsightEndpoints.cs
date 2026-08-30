// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services.Cycles;
using OurStory.Web.Infrastructure;

namespace OurStory.Web.Api;

/// <summary>
/// 提供花信小结的模型通道测试接口
/// </summary>
public static class CycleInsightEndpoints {
    /// <summary>
    /// 注册花信小结后台接口
    /// </summary>
    /// <param name="app">Web 应用程序实例</param>
    public static void MapCycleInsightEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/cycle-insight").RequireAuthorization();

        _ = group.MapPost("/test", async (
            HttpContext context,
            ICycleInsightService insight,
            ICycleService cycles,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) => {
                if (context.User.UserId() is not { } userId) {
                    return Results.Unauthorized();
                }

                try {
                    var narrative = await cycles.LatestNarrativeAsync(userId, cancellationToken);
                    var probe = await insight.ProbeAsync(narrative, cancellationToken);

                    return Results.Json(new {
                        ok = probe.Ok,
                        message = probe.Message,
                        text = probe.Text
                    });
                } catch (Exception exception) when (exception is not OperationCanceledException) {
                    loggers.CreateLogger(typeof(CycleInsightEndpoints)).LogError(
                        exception,
                        "后台测试花信小结模型通道时发生异常。");

                    return Results.Json(new {
                        ok = false,
                        message = "模型测试未能完成，请稍后重试。详细原因请查看站点日志。",
                        text = string.Empty
                    });
                }
            });
    }
}
