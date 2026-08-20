// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;
using OurStory.Core.Models;

namespace OurStory.Services.Affinity;

/// <summary>
/// 获取心有灵犀服务接口
/// </summary>
public interface IAffinityService {
    /// <summary>
    /// 异步获取心有灵犀仪表盘数据
    /// </summary>
    /// <param name="userId">获取用户标识</param>
    /// <param name="role">获取用户角色</param>
    /// <param name="page">获取历史记录页码</param>
    /// <param name="pageSize">获取历史记录每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取心有灵犀仪表盘数据</returns>
    Task<AffinityDashboard> GetDashboardAsync(int userId, UserRole role, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取今日题目状态
    /// </summary>
    /// <param name="userId">获取用户标识</param>
    /// <param name="role">获取用户角色</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取今日题目状态</returns>
    Task<string> GetTodayStatusAsync(int userId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步提交今日题目答案
    /// </summary>
    /// <param name="dailyQuestionId">获取每日题目标识</param>
    /// <param name="optionIndex">获取选择的选项索引</param>
    /// <param name="userId">获取用户标识</param>
    /// <param name="role">获取用户角色</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取答案提交结果</returns>
    Task<AffinitySubmitResult> SubmitAsync(int dailyQuestionId, int optionIndex, int userId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取心有灵犀题目列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取心有灵犀题目卡片列表</returns>
    Task<IReadOnlyList<AffinityQuestionCard>> GetQuestionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取指定心有灵犀题目
    /// </summary>
    /// <param name="id">获取题目标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取心有灵犀题目卡片</returns>
    Task<AffinityQuestionCard?> GetQuestionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取保存后的心有灵犀题目卡片
    /// </summary>
    /// <param name="id">获取题目标识</param>
    /// <param name="model">获取题目编辑模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取保存后的心有灵犀题目卡片</returns>
    Task<AffinityQuestionCard> SaveQuestionAsync(int? id, AffinityQuestionEditModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步设置心有灵犀题目启用状态
    /// </summary>
    /// <param name="id">获取题目标识</param>
    /// <param name="active">获取启用状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取是否设置成功</returns>
    Task<bool> SetQuestionActiveAsync(int id, bool active, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除心有灵犀题目
    /// </summary>
    /// <param name="id">获取题目标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，获取是否删除成功</returns>
    Task<bool> DeleteQuestionAsync(int id, CancellationToken cancellationToken = default);
}
