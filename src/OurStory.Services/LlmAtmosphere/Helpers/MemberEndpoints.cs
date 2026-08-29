// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Options;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 将氛围组角色配置转换为通用模型服务端点
/// </summary>
public static class MemberEndpoints {
    /// <summary>
    /// 获取该角色本次调用使用的模型服务端点
    /// </summary>
    /// <param name="member">氛围组角色配置</param>
    /// <param name="timeoutSeconds">氛围组公用的单次调用超时秒数</param>
    /// <returns>可直接交给 Responses 客户端的服务描述</returns>
    public static ResponsesEndpoint ToEndpoint(this LlmAtmosphereMember member, int timeoutSeconds) {
        ArgumentNullException.ThrowIfNull(member);

        return new ResponsesEndpoint(
            $"氛围组 {member.Name}",
            member.BaseUrl,
            member.Model,
            member.ApiKey,
            member.MaxOutputTokens,
            timeoutSeconds);
    }
}
