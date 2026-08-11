// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;
using OurStory.Core.Models;

namespace OurStory.Services.Moments;

/// <summary>
/// 提供回忆列表查询、详情查看、密码解锁以及后台管理等功能
/// </summary>
public interface IMomentService {
    /// <summary>
    /// 异步执行分页获取回忆卡片列表
    /// </summary>
    /// <param name="page">页码，从 1 开始</param>
    /// <param name="viewer">当前访问者身份及权限信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，分页后的回忆卡片列表</returns>
    Task<PagedList<MomentCard>> GetPageAsync(
        int page,
        MomentViewer viewer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取最新的回忆卡片列表
    /// </summary>
    /// <param name="count">需要获取的数量</param>
    /// <param name="viewer">当前访问者身份及权限信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，最新回忆卡片列表</returns>
    Task<IReadOnlyList<MomentCard>> GetLatestAsync(
        int count,
        MomentViewer viewer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取已发布回忆数量
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，已发布回忆总数量</returns>
    Task<int> CountPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步根据短链接标识获取回忆详情
    /// </summary>
    /// <param name="slug">回忆唯一标识</param>
    /// <param name="viewer">当前访问者身份及权限信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，回忆详情；不存在或不可见时返回 null</returns>
    Task<MomentDetail?> GetDetailAsync(
        string slug,
        MomentViewer viewer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步验证受保护回忆的访问密码
    /// </summary>
    /// <param name="slug">回忆唯一标识</param>
    /// <param name="password">访问密码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，验证成功返回回忆编号，否则返回 null</returns>
    Task<int?> UnlockAsync(
        string slug,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步执行后台分页获取回忆记录
    /// </summary>
    /// <param name="page">页码，从 1 开始</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="authorId">只要这个人发的；传 null 表示不限作者</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，后台回忆分页列表</returns>
    Task<PagedList<Moment>> ListForAdminAsync(
        int page,
        int pageSize,
        int? authorId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取回忆总数，含草稿
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，回忆总数量</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步根据编号获取回忆实体
    /// </summary>
    /// <param name="id">回忆编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，回忆实体；不存在时返回 null</returns>
    Task<Moment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步创建新的回忆记录
    /// </summary>
    /// <param name="model">回忆编辑模型</param>
    /// <param name="authorId">创建者用户编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，创建成功后的回忆实体</returns>
    Task<Moment> CreateAsync(
        MomentEditModel model,
        int authorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步更新指定回忆记录
    /// </summary>
    /// <param name="id">回忆编号</param>
    /// <param name="model">回忆编辑模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，更新成功返回 true；记录不存在返回 false</returns>
    Task<bool> UpdateAsync(
        int id,
        MomentEditModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除指定回忆记录
    /// </summary>
    /// <param name="id">回忆编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，删除成功返回 true；记录不存在返回 false</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
