// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 配置纪念日实体映射
/// </summary>
public class AnniversaryConfiguration : IEntityTypeConfiguration<Anniversary> {
    /// <summary>
    /// 配置数据库表、字段和索引
    /// </summary>
    public void Configure(EntityTypeBuilder<Anniversary> builder) {
        _ = builder.ToTable("anniversaries");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Title).HasMaxLength(80).IsRequired();
        _ = builder.Property(item => item.CalendarType).HasDefaultValue(AnniversaryCalendarType.Solar);
        _ = builder.Property(item => item.CoverUrl).HasMaxLength(500);
        _ = builder.HasOne(item => item.Author)
            .WithMany()
            .HasForeignKey(item => item.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);
        _ = builder.HasIndex(item => new { item.IsPrivate, item.AnniversaryDate });
    }
}
