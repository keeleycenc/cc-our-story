// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace OurStory.Services.Cycles;

/// <summary>
/// 在单站点进程内按情侣关系串行化写入
/// </summary>
internal sealed class CycleWriteCoordinator {
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

    public async Task<IAsyncDisposable> EnterAsync(int relationshipId, CancellationToken cancellationToken) {
        var gate = _gates.GetOrAdd(relationshipId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable {
        public ValueTask DisposeAsync() {
            _ = gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
