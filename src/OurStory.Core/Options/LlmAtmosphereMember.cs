// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace OurStory.Core.Options;

/// <summary>
/// 表示一个氛围组角色及其对应的 Responses 兼容模型配置
/// </summary>
public class LlmAtmosphereMember {
    /// <summary>
    /// 获取或设置角色唯一标识符
    /// </summary>
    /// <remarks>
    /// 角色创建后保持不变，历史评论通过该标识关联对应角色。
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置角色显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    /// 获取或设置角色头像地址
    /// </summary>
    /// <remarks>
    /// 留空时由页面根据角色名称生成文字头像及对应配色。
    /// </remarks>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置角色人设
    /// </summary>
    /// <remarks>
    /// 角色人设会与站点统一的互动规则共同组成模型 instructions。
    /// </remarks>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置一个值，指示是否启用当前角色
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 获取或设置一个值，指示是否允许将点点滴滴中的图片发送给模型
    /// </summary>
    /// <remarks>
    /// 开启后会优先尝试多模态请求。当模型不支持图片输入、图片读取失败
    /// 或视觉请求被拒绝时，可自动降级为纯文本请求。
    /// </remarks>
    public bool AllowImages { get; set; }

    /// <summary>
    /// 获取或设置点点滴滴发布后主动留言的触发概率，取值范围为 0 到 100
    /// </summary>
    public int CommentChance { get; set; } = 100;

    /// <summary>
    /// 获取或设置角色收到回复后继续互动的触发概率，取值范围为 0 到 100
    /// </summary>
    public int ReplyChance { get; set; } = 90;

    /// <summary>
    /// 获取或设置互动前的最短等待时间，单位为分钟
    /// </summary>
    public int DelayMinMinutes { get; set; } = 3;

    /// <summary>
    /// 获取或设置互动前的最长等待时间，单位为分钟
    /// </summary>
    public int DelayMaxMinutes { get; set; } = 90;

    /// <summary>
    /// 获取或设置单次模型调用允许生成的最大输出 Token 数
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// 获取一个值，指示该配的几项是否齐全
    /// </summary>
    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// 获取一个值，指示当前角色配置是否完整且可以参与互动
    /// </summary>
    [JsonIgnore]
    public bool IsUsable => Enabled && IsConfigured;

    /// <summary>
    /// 随机生成介于 <see cref="DelayMinMinutes"/> 与 <see cref="DelayMaxMinutes"/> 之间的等待时间
    /// </summary>
    /// <param name="random">随机数生成器</param>
    /// <returns>本次互动需要等待的时间</returns>
    public TimeSpan NextDelay(Random random) {
        ArgumentNullException.ThrowIfNull(random);

        var low = Math.Clamp(DelayMinMinutes, 0, 60 * 24);
        var high = Math.Clamp(DelayMaxMinutes, low, 60 * 24);

        return TimeSpan.FromMinutes(random.Next(low, high + 1));
    }

    /// <summary>
    /// 创建一个配置相同的新角色副本
    /// </summary>
    /// <param name="id">新角色的唯一标识</param>
    /// <param name="name">新角色的名称</param>
    /// <returns>创建完成的角色副本</returns>
    public LlmAtmosphereMember CopyAs(string id, string name) => new() {
        Id = id,
        Name = name,
        BaseUrl = BaseUrl,
        Model = Model,
        ApiKey = ApiKey,
        AvatarUrl = AvatarUrl,
        Prompt = Prompt,
        Enabled = false,
        AllowImages = AllowImages,
        CommentChance = CommentChance,
        ReplyChance = ReplyChance,
        DelayMinMinutes = DelayMinMinutes,
        DelayMaxMinutes = DelayMaxMinutes,
        MaxOutputTokens = MaxOutputTokens
    };
}
