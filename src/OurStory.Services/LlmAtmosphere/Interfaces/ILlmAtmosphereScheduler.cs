// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Services.LlmAtmosphere;

/// <summary>
/// 定义氛围组互动任务的调度能力，负责根据业务事件生成并安排延迟互动
/// </summary>
/// <remarks>
/// 点点滴滴发布与评论流程不应等待模型调用完成，因此业务层仅通过事件方法通知调度器。
/// 调度器本身不访问数据库、不调用模型服务，也不向上层传播异常，仅负责根据配置生成并加入待执行任务。
/// 实际模型调用由后台处理流程异步完成，调用失败不会影响正常业务。
/// </remarks>
public interface ILlmAtmosphereScheduler {
    /// <summary>
    /// 获取当前尚未执行的待调度任务数量
    /// </summary>
    int Pending { get; }

    /// <summary>
    /// 处理点点滴滴发布事件，并按配置生成可能的互动任务
    /// </summary>
    /// <param name="momentId">目标记录的唯一标识符</param>
    /// <param name="isProtected">指示目标记录是否受保护</param>
    void OnMomentPublished(int momentId, bool isProtected);

    /// <summary>
    /// 处理新增评论事件，并按配置生成可能的回复任务
    /// </summary>
    /// <param name="momentId">目标记录的唯一标识符</param>
    /// <param name="commentId">新增评论的唯一标识符</param>
    /// <param name="repliedMemberId">被回复评论所属的氛围组角色唯一标识符；非氛围组评论时为 null</param>
    /// <param name="isProtected">指示目标记录是否受保护</param>
    void OnCommentAdded(int momentId, int commentId, string? repliedMemberId, bool isProtected);

    /// <summary>
    /// 将一项氛围组互动任务加入待执行队列
    /// </summary>
    /// <param name="trigger">待调度的互动任务</param>
    /// <returns>成功加入队列时返回 <see langword="true"/>；任务重复或队列已满时返回 <see langword="false"/></returns>
    bool Schedule(LlmAtmosphereTrigger trigger);

    /// <summary>
    /// 获取并移除所有已到计划执行时间的互动任务
    /// </summary>
    /// <returns>按计划执行时间升序排列的到期任务集合</returns>
    IReadOnlyList<LlmAtmosphereTrigger> TakeDue();
}
