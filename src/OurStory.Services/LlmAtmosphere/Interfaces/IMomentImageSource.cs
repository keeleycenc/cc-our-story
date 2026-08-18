// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using OurStory.Core.Entities;

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 定义点点滴滴图片的模型输入获取能力
/// </summary>
/// <remarks>
/// 具体图片来源由实现内部处理，可兼容本地存储与 OSS 等不同存储方式。
/// 上层业务仅负责获取可供模型使用的图片，不依赖具体存储实现
/// </remarks>
public interface IMomentImageSource {
    /// <summary>
    /// 异步收集指定记录中可供模型使用的图片
    /// </summary>
    /// <param name="moment">目标点点滴滴记录</param>
    /// <param name="max">最多收集的图片数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>可直接用于模型请求的图片集合；没有可用图片时返回空集合</returns>
    Task<IReadOnlyList<ResponsesImage>> CollectAsync(
        Moment moment,
        int max,
        CancellationToken cancellationToken = default);
}
