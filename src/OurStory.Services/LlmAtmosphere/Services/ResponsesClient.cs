// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using OurStory.Core.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// Responses 协议客户端，负责构造请求并解析模型返回的文本内容
/// </summary>
/// <remarks>
/// 当前仅使用 <c>/responses</c> 接口，请求结构相对固定，因此直接基于 HTTP 实现，
/// 可避免对特定 SDK 的 Base URL、实验性接口支持及额外依赖产生耦合。
/// 后续如需流式输出或更多协议能力，可在此基础上按需扩展
/// </remarks>
internal sealed class ResponsesClient(
    IHttpClientFactory clients,
    ActiveConfiguration configuration,
    ILogger<ResponsesClient> logger) : IResponsesClient {
    /// <summary>
    /// 命名 HttpClient 的名字
    /// </summary>
    public const string HttpClientName = "ourstory.llm";

    /// <summary>
    /// 出错时日志里最多带多少个字符的响应体，够定位问题就行
    /// </summary>
    private const int ErrorBodyLimit = 400;

    /// <summary>
    /// 中文按 UTF-8 原样写出去，不转成 \uXXXX，请求体能小一大截，抓包也读得了
    /// </summary>
    private static readonly JsonSerializerOptions Json = new() {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<ResponsesResult> CompleteAsync(ResponsesRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (!Uri.TryCreate(Endpoint(request.Member.BaseUrl), UriKind.Absolute, out var endpoint)) {
            logger.LogWarning("氛围组「{Member}」的服务地址不是合法的 URL，跳过。", request.Member.Name);
            return ResponsesResult.Failed(ResponsesFailure.Rejected);
        }

        var seconds = Math.Clamp(configuration.LlmAtmosphere.TimeoutSeconds, 5, 300);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = new StringContent(Body(request).ToJsonString(Json), Encoding.UTF8, "application/json")
        };

        // Key 只出现在这一行，任何日志、异常和页面上都不会带上它
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Member.ApiKey);

        try {
            using var response = await clients.CreateClient(HttpClientName).SendAsync(message, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode) {
                var failure = Classify(response.StatusCode);
                logger.LogWarning(
                    "氛围组「{Member}」调用模型 {Model} 失败（{Status}）：{Body}",
                    request.Member.Name,
                    request.Member.Model,
                    (int)response.StatusCode,
                    Clip(body));

                return ResponsesResult.Failed(failure);
            }

            if (IsTruncated(body)) {
                return ResponsesResult.Failed(ResponsesFailure.Truncated);
            }

            var text = ReadText(body);
            return text.Length > 0 ? ResponsesResult.Success(text) : ResponsesResult.Failed(ResponsesFailure.Empty);
        } catch (JsonException exception) {
            logger.LogWarning(exception, "氛围组「{Member}」拿回来的响应不是合法 JSON。", request.Member.Name);
            return ResponsesResult.Failed(ResponsesFailure.Empty);
        } catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) {
            logger.LogWarning(
                exception,
                "氛围组「{Member}」连不上模型服务 {Host}。",
                request.Member.Name,
                endpoint.Host);

            return ResponsesResult.Failed(ResponsesFailure.Unreachable);
        }
    }

    #region 私有方法

    internal static string Endpoint(string baseUrl) {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');

        return trimmed.EndsWith("/responses", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/responses";
    }

    internal static JsonObject Body(ResponsesRequest request) {
        var content = new JsonArray(new JsonObject {
            ["type"] = "input_text",
            ["text"] = request.Text
        });

        foreach (var image in request.Images) {
            content.Add(new JsonObject {
                ["type"] = "input_image",
                ["image_url"] = image.Url
            });
        }

        return new JsonObject {
            ["model"] = request.Member.Model,
            ["instructions"] = request.Instructions,
            ["input"] = new JsonArray(new JsonObject {
                ["role"] = "user",
                ["content"] = content
            }),
            ["max_output_tokens"] = Math.Clamp(request.Member.MaxOutputTokens, 32, 4096),
            ["stream"] = false,

            // 这是两个人的私事，没必要留在对端的历史里
            ["store"] = false
        };
    }

    internal static string ReadText(string body) {
        if (JsonNode.Parse(body) is not JsonObject root) {
            return string.Empty;
        }

        if (root["output_text"] is JsonValue direct && direct.TryGetValue<string>(out var shortcut)) {
            return shortcut.Trim();
        }

        if (root["output"] is not JsonArray output) {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var item in output) {
            if (item is not JsonObject entry || Type(entry) is not (null or "message")) {
                continue;
            }

            if (entry["content"] is not JsonArray parts) {
                continue;
            }

            foreach (var part in parts) {
                if (part is JsonObject piece
                    && Type(piece) is null or "output_text"
                    && piece["text"]?.GetValue<string>() is { Length: > 0 } text) {
                    _ = builder.Append(text);
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static string? Type(JsonObject entry) =>
        entry["type"] is JsonValue value && value.TryGetValue<string>(out var name) ? name : null;

    private static ResponsesFailure Classify(HttpStatusCode status) => status switch {
        HttpStatusCode.Unauthorized => ResponsesFailure.Unauthorized,
        HttpStatusCode.Forbidden => ResponsesFailure.Forbidden,
        HttpStatusCode.TooManyRequests => ResponsesFailure.RateLimited,
        >= HttpStatusCode.InternalServerError => ResponsesFailure.Unreachable,
        _ => ResponsesFailure.Rejected
    };

    internal static bool IsTruncated(string body) {
        if (JsonNode.Parse(body) is not JsonObject root) {
            return false;
        }

        if (root["status"] is JsonValue status
            && status.TryGetValue<string>(out var name)
            && string.Equals(name, "incomplete", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return root["incomplete_details"] is JsonObject details
            && details["reason"] is JsonValue reason
            && reason.TryGetValue<string>(out var why)
            && why.Contains("max_output_tokens", StringComparison.OrdinalIgnoreCase);
    }

    private static string Clip(string body) {
        var text = body.Replace('\n', ' ').Trim();
        return text.Length <= ErrorBodyLimit ? text : text[..ErrorBodyLimit] + "…";
    }

    #endregion
}
