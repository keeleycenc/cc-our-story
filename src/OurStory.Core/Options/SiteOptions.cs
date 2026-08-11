// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Options;

/// <summary>
/// 配置文件里的 "Site" 节点：部署时定下来、跑起来就不动的站点参数
/// </summary>
/// <remarks>
/// 和后台「站点设置」里的 <see cref="Models.SiteSettings"/> 分工不同：
/// 那边是随时能改、存在数据库里的展示内容（名字、头像、情话）；
/// 这里是启动时才读一次的运行参数，改完要重启
/// </remarks>
public class SiteOptions {
    /// <summary>
    /// 获取或设置 SQLite 文件名，位于数据目录下
    /// </summary>
    /// <remarks>
    /// 数据目录本身不在这里配 —— 配置文件就放在数据目录里，
    /// 先有目录才读得到文件。它由 <c>OURSTORY_DATA_DIR</c> 环境变量决定
    /// </remarks>
    public string DatabaseFileName { get; set; } = "ourstory.db";

    /// <summary>
    /// 获取或设置站点时区。「今天」「相恋第 N 天」「每日 99 次上限」都按它算，不跟着服务器所在时区跑
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Shanghai";

    /// <summary>
    /// 获取或设置生成访客指纹时掺进去的密钥。留空会在首次启动时随机生成并写进数据库，这样同一份部署重启后访客的统计不会断
    /// </summary>
    public string VisitorSecret { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置首次启动时自动创建的两个账号
    /// </summary>
    public SeedAccountOptions Seed { get; set; } = new();
}
