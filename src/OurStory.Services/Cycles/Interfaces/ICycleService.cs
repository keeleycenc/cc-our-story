// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core.Models;

namespace OurStory.Services.Cycles;

/// <summary>
/// 花信如期的关系内周期查询与写入服务接口
/// </summary>
public interface ICycleService {
    /// <summary>
    /// 异步获取当前用户所属情侣关系下的花信如期完整页面数据
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验情侣关系及数据访问权限</param>
    /// <param name="page">历史记录页码，从 1 开始</param>
    /// <param name="pageSize">历史记录每页数量</param>
    /// <param name="year">月历展示年份</param>
    /// <param name="month">月历展示月份</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>包含当前状态、统计预测、月历及历史记录的页面聚合数据</returns>
    Task<CycleDashboard> GetDashboardAsync(
        int userId,
        int page,
        int pageSize,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取一个可交互月份的日历数据
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验情侣关系及数据访问权限</param>
    /// <param name="year">月历展示年份</param>
    /// <param name="month">月历展示月份</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>当前展示月份的周期日历</returns>
    Task<CycleCalendarMonth> GetCalendarAsync(
        int userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取用于首页展示的花信周期状态摘要
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验情侣关系及数据访问权限</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>适合首页卡片展示的周期状态摘要文本</returns>
    Task<string> GetHomeStatusAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步以当前日期登记一次新的经期开始记录
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验写入权限</param>
    /// <param name="requestKey">用于防止重复提交的请求唯一键</param>
    /// <param name="confirmSuspicious">是否确认继续提交被规则判定为可疑的开始日期</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次开始登记的结构化写入结果</returns>
    Task<CycleWriteResult> StartAsync(
        int userId,
        string requestKey,
        bool confirmSuspicious,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步以当前日期结束当前进行中的经期记录
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验写入权限</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次结束登记的结构化写入结果</returns>
    Task<CycleWriteResult> EndAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步创建一条历史周期记录，结束日期可留空表示仍在进行
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验写入权限</param>
    /// <param name="submission">周期记录的提交内容</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次历史周期记录创建的结构化写入结果</returns>
    Task<CycleWriteResult> CreateAsync(
        int userId,
        CycleRecordSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步更新指定历史周期记录的日期与备注
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验写入权限</param>
    /// <param name="recordId">待更新的周期记录标识</param>
    /// <param name="startDate">更新后的经期开始日期</param>
    /// <param name="endDate">更新后的经期结束日期；留空表示这条记录仍在进行</param>
    /// <param name="note">更新后的周期补充备注</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次周期记录更新的结构化写入结果</returns>
    Task<CycleWriteResult> UpdateAsync(
        int userId,
        int recordId,
        DateOnly startDate,
        DateOnly? endDate,
        string note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除一条周期记录。当天的补充记录不受影响
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验写入权限</param>
    /// <param name="recordId">待删除的周期记录标识</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次删除的结构化写入结果</returns>
    Task<CycleWriteResult> DeleteAsync(
        int userId,
        int recordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步写入或更新某一天的补充记录，并可同时调整经期边界
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验写入权限</param>
    /// <param name="submission">这一天的提交内容</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次写入的结构化写入结果</returns>
    Task<CycleWriteResult> SaveDayAsync(
        int userId,
        CycleDaySubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步整理最新一次周期的事实上下文，供后台试写模型小结
    /// </summary>
    /// <param name="userId">当前操作用户标识，用于校验情侣关系及数据访问权限</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>最新一次周期的上下文；无权限或尚无记录时为 <see langword="null"/></returns>
    /// <remarks>
    /// 与页面、后台补写共用同一份投影逻辑，因此试写看到的事实与正式生成时完全一致。
    /// </remarks>
    Task<CycleNarrativeContext?> LatestNarrativeAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步补写过期或缺失的周期小结，由后台任务调用
    /// </summary>
    /// <param name="limit">本次最多补写多少条</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>本次实际补写的条数</returns>
    Task<int> RefreshSummariesAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
