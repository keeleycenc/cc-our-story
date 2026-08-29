// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace OurStory.Core.Options;

/// <summary>
/// 表示配置文件中的 <c>CycleInsight</c> 节点，用于配置花信如期的模型小结服务
/// </summary>
public class CycleInsightOptions {
    /// <summary>
    /// 获取或设置一个值，指示是否启用模型小结
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 获取或设置兼容 OpenAI Responses 协议的服务地址
    /// </summary>
    /// <remarks>
    /// 例如 <c>https://api.openai.com/v1</c>。
    /// </remarks>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置模型名称
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置模型服务的 API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置附加在站点统一写作要求之后的语气偏好
    /// </summary>
    public string Tone { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置单次调用的超时秒数
    /// </summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// 获取或设置单次调用允许生成的最大输出 Token 数
    /// </summary>
    public int MaxOutputTokens { get; set; } = 8192;

    /// <summary>
    /// 获取或设置一条小结在多少小时内不再重新生成
    /// </summary>
    /// <remarks>
    /// 事实发生变化时，小结会立即失效；该值仅控制事实未变化时的重新生成间隔。
    /// </remarks>
    public int RefreshHours { get; set; } = 12;

    /// <summary>
    /// 获取一个值，指示几项必填是否齐全
    /// </summary>
    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// 获取一个值，指示当前配置是否允许调用模型服务
    /// </summary>
    [JsonIgnore]
    public bool IsUsable => Enabled && IsConfigured;
}
