// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using OurStory.Core.Models;

namespace OurStory.Services.Cycles;

/// <summary>
/// 花信小结生成服务接口
/// </summary>
/// <remarks>
/// 模型已配置并启用时优先使用模型，其它情况使用站内规则。
/// 调用方无需感知具体生成方式，返回结果始终可直接用于展示。
/// </remarks>
public interface ICycleInsightService {
    /// <summary>
    /// 获取一个值，指示当前是否启用模型服务
    /// </summary>
    bool UsesModel { get; }

    /// <summary>
    /// 异步为一次周期写一段小结
    /// </summary>
    /// <param name="context">本次周期的全部事实</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型生成的小结；模型不可用或调用失败时返回站内规则文案</returns>
    Task<CycleSummaryText> WriteAsync(CycleNarrativeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步试调用一次模型，供后台验证配置
    /// </summary>
    /// <param name="context">用于试写的事实上下文；留空时使用内置示例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次试调用的回执</returns>
    /// <remarks>
    /// 结果只回传给调用方，不会写入任何记录；补写仍由后台任务按配置的间隔执行。
    /// </remarks>
    Task<CycleInsightProbe> ProbeAsync(
        CycleNarrativeContext? context = null,
        CancellationToken cancellationToken = default);
}
