// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Models;
using System.Collections.Concurrent;

namespace OurStory.Services.Notifications;

internal sealed class MissYouNotifier : IMissYouNotifier, IDisposable {
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(30);

    private readonly INotificationQueue _queue;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<int, Pending> _pending = new();
    private bool _disposed;

    public MissYouNotifier(INotificationQueue queue)
        : this(queue, DefaultWindow) {
    }

    /// <summary>
    /// 构造函数，初始化 <see cref="MissYouNotifier"/> 新实例"/>
    /// </summary>
    internal MissYouNotifier(INotificationQueue queue, TimeSpan window) {
        _queue = queue;
        _window = window;
    }

    public void Record(int userId, string displayName, int taps) {
        if (taps <= 0 || _disposed) {
            return;
        }

        _pending.GetOrAdd(userId, id => new Pending(id, this)).Add(displayName, taps);
    }

    /// <summary>
    /// 释放所有还在等着的计时器
    /// </summary>
    public void Dispose() {
        _disposed = true;

        foreach (var key in _pending.Keys) {
            if (_pending.TryRemove(key, out var pending)) {
                pending.Dispose();
            }
        }
    }

    private void Flush(int userId, string displayName, int taps) {
        var body = taps == 1
            ? "刚刚在首页想了你一下"
            : $"刚刚一口气想了你 {taps} 下";

        _ = _queue.Enqueue(NotificationRequest.ToPartner(
            NotificationTopic.MissYou,
            userId,
            new PushMessage($"{displayName}想你了", body, "/", $"miss-you-{userId}")));
    }

    private sealed class Pending : IDisposable {
        private readonly int _userId;
        private readonly MissYouNotifier _owner;
        private readonly Lock _gate = new();
        private readonly Timer _timer;
        private string _displayName = string.Empty;
        private int _taps;

        public Pending(int userId, MissYouNotifier owner) {
            _userId = userId;
            _owner = owner;
            _timer = new Timer(_ => Fire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Add(string displayName, int taps) {
            lock (_gate) {
                _taps += taps;
                _displayName = displayName;
            }

            _ = _timer.Change(_owner._window, Timeout.InfiniteTimeSpan);
        }

        public void Dispose() => _timer.Dispose();

        private void Fire() {
            string name;
            int taps;

            lock (_gate) {
                taps = _taps;
                name = _displayName;
                _taps = 0;
            }

            if (taps > 0) {
                _owner.Flush(_userId, name, taps);
            }
        }
    }
}
