// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Abstractions;

/// <summary>
/// 附件存储。对外只认 object key，形如 <c>ourstory/public/2026/08/&lt;随机名&gt;.png</c>。
/// </summary>
public interface IFileStorage {
    /// <summary>
    /// 获取当前实际生效的存储方式名，用于后台展示
    /// </summary>
    string DriverName { get; }

    /// <summary>
    /// 异步存一个新附件
    /// </summary>
    /// <param name="content">附件内容</param>
    /// <param name="extension">文件扩展名</param>
    /// <param name="contentType">内容类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>一个异步操作任务结果，保存成功后的对象键 objectKey</returns>
    Task<string> SaveAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除一个附件
    /// </summary>
    /// <param name="objectKey">对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>一个异步操作任务结果，表示删除是否成功</returns>
    Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步列出当前存储中的附件，供后台图片库展示
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>一个异步操作任务结果，包含所有附件的列表</returns>
    Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 拿到对外可访问的完整地址
    /// </summary>
    /// <param name="objectKey">对象键</param>
    /// <returns>附件公开访问完整地址</returns>
    string PublicUrl(string objectKey);
}

/// <summary>
/// 存储中的一个附件
/// </summary>
public sealed record StoredFile(string ObjectKey, long Size, DateTimeOffset LastModified);
