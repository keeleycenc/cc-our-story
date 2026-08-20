// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.Storage;

/// <summary>
/// 按需生成并缓存本地附件的派生图
/// </summary>
public interface IThumbnailService {
    /// <summary>
    /// 异步取得指定附件在某一档规格下的文件路径，没有就先生成一份
    /// </summary>
    /// <param name="objectKey">附件对象键</param>
    /// <param name="variant">要的规格，默认卡片封面</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，派生图的绝对路径；原图不存在或这个格式压不了时返回 null</returns>
    Task<string?> EnsureAsync(string objectKey, ImageVariant? variant = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读出原图摆正之后的像素尺寸，只读文件头，不解码整张图
    /// </summary>
    /// <remarks>
    /// 正文里的图要先占好位置，浏览器才知道哪几张真在视口附近；
    /// 没有尺寸的话它们全挤成零高度，原生懒加载就等于没开
    /// </remarks>
    /// <param name="objectKey">附件对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作任务结果，宽高；原图不存在或读不出来时返回 null</returns>
    Task<ImageSize?> MeasureAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步删除指定原图生成的全部本地派生图和尺寸缓存
    /// </summary>
    /// <param name="objectKey">原图对象键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示一个异步操作任务</returns>
    Task ClearAsync(string objectKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// 图片摆正之后的像素尺寸
/// </summary>
/// <param name="Width">宽</param>
/// <param name="Height">高</param>
public readonly record struct ImageSize(int Width, int Height);
