// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 定义基于 OpenAI Responses 协议的模型调用客户端
/// </summary>
/// <remarks>
/// 上层业务仅依赖此接口，无需感知实际使用的是 OpenAI、兼容 Responses 协议的第三方服务或自建网关。
/// 实现应将连接失败、请求超时、限流及远端服务异常等情况统一转换为
/// <see cref="ResponsesResult"/> 中对应的失败类型，避免异常直接影响上层业务流程。
/// </remarks>
public interface IResponsesClient {
    /// <summary>
    /// 异步执行一次 Responses 请求
    /// </summary>
    /// <param name="request">本次模型调用所需的请求内容与角色配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含模型返回文本或失败原因的调用结果</returns>
    Task<ResponsesResult> CompleteAsync(ResponsesRequest request, CancellationToken cancellationToken = default);
}
