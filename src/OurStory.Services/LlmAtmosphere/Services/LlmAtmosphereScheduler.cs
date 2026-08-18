// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Configuration;
using OurStory.Core.Options;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 管理内存中的氛围组延迟任务，并按计划时间依次调度执行
/// </summary>
/// <remarks>
/// 延迟任务仅保存在内存中，不进行持久化。
/// 站点重启导致的未执行任务由后台巡检重新检查近期记录并按需补偿。
/// 这样可以避免调度层直接依赖数据库，同时保持业务侧调用轻量且无需异步等待。
/// </remarks>
internal sealed class LlmAtmosphereScheduler : ILlmAtmosphereScheduler {
    private const int Capacity = 512;   // 队列长度上限

    private readonly ActiveConfiguration _configuration;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly List<LlmAtmosphereTrigger> _pending = [];

    public LlmAtmosphereScheduler(ActiveConfiguration configuration)
        : this(configuration, TimeProvider.System) {
    }

    internal LlmAtmosphereScheduler(ActiveConfiguration configuration, TimeProvider? time = null) {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;
        _time = time ?? TimeProvider.System;
    }

    public int Pending {
        get {
            lock (_gate) {
                return _pending.Count;
            }
        }
    }

    public void OnMomentPublished(int momentId, bool isProtected) {
        var options = _configuration.LlmAtmosphere;
        if (!Allowed(options, isProtected)) {
            return;
        }

        var now = _time.GetUtcNow();

        foreach (var member in options.ActiveMembers) {
            if (!Rolled(member.CommentChance)) {
                continue;
            }

            _ = Schedule(new LlmAtmosphereTrigger(
                LlmAtmosphereTriggerKind.Comment,
                momentId,
                member.Id,
                now + member.NextDelay(Random.Shared)));
        }
    }

    public void OnCommentAdded(int momentId, int commentId, string? repliedMemberId, bool isProtected) {
        var options = _configuration.LlmAtmosphere;
        if (!Allowed(options, isProtected)) {
            return;
        }

        if (options.Find(repliedMemberId) is not { IsUsable: true } member || !Rolled(member.ReplyChance)) {
            return;
        }

        _ = Schedule(new LlmAtmosphereTrigger(
            LlmAtmosphereTriggerKind.Reply,
            momentId,
            member.Id,
            _time.GetUtcNow() + member.NextDelay(Random.Shared),
            commentId));
    }

    public bool Schedule(LlmAtmosphereTrigger trigger) {
        ArgumentNullException.ThrowIfNull(trigger);

        lock (_gate) {
            if (_pending.Count >= Capacity || _pending.Any(item => item.Key == trigger.Key)) {
                return false;
            }

            _pending.Add(trigger);
            return true;
        }
    }

    public IReadOnlyList<LlmAtmosphereTrigger> TakeDue() {
        var now = _time.GetUtcNow();

        lock (_gate) {
            var due = _pending.Where(item => item.DueAt <= now).OrderBy(item => item.DueAt).ToList();
            foreach (var item in due) {
                _ = _pending.Remove(item);
            }

            return due;
        }
    }

    #region 私有方法

    private static bool Allowed(LlmAtmosphereOptions options, bool isProtected) =>
        options.ActiveMembers.Count > 0 && (!isProtected || options.IncludeProtected);

    private static bool Rolled(int chance) =>
        chance > 0 && Random.Shared.Next(100) < Math.Min(chance, 100);

    #endregion
}
