// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Models;

namespace OurStory.Services.Notifications;

/// <summary>
/// 通知的待发队列
/// </summary>
/// <remarks>
/// 发一条通知要挨个连推送服务，慢的时候要好几秒。发布一条点点滴滴不该为此干等，
/// 所以业务里只把请求丢进这个队列就返回，真正的投递交给后台那一头慢慢做。
/// 推送服务抽风也就只是少收一条通知，不会连累到保存本身
/// </remarks>
public interface INotificationQueue {
    /// <summary>
    /// 把一条通知排进队列，立刻返回
    /// </summary>
    /// <param name="request">投递请求</param>
    /// <returns>排进去了返回 true；队列已经满了或者站点正在关闭返回 false</returns>
    bool Enqueue(NotificationRequest request);

    /// <summary>
    /// 异步按排队顺序取出待发的通知，没有就一直等着
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待发通知的异步序列</returns>
    IAsyncEnumerable<NotificationRequest> ReadAllAsync(CancellationToken cancellationToken = default);
}
