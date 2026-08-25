// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Models;

namespace OurStory.Services.Anniversaries;

/// <summary>
/// 纪念日当天的心意奖励
/// </summary>
public interface IAnniversaryRewardService {
    /// <summary>
    /// 异步检查某一天有没有纪念日，有就按分类给两个人各发一份
    /// </summary>
    /// <remarks>
    /// 当天有几个纪念日就发几份，不设上限；同一天同一个纪念日只会发一次，
    /// 所以重跑、补跑都安全
    /// </remarks>
    /// <param name="day">要检查的日期，按站点时区理解</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，这一趟命中的纪念日条数与实际发出的心意</returns>
    Task<AnniversaryRewardResult> AwardForDayAsync(DateOnly day, CancellationToken cancellationToken = default);
}
