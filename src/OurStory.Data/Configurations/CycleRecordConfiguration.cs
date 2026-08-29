// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 花信记录数据库映射
/// </summary>
public sealed class CycleRecordConfiguration : IEntityTypeConfiguration<CycleRecord> {
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CycleRecord> builder) {
        _ = builder.ToTable("cycle_records");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Note).HasMaxLength(500).IsRequired();
        _ = builder.Property(item => item.RequestKey).HasMaxLength(36).IsRequired();
        _ = builder.Property(item => item.Summary).HasMaxLength(1200).IsRequired();
        _ = builder.Property(item => item.SummaryStamp).HasMaxLength(64).IsRequired();

        _ = builder.HasIndex(item => new { item.RelationshipId, item.StartDate });
        _ = builder.HasIndex(item => new { item.RelationshipId, item.RequestKey }).IsUnique();
        _ = builder.HasIndex(item => item.RelationshipId)
            .IsUnique()
            .HasFilter("\"EndDate\" IS NULL");

        _ = builder.HasOne(item => item.Relationship)
            .WithMany(item => item.CycleRecords)
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
