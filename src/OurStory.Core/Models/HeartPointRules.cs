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
    public const int MaxReward = 20;

    /// <summary>
    /// 某一类纪念日当天，两个人各能拿到的心意
    /// </summary>
    /// <param name="kind">纪念日分类</param>
    /// <returns>该分类当天的基础奖励</returns>
    public static int AnniversaryReward(AnniversaryKind kind) => kind switch {
        AnniversaryKind.Love => 10,
        AnniversaryKind.Wedding => 10,
        AnniversaryKind.Birthday => 8,
        AnniversaryKind.FirstMeeting => 8,
        AnniversaryKind.Milestone => 8,
        AnniversaryKind.Travel => 5,
        AnniversaryKind.Festival => 5,
        AnniversaryKind.Promise => 5,
        AnniversaryKind.Family => 5,
        _ => 3
    };
}
