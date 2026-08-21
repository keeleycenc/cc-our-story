// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core;
using OurStory.Core.Models;

namespace OurStory.Services.Affinity;

/// <summary>
/// 心有灵犀的答题与封存题目管理接口
/// </summary>
public interface IAffinityService {
    /// <summary>
    /// 异步获取亲密度主页数据
    /// </summary>
    /// <param name="userId">用户编号</param>
    /// <param name="role">用户角色</param>
    /// <param name="page">历史记录页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>主页数据</returns>
    Task<AffinityDashboard> GetDashboardAsync(int userId, UserRole role, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取今日答题状态
    /// </summary>
    /// <param name="userId">用户编号</param>
    /// <param name="role">用户角色</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>答题状态</returns>
    Task<string> GetTodayStatusAsync(int userId, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步提交今日答案
    /// </summary>
    /// <param name="dailyQuestionId">每日题目编号</param>
    /// <param name="answer">与题型匹配的回答</param>
    /// <param name="userId">用户编号</param>
    /// <param name="role">用户角色</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提交结果</returns>
    Task<AffinitySubmitResult> SubmitAsync(
        int dailyQuestionId,
        AffinityAnswerSubmission answer,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取一页封存题目
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>封存题目分页列表</returns>
    Task<PagedList<AffinityQuestionCard>> GetSealedQuestionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取一页双方均已完成的只读作答记录
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>共同作答记录分页列表</returns>
    Task<PagedList<AffinityAnsweredQuestionCard>> GetAnsweredQuestionsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步创建并封存题目
    /// </summary>
    /// <param name="model">题目创建模型</param>
    /// <param name="creatorUserId">创建者用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建后的题目信息</returns>
    Task<AffinityQuestionCard> CreateQuestionAsync(AffinityQuestionCreateModel model, int creatorUserId, CancellationToken cancellationToken = default);
}
