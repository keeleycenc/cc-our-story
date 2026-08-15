// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 配置心愿商品实体映射
/// </summary>
public class ShopItemConfiguration : IEntityTypeConfiguration<ShopItem> {
    /// <summary>
    /// 配置数据库表、字段和索引
    /// </summary>
    public void Configure(EntityTypeBuilder<ShopItem> builder) {
        _ = builder.ToTable("shop_items");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Title).HasMaxLength(60).IsRequired();
        _ = builder.Property(item => item.Description).HasMaxLength(300).IsRequired();
        _ = builder.Property(item => item.CoverUrl).HasMaxLength(500);

        // 发布者和兑换者都不允许级联删账号：这两行数据本来也不会被删
        _ = builder.HasOne(item => item.Seller)
            .WithMany()
            .HasForeignKey(item => item.SellerId)
            .OnDelete(DeleteBehavior.Restrict);
        _ = builder.HasOne(item => item.Buyer)
            .WithMany()
            .HasForeignKey(item => item.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 预设只是发布时的模板，删掉它不该动到已经发出去的心愿
        _ = builder.HasOne(item => item.Preset)
            .WithMany()
            .HasForeignKey(item => item.PresetId)
            .OnDelete(DeleteBehavior.SetNull);

        _ = builder.HasIndex(item => new { item.Status, item.IsPrivate });
        _ = builder.HasIndex(item => item.SellerId);
        _ = builder.HasIndex(item => item.BuyerId);
    }
}
