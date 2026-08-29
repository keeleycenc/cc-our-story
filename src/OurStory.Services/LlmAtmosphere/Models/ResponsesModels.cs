// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 表示一次 Responses 调用使用的模型服务端点
/// </summary>
/// <remarks>
/// 客户端仅依赖该端点描述，不感知氛围组、花信小结等具体业务来源。
/// 各业务模块负责将服务地址、模型、凭据和输出额度转换为该记录。
/// </remarks>
/// <param name="Label">用于日志追踪的调用方名称</param>
/// <param name="BaseUrl">兼容 OpenAI Responses 协议的服务地址</param>
/// <param name="Model">模型名称</param>
/// <param name="ApiKey">模型服务的 API Key</param>
/// <param name="MaxOutputTokens">单次调用允许生成的最大输出 Token 数</param>
/// <param name="TimeoutSeconds">单次调用的超时秒数</param>
public sealed record ResponsesEndpoint(
    string Label,
    string BaseUrl,
    string Model,
    string ApiKey,
    int MaxOutputTokens,
    int TimeoutSeconds) {
    /// <summary>
    /// 获取一个值，指示当前端点配置是否完整
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// 表示一次发送给 Responses 协议模型的请求
/// </summary>
/// <param name="Endpoint">本次调用使用的模型服务</param>
/// <param name="Instructions">角色人设与生成要求，对应 Responses 协议中的 instructions</param>
/// <param name="Text">本次请求需要模型理解的文本内容</param>
/// <param name="Images">随文本一起发送的图片；为空时表示纯文本请求</param>
public sealed record ResponsesRequest(
    ResponsesEndpoint Endpoint,
    string Instructions,
    string Text,
    IReadOnlyList<ResponsesImage> Images) {
    /// <summary>
    /// 创建不包含图片的纯文本请求
    /// </summary>
    /// <param name="endpoint">本次调用使用的模型服务</param>
    /// <param name="instructions">角色人设与生成要求</param>
    /// <param name="text">需要模型理解的文本内容</param>
    public ResponsesRequest(ResponsesEndpoint endpoint, string instructions, string text)
        : this(endpoint, instructions, text, []) {
    }

    /// <summary>
    /// 创建一份移除图片后的请求，用于视觉请求失败时降级为纯文本重试
    /// </summary>
    public ResponsesRequest WithoutImages() => this with { Images = [] };
}

/// <summary>
/// 表示一张随 Responses 请求发送的图片
/// </summary>
/// <param name="Url">
/// 图片资源地址。当前会将本地附件和 OSS 图片统一转换为 <c>data:</c> 内联资源，
/// 避免依赖模型服务直接访问站点资源。
/// </param>
public sealed record ResponsesImage(string Url);

/// <summary>
/// 表示一次 Responses 调用的结果
/// </summary>
/// <param name="Text">模型返回的文本内容；调用失败时为空</param>
/// <param name="Failure">调用失败原因；成功时为 <see cref="ResponsesFailure.None"/></param>
public sealed record ResponsesResult(string Text, ResponsesFailure Failure = ResponsesFailure.None) {
    /// <summary>
    /// 获取一个值，指示本次调用是否成功返回了有效文本
    /// </summary>
    public bool IsSuccess => Failure == ResponsesFailure.None && !string.IsNullOrWhiteSpace(Text);

    /// <summary>
    /// 创建一个成功的调用结果
    /// </summary>
    /// <param name="text">模型返回的文本内容</param>
    /// <returns>成功的调用结果</returns>
    public static ResponsesResult Success(string text) => new(text);

    /// <summary>
    /// 创建一个失败的调用结果
    /// </summary>
    /// <param name="failure">调用失败原因</param>
    /// <returns>失败的调用结果</returns>
    public static ResponsesResult Failed(ResponsesFailure failure) => new(string.Empty, failure);
}

/// <summary>
/// 定义 Responses 调用可能出现的失败类型
/// </summary>
/// <remarks>
/// 对失败原因进行分类，便于上层决定是否需要降级重试。
/// 当前仅 <see cref="Rejected"/> 可能由图片输入不受支持或请求参数不兼容导致，
/// 因此适合尝试移除图片后重新发送纯文本请求。
/// </remarks>
public enum ResponsesFailure {
    /// <summary>
    /// 调用成功
    /// </summary>
    None = 0,

    /// <summary>
    /// 请求被服务端拒绝，可能由不支持图片输入、参数不兼容或请求格式错误导致
    /// </summary>
    Rejected = 1,

    /// <summary>
    /// API Key 无效或当前凭据没有访问权限
    /// </summary>
    Unauthorized = 2,

    /// <summary>
    /// 请求受到服务端限流
    /// </summary>
    RateLimited = 3,

    /// <summary>
    /// 服务不可达、请求超时或远端服务发生异常
    /// </summary>
    Unreachable = 4,

    /// <summary>
    /// 请求成功完成，但响应中未包含可用的文本内容
    /// </summary>
    Empty = 5,

    /// <summary>
    /// 凭据本身有效，但服务端拒绝访问该接口
    /// </summary>
    Forbidden = 6,

    /// <summary>
    /// 输出额度已耗尽
    /// </summary>
    Truncated = 7
}

/// <summary>
/// 提供适用于后台页面的调用失败说明
/// </summary>
public static class ResponsesFailures {
    /// <summary>
    /// 取得失败原因对应的中文说明
    /// </summary>
    /// <param name="failure">调用失败原因</param>
    /// <returns>面向后台页面的说明文字</returns>
    public static string Describe(this ResponsesFailure failure) => failure switch {
        ResponsesFailure.None => "调用成功。",
        ResponsesFailure.Rejected => "模型服务拒绝了本次请求，请检查服务地址和模型名称。",
        ResponsesFailure.Unauthorized => "API Key 无效或没有访问权限。",
        ResponsesFailure.Forbidden => "凭据有效，但模型服务不允许访问该接口。",
        ResponsesFailure.RateLimited => "模型服务当前限流，请稍后重试。",
        ResponsesFailure.Unreachable => "无法连接模型服务，请检查网络连接或超时设置。",
        ResponsesFailure.Empty => "服务端返回了空内容。",
        ResponsesFailure.Truncated => "输出内容被截断，请提高「单次最多生成」后重试。",
        _ => "调用失败。"
    };
}
