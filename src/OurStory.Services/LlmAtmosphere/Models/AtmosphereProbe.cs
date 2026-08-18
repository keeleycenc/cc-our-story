// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 表示后台手动触发氛围组互动后的执行结果
/// </summary>
/// <remarks>
/// 正常氛围组互动会受到触发概率与延迟时间影响，不适合用于快速验证模型配置。
/// 手动触发会跳过这些调度条件，直接执行一次模型调用，并返回调用结果、生成内容及保存状态，
/// 便于确认服务地址、模型和 API Key 等配置是否可用。
/// </remarks>
/// <param name="Ok">指示本次调用是否成功获得可用内容</param>
/// <param name="Text">模型生成的文本内容；调用失败时为空</param>
/// <param name="Message">用于页面展示的执行结果说明</param>
/// <param name="Saved">指示生成内容是否已经写入评论区</param>
public sealed record AtmosphereProbe(bool Ok, string Text, string Message, bool Saved = false) {
    /// <summary>
    /// 创建一个未执行模型调用的失败结果
    /// </summary>
    /// <remarks>
    /// 适用于角色配置不完整、目标记录不可用或不满足内容保护规则等情况。
    /// </remarks>
    /// <param name="message">无法继续执行的原因</param>
    /// <returns>表示操作被阻止的执行结果</returns>
    public static AtmosphereProbe Blocked(string message) => new(false, string.Empty, message);
}
