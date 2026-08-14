// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Storage;

/// <summary>
/// 按需生成并缓存本地附件的缩略图
/// </summary>
public interface IThumbnailService {
    /// <summary>
    /// 异步取得指定附件的缩略图文件路径，没有就先生成一份
    /// </summary>
    /// <param name="objectKey">附件对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，缩略图的绝对路径；原图不存在或这个格式压不了时返回 null</returns>
    Task<string?> EnsureAsync(string objectKey, CancellationToken cancellationToken = default);
}
