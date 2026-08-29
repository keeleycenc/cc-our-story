// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 花信每日补充记录的数据库映射
/// </summary>
public sealed class CycleDailyLogConfiguration : IEntityTypeConfiguration<CycleDailyLog> {
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CycleDailyLog> builder) {
        _ = builder.ToTable("cycle_daily_logs");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Note).HasMaxLength(300).IsRequired();
        _ = builder.Ignore(item => item.IsEmpty);

        // 同一情侣关系在同一天仅保留一条补充记录
        _ = builder.HasIndex(item => new { item.RelationshipId, item.Date }).IsUnique();

        _ = builder.HasOne(item => item.Relationship)
            .WithMany(item => item.CycleDailyLogs)
            .HasForeignKey(item => item.RelationshipId)
            .OnDelete(DeleteBehavior.Cascade);
        _ = builder.HasOne(item => item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        _ = builder.HasOne(item => item.UpdatedByUser)
            .WithMany()
            .HasForeignKey(item => item.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
