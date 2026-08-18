// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 定义氛围组互动的核心业务服务，负责读取上下文、调用模型并写入评论
/// </summary>
public interface ILlmAtmosphereService {
    /// <summary>
    /// 异步执行一项已到期的氛围组互动任务
    /// </summary>
    /// <remarks>
    /// 任务从创建到执行之间可能经过较长时间，因此执行前会重新校验目标记录、
    /// 发布状态、评论数量及其他相关条件，确保当前仍满足互动要求。
    /// </remarks>
    /// <param name="trigger">待执行的氛围组互动任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果成功创建了一条留言或回复，则返回 <see langword="true"/>；否则返回 <see langword="false"/></returns>
    Task<bool> RunAsync(LlmAtmosphereTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步检查近期记录，并计算后续需要调度的氛围组互动任务
    /// </summary>
    /// <remarks>
    /// 延迟任务仅保存在内存中，站点重启后可能丢失。
    /// 巡检过程会同时检查近期记录，并补充符合条件但尚未调度的互动任务。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本轮检查生成的待调度互动任务集合</returns>
    Task<IReadOnlyList<LlmAtmosphereTrigger>> SweepAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步立即触发指定角色执行一次互动，用于后台验证模型配置
    /// </summary>
    /// <remarks>
    /// 该操作会跳过触发概率、延迟等待以及同一记录顶层留言数量等日常互动限制，
    /// 直接发起一次模型调用，便于快速确认服务地址、模型与 API Key 等配置是否可用。
    /// 草稿不会参与，受密码保护的记录仍需显式允许，这些隐私与内容保护规则不会被跳过。
    /// </remarks>
    /// <param name="memberId">角色唯一标识符；未启用的角色同样可以进行验证</param>
    /// <param name="momentId">作为互动上下文的记录标识符；为 0 时使用最近发布的一条记录</param>
    /// <param name="persist">为 <see langword="true"/> 时将生成内容写入评论区，否则仅返回预览结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次手动验证的执行结果</returns>
    Task<AtmosphereProbe> ProbeAsync(
        string memberId,
        int momentId,
        bool persist,
        CancellationToken cancellationToken = default);
}
