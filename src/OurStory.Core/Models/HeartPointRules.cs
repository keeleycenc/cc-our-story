// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Core.Models;

/// <summary>
/// 心意奖励使用的统一边界
/// </summary>
public static class HeartPointRules {
    /// <summary>
    /// 单次奖励允许关闭
    /// </summary>
    public const int MinReward = 0;

    /// <summary>
    /// 心有灵犀每次答题必须提供的最低奖励
    /// </summary>
    public const int MinAffinityReward = 1;

    /// <summary>
    /// 单次奖励上限
    /// </summary>
    public const int MaxReward = 100;
}
