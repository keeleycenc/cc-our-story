// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;
using OurStory.Core.Models;

namespace OurStory.Services.Anniversaries;

/// <summary>
/// 提供纪念日查询与管理能力
/// </summary>
public interface IAnniversaryService {
    /// <summary>
    /// 异步获取当前访问者可查看的纪念日列表
    /// </summary>
    /// <param name="isOwner">是否为站点所有者，所有者可查看私密纪念日</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，按纪念日规则排序后的纪念日列表</returns>
    Task<IReadOnlyList<AnniversaryOccurrence>> GetForViewerAsync(bool isOwner, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取全部纪念日列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，按纪念日规则排序后的全部纪念日列表</returns>
    Task<IReadOnlyList<AnniversaryOccurrence>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取指定纪念日
    /// </summary>
    /// <param name="id">纪念日 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，纪念日，不存在时返回 null</returns>
    Task<Anniversary?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取当前访问者可查看的纪念日数量
    /// </summary>
    /// <param name="isOwner">是否为站点所有者，所有者可统计私密纪念日</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，可查看的纪念日数量</returns>
    Task<int> CountForViewerAsync(bool isOwner, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取私密纪念日数量
    /// </summary>
    /// <param name="cancellationToken">用于取消异步操作的令牌</param>
    /// <returns>私密纪念日的数量</returns>
    Task<int> CountPrivateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步获取当前访问者可查看的指定纪念日详情
    /// </summary>
    /// <param name="id">纪念日 ID</param>
    /// <param name="isOwner">是否为站点所有者，所有者可查看私密纪念日</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，纪念日详情，不存在或无权查看时返回 null</returns>
    Task<AnniversaryOccurrence?> GetOccurrenceAsync(int id, bool isOwner, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步创建纪念日
    /// </summary>
    /// <param name="model">纪念日编辑数据</param>
    /// <param name="authorId">作者 ID，可为空</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，创建后的纪念日</returns>
    Task<Anniversary> CreateAsync(AnniversaryEditModel model, int? authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步更新指定纪念日
    /// </summary>
    /// <param name="id">纪念日 ID</param>
    /// <param name="model">纪念日编辑数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，更新成功返回 true，纪念日不存在时返回 false</returns>
    Task<bool> UpdateAsync(int id, AnniversaryEditModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除指定纪念日
    /// </summary>
    /// <param name="id">纪念日 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，删除成功返回 true，纪念日不存在时返回 false</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
