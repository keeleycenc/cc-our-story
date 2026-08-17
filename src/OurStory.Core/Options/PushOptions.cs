// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Options;

/// <summary>
/// 配置文件里的 "Push" 节点：Web Push 用的 VAPID 身份
/// </summary>
/// <remarks>
/// 这一对密钥就是浏览器眼里的「这个站点」。换掉它，之前所有设备上的订阅
/// 都会立刻失效、必须重新授权，所以首次启动自动生成之后就别再动了。
/// 留空时站点会自己生成一对写回配置文件；配置文件不可写的话推送用不了，
/// 启动日志里会说明。
/// </remarks>
public class PushOptions {
    /// <summary>
    /// 获取或设置 VAPID 公钥，base64url 编码的 65 字节未压缩 P-256 公钥点
    /// </summary>
    /// <remarks>前端 <c>subscribe()</c> 时要原样传给 <c>applicationServerKey</c>。</remarks>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 VAPID 私钥，base64url 编码的 32 字节 P-256 私钥
    /// </summary>
    /// <remarks>只在服务端签 JWT 用，任何时候都不要发到页面上。</remarks>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 VAPID 的 <c>sub</c>，推送服务出问题时它们照着这个联系站长
    /// </summary>
    /// <remarks>必须是 <c>mailto:</c> 或 <c>https://</c> 开头，留空按本机地址兜底。</remarks>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 获取一个值，指示这对密钥是否可用
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
