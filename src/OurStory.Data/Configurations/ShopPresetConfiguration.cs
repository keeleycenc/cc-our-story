// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 配置心愿预设实体映射
/// </summary>
public class ShopPresetConfiguration : IEntityTypeConfiguration<ShopPreset> {
    /// <summary>
    /// 配置数据库表、字段和索引
    /// </summary>
    public void Configure(EntityTypeBuilder<ShopPreset> builder) {
        _ = builder.ToTable("shop_presets");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Title).HasMaxLength(60).IsRequired();
        _ = builder.Property(item => item.Description).HasMaxLength(300).IsRequired();
        _ = builder.Property(item => item.CoverUrl).HasMaxLength(500);
        _ = builder.HasIndex(item => new { item.IsActive, item.SortOrder });
    }
}
