// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Services.Cycles;

namespace OurStory.Web.Api;

/// <summary>
/// 提供花信小结模型测试与手动补写接口
/// </summary>
/// <remarks>
/// 模型调用通过独立接口执行，避免长时间请求触发后台页面导航。
/// </remarks>
public static class CycleInsightEndpoints {
    /// <summary>
    /// 手动补写小结时的单次处理上限
    /// </summary>
    private const int RefreshBatch = 20;

    /// <summary>
    /// 注册花信小结后台接口
    /// </summary>
    /// <param name="app">Web 应用程序实例</param>
    public static void MapCycleInsightEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/cycle-insight").RequireAuthorization();

        _ = group.MapPost("/test", async (
            ICycleInsightService insight,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) => {
                try {
                    var probe = await insight.ProbeAsync(cancellationToken);
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

        _ = group.MapPost("/refresh", async (
            ActiveConfiguration configuration,
            ICycleService cycles,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) => {
                if (!configuration.CycleInsight.IsUsable) {
                    return Results.Json(new {
                        ok = false,
                        message = "模型通道尚未启用或配置不完整，请先保存完整配置。"
                    });
                }

                try {
                    var written = await cycles.RefreshSummariesAsync(RefreshBatch, cancellationToken);
                    return Results.Json(new {
                        ok = true,
                        written,
                        message = written > 0
                            ? $"已补写 {written} 条花信小结。"
                            : "当前没有需要补写的小结，或模型本次未返回有效内容。"
                    });
                } catch (Exception exception) when (exception is not OperationCanceledException) {
                    loggers.CreateLogger(typeof(CycleInsightEndpoints)).LogError(
                        exception,
                        "后台手动补写花信小结时发生异常。");

                    return Results.Json(new {
                        ok = false,
                        message = "小结补写未能完成，请稍后重试。详细原因请查看站点日志。"
                    });
                }
            });
    }
}
