// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Options;

/// <summary>
/// 空库时创建男主 / 女主账号用的初始信息
/// </summary>
/// <remarks>
/// 密码留空时随机生成一串并打到启动日志里，绝不会出现「默认密码」这种东西。
/// 想自己定口令就在第一次启动前把配置文件写好 —— 库一旦建起来，这几项就不再有人看了
/// </remarks>
public class SeedAccountOptions {
    /// <summary>
    /// 获取或设置 BoyUserName
    /// </summary>
    public string BoyUserName { get; set; } = "boy";

    /// <summary>
    /// 获取或设置 BoyPassword
    /// </summary>
    public string BoyPassword { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 GirlUserName
    /// </summary>
    public string GirlUserName { get; set; } = "girl";

    /// <summary>
    /// 获取或设置 GirlPassword
    /// </summary>
    public string GirlPassword { get; set; } = string.Empty;
}
