// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 情侣关系数据库映射
/// </summary>
public sealed class CoupleRelationshipConfiguration : IEntityTypeConfiguration<CoupleRelationship> {
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CoupleRelationship> builder) {
        _ = builder.ToTable("couple_relationships");
        _ = builder.HasKey(item => item.Id);
        _ = builder.HasIndex(item => item.IsActive);
    }
}
