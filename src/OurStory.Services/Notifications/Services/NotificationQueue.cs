// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Models;
using System.Threading.Channels;

namespace OurStory.Services.Notifications;

internal sealed class NotificationQueue : INotificationQueue {
    /// <summary>
    /// 队列长度。两个人的站点，真排到这个数说明推送那头已经堵死了，
    /// 再往里塞只是让内存跟着涨
    /// </summary>
    private const int Capacity = 256;

    private readonly Channel<NotificationRequest> _channel =
        Channel.CreateBounded<NotificationRequest>(new BoundedChannelOptions(Capacity) {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool Enqueue(NotificationRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        return _channel.Writer.TryWrite(request);
    }

    public IAsyncEnumerable<NotificationRequest> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
