// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 配置心意流水实体映射
/// </summary>
public class HeartPointEntryConfiguration : IEntityTypeConfiguration<HeartPointEntry> {
    /// <summary>
    /// 配置数据库表、字段和索引
    /// </summary>
    public void Configure(EntityTypeBuilder<HeartPointEntry> builder) {
        _ = builder.ToTable("heart_point_entries");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.SourceKey).HasMaxLength(80).IsRequired();
        _ = builder.Property(item => item.Note).HasMaxLength(120).IsRequired();
        _ = builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(item => new { item.UserId, item.SourceKey }).IsUnique();
        _ = builder.HasIndex(item => item.CreatedAt);
    }
}
