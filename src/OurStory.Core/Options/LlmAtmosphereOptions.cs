// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace OurStory.Core.Options;

/// <summary>
/// 表示配置文件中的 <c>LlmAtmosphere</c> 节点，用于管理氛围组的全局设置与角色配置
/// </summary>
/// <remarks>
/// 配置与 OSS、VAPID 等敏感信息保持一致，统一存放在配置文件中，而不是数据库中。
/// 后台修改后通过 <see cref="Configuration.ActiveConfiguration"/> 整体更新，无需重启站点。
/// 角色可以随时删除，历史评论仅保留稳定的角色标识及必要快照，因此删除角色不会影响已有内容展示。
/// </remarks>
public class LlmAtmosphereOptions {
    /// <summary>
    /// 获取或设置一个值，指示是否启用氛围组
    /// </summary>
    /// <remarks>
    /// 关闭后不再产生新的模型调用，已经生成的评论与回复不受影响。
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// 获取或设置一个值，指示是否允许将受保护的点点滴滴发送给模型
    /// </summary>
    /// <remarks>
    /// 默认关闭。受保护内容只有在后台显式开启后才允许参与氛围组互动；
    /// 草稿内容始终不会发送给模型。
    /// </remarks>
    public bool IncludeProtected { get; set; }

    /// <summary>
    /// 获取或设置同一条点点滴滴允许生成的氛围组评论数量上限
    /// </summary>
    /// <remarks>
    /// 用于控制互动密度，避免氛围组内容占据过多评论空间。
    /// </remarks>
    public int MaxCommentsPerMoment { get; set; } = 6;

    /// <summary>
    /// 获取或设置后台巡检回看的最近记录天数
    /// </summary>
    public int RecentDays { get; set; } = 3;

    /// <summary>
    /// 获取或设置后台巡检的执行间隔，单位为分钟
    /// </summary>
    public int SweepMinutes { get; set; } = 20;

    /// <summary>
    /// 获取或设置同一条记录中两次氛围组互动之间的最短间隔，单位为分钟
    /// </summary>
    /// <remarks>
    /// 用于分散不同角色的互动时间，避免多条评论集中在短时间内出现。
    /// </remarks>
    public int QuietMinutes { get; set; } = 30;

    /// <summary>
    /// 获取或设置单次模型调用的超时时间，单位为秒
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 获取或设置单次模型请求最多携带的图片数量
    /// </summary>
    public int MaxImages { get; set; } = 3;

    /// <summary>
    /// 获取或设置氛围组角色集合
    /// </summary>
    /// <remarks>
    /// 每个角色可以独立配置服务地址、模型、人设及互动行为。
    /// </remarks>
    public IList<LlmAtmosphereMember> Members { get; set; } = [];

    /// <summary>
    /// 获取当前可参与互动的角色集合
    /// </summary>
    /// <remarks>
    /// 只有在总开关启用且角色自身配置完整、状态可用时，才会包含在结果中。
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<LlmAtmosphereMember> ActiveMembers =>
        Enabled ? [.. Members.Where(member => member.IsUsable)] : [];

    /// <summary>
    /// 根据角色标识查找对应的氛围组角色
    /// </summary>
    /// <param name="id">角色唯一标识符</param>
    /// <returns>找到时返回对应角色；不存在或已删除时返回 null</returns>
    public LlmAtmosphereMember? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Members.FirstOrDefault(member =>
                string.Equals(member.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 为新角色分配一个未被占用的标识
    /// </summary>
    /// <returns>可直接使用的角色标识</returns>
    public string NewId() {
        string candidate;

        do {
            candidate = Guid.NewGuid().ToString("n")[..12];
        } while (Find(candidate) is not null);

        return candidate;
    }

    /// <summary>
    /// 基于期望名称生成一个不与现有角色重名的名称
    /// </summary>
    /// <param name="desired">期望使用的名称</param>
    /// <returns>未被占用的名称；必要时追加序号</returns>
    public string UniqueName(string desired) {
        var wanted = (desired ?? string.Empty).Trim();
        if (wanted.Length == 0) {
            wanted = "新角色";
        }

        var candidate = wanted;
        var suffix = 2;

        while (Members.Any(member => string.Equals(member.Name, candidate, StringComparison.Ordinal))) {
            candidate = $"{wanted} {suffix++}";
        }

        return candidate;
    }
}
