// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Options;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 表示一次发送给 Responses 协议模型的请求
/// </summary>
/// <param name="Member">当前使用的氛围组角色配置，包括服务地址、模型和 API Key</param>
/// <param name="Instructions">角色人设与生成要求，对应 Responses 协议中的 instructions</param>
/// <param name="Text">本次请求需要模型理解的文本内容</param>
/// <param name="Images">随文本一起发送的图片；为空时表示纯文本请求</param>
public sealed record ResponsesRequest(
    LlmAtmosphereMember Member,
    string Instructions,
    string Text,
    IReadOnlyList<ResponsesImage> Images) {
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
    /// 输出额度用光
    /// </summary>
    Truncated = 7
}
