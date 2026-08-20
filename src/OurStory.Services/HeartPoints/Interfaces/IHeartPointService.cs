// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core;
using OurStory.Core.Models;

namespace OurStory.Services.HeartPoints;

/// <summary>
/// 心意值的记账服务接口
/// </summary>
public interface IHeartPointService {
    /// <summary>
    /// 异步获取某个人的心意余额
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，全部流水之和</returns>
    Task<int> GetBalanceAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取男主和女主两个人的心意概况
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，按男主、女主排好的余额列表</returns>
    Task<IReadOnlyList<HeartPointBalance>> GetBalancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取某个人的心意账单
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="page">页码，从 1 开始</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，按时间倒序的一页流水</returns>
    Task<PagedList<HeartPointRecord>> GetRecordsAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步给某人记一笔当天的内容奖励
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="reason">奖励来头</param>
    /// <param name="day">奖励算在站点时区的哪一天，形如 2026-08-15</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，真发出去了返回发了多少，当天已经发过或者奖励配成 0 时返回 0</returns>
    Task<int> AwardDailyAsync(int userId, HeartPointReason reason, string day, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步发放一次性奖励
    /// </summary>
    /// <param name="userId">用户编号</param>
    /// <param name="reason">奖励原因</param>
    /// <param name="sourceKey">奖励来源唯一标识</param>
    /// <param name="amount">奖励数量</param>
    /// <param name="note">奖励备注</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际发放数量，重复发放时返回 0</returns>
    Task<int> AwardOnceAsync(
        int userId,
        HeartPointReason reason,
        string sourceKey,
        int amount,
        string note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步把商城上线之前就发生过的事补记成心意
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，补记的条数与合计；已经算过时不会再算第二遍</returns>
    Task<HeartPointBackfillResult> BackfillAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步判断「初始心意」是不是已经算过了
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，算过返回 true</returns>
    Task<bool> IsBackfilledAsync(CancellationToken cancellationToken = default);
}
