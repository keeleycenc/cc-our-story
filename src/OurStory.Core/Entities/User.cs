// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace OurStory.Core.Entities;

/// <summary>
/// 能登录后台的用户，正常情况下只有男主和女主
/// </summary>
public class User {
    /// <summary>
    /// 获取或设置用户的唯一标识符
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 获取或设置当前所属情侣关系
    /// </summary>
    public int? CoupleRelationshipId { get; set; }

    /// <summary>
    /// 获取或设置登录名，全站唯一，不区分大小写
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置用户的角色，只可能是 <see cref="UserRole.Boy"/> 或 <see cref="UserRole.Girl"/>
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Boy;

    /// <summary>
    /// 获取或设置用户的密码哈希值，格式见 PasswordHasher
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置用户是否启用，禁用的用户无法登录后台
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 获取或设置用户的创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置用户的最后登录时间；从未登录过的用户为 null
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// 获取或设置用户关联的点点滴滴列表
    /// </summary>
    public ICollection<Moment> Moments { get; set; } = [];

    /// <summary>
    /// 获取或设置当前所属情侣关系
    /// </summary>
    public CoupleRelationship? CoupleRelationship { get; set; }
}
