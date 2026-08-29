// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 表示 UserConfiguration
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User> {
    /// <summary>
    /// 配置用户实体的数据库映射
    /// </summary>
    public void Configure(EntityTypeBuilder<User> builder) {
        _ = builder.ToTable("users");

        _ = builder.HasKey(user => user.Id);

        _ = builder.Property(user => user.UserName).HasMaxLength(32).IsRequired();
        _ = builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();

        // SQLite 的 NOCASE 只覆盖 ASCII，用户名本来就只允许字母数字，够用
        _ = builder.HasIndex(user => user.UserName).IsUnique();
        _ = builder.Property(user => user.UserName).UseCollation("NOCASE");

        _ = builder.HasIndex(user => user.CoupleRelationshipId);
        _ = builder.HasOne(user => user.CoupleRelationship)
            .WithMany(relationship => relationship.Members)
            .HasForeignKey(user => user.CoupleRelationshipId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
