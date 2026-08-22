// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Options;

namespace OurStory.Core.Configuration;

/// <summary>
/// 整份站点配置，对应数据目录下的 <c>ourstory.json</c>
/// </summary>
/// <remarks>
/// 以后要加配置，在这里挂个新属性或新的 Options 类就行，别的地方不用动
/// </remarks>
public class OurStoryConfiguration {
    /// <summary>
    /// 获取或设置站点运行参数
    /// </summary>
    public SiteOptions Site { get; set; } = new();

    /// <summary>
    /// 获取或设置附件存储参数
    /// </summary>
    public StorageOptions Storage { get; set; } = new();

    /// <summary>
    /// 获取或设置 Web Push 的 VAPID 身份，首次启动自动生成
    /// </summary>
    public PushOptions Push { get; set; } = new();

    /// <summary>
    /// 获取或设置 SMTP 邮件通知参数
    /// </summary>
    public EmailOptions Email { get; set; } = new();

    /// <summary>
    /// 获取或设置 LLM 氛围组：一群按人设留言的虚拟朋友
    /// </summary>
    public LlmAtmosphereOptions LlmAtmosphere { get; set; } = new();
}
