// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;

namespace OurStory.Services.Notifications;

/// <summary>
/// 一次投递的下场
/// </summary>
internal enum PushSendOutcome {
    /// <summary>
    /// 推送服务已经收下，会替我们转交给设备
    /// </summary>
    Delivered = 0,

    /// <summary>
    /// 这份订阅已经作废：应用被卸载、浏览器数据被清空或者用户撤了授权。
    /// 对应的设备记录可以直接删掉
    /// </summary>
    Gone = 1,

    /// <summary>
    /// 这次没成，但订阅本身也许还活着，下次还能再试
    /// </summary>
    Failed = 2,

    /// <summary>
    /// VAPID 还没配好，压根没发出去
    /// </summary>
    NotConfigured = 3
}

/// <summary>
/// 往一台设备上发一条加密通知
/// </summary>
/// <remarks>
/// 只管「这一条、这一台」，要发给谁、该不该发是 <see cref="INotificationService"/> 的事
/// </remarks>
internal interface IWebPushSender {
    /// <summary>
    /// 获取一个值，指示 VAPID 密钥是否已经就绪
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// 获取要交给浏览器的 VAPID 公钥；没配好时是空串
    /// </summary>
    string PublicKey { get; }

    /// <summary>
    /// 把一条通知加密后送到推送服务
    /// </summary>
    /// <param name="device">目标设备</param>
    /// <param name="payload">通知内容，Service Worker 会原样收到这段 JSON</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，这次投递的下场</returns>
    Task<PushSendOutcome> SendAsync(PushDevice device, string payload, CancellationToken cancellationToken = default);
}
