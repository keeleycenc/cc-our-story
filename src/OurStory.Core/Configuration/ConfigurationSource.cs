// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Configuration;

/// <summary>
/// 配置这一次是怎么来的，启动日志里会说明
/// </summary>
public enum ConfigurationSource {
    /// <summary>
    /// 照着配置文件读出来的
    /// </summary>
    File,

    /// <summary>
    /// 文件不存在，用默认值起的站，并顺手生成了一份模板
    /// </summary>
    Created,

    /// <summary>
    /// 从老版本的 appsettings.json / 环境变量搬过来了一份
    /// </summary>
    Migrated,

    /// <summary>
    /// 文件在，但读不出来，这次先用默认值顶上
    /// </summary>
    Fallback
}
