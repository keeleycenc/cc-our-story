// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

namespace OurStory.Services.Storage;

/// <summary>
/// 后台媒体库的引用检查与安全删除服务
/// </summary>
public interface IMediaLibraryService {
    /// <summary>
    /// 异步删除媒体文件，如果存在引用则返回引用信息
    /// </summary>
    /// <param name="objectKey">媒体文件对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，包含删除状态或引用信息</returns>
    Task<MediaDeleteResult> DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步查找媒体文件的引用信息
    /// </summary>
    /// <param name="objectKey">媒体文件对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，包含引用记录列表</returns>
    Task<IReadOnlyList<MediaReference>> FindReferencesAsync(string objectKey, CancellationToken cancellationToken = default);
}
