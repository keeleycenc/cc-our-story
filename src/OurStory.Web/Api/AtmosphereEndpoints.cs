// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Services.LlmAtmosphere;

namespace OurStory.Web.Api;

/// <summary>
/// 提供氛围组相关的后台交互接口，包括角色试聊与即时留言。
/// </summary>
/// <remarks>
/// 模型调用通常需要一定处理时间，因此通过独立 API 异步完成请求，
/// 避免整页提交影响后台其他功能的正常使用。
/// </remarks>
public static class AtmosphereEndpoints {
    /// <summary>
    /// 注册氛围组相关接口。
    /// </summary>
    /// <param name="app">Web 应用程序实例。</param>
    public static void MapAtmosphereEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.MapPost("/api/atmosphere/test", async (
            AtmosphereTestInput input,
            ILlmAtmosphereService atmosphere,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) => {
                if (string.IsNullOrWhiteSpace(input.MemberId)) {
                    return Results.Json(new {
                        ok = false,
                        message = "请选择想要试聊的角色。"
                    });
                }

                try {
                    var probe = await atmosphere.ProbeAsync(
                        input.MemberId,
                        input.TopicId,
                        input.Persist,
                        cancellationToken);

                    return Results.Json(new {
                        ok = probe.Ok,
                        message = probe.Message,
                        text = probe.Text,
                        saved = probe.Saved
                    });
                } catch (Exception exception) when (exception is not OperationCanceledException) {
                    // 调试入口也应确保异常被及时返回，避免前端长期处于等待状态
                    loggers.CreateLogger(typeof(AtmosphereEndpoints)).LogError(
                        exception,
                        "后台试聊氛围组角色 {MemberId} 时发生异常。",
                        input.MemberId);

                    return Results.Json(new {
                        ok = false,
                        message = "这次没有顺利完成，可以稍后再试。详细原因请查看站点日志。"
                    });
                }
            })
            .RequireAuthorization();
    }
}

/// <summary>
/// 表示后台发起角色试聊时提交的请求参数。
/// </summary>
/// <param name="MemberId">需要试聊的角色标识。</param>
/// <param name="TopicId">作为话题上下文的记录标识；0 表示使用最近发布的记录。</param>
/// <param name="Persist">是否将生成内容保存到评论区；否则仅用于预览。</param>
public sealed record AtmosphereTestInput(
    string MemberId,
    int TopicId,
    bool Persist);
