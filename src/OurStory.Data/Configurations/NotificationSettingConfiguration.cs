// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 配置通知偏好实体映射
/// </summary>
public class NotificationSettingConfiguration : IEntityTypeConfiguration<NotificationSetting> {
    /// <summary>
    /// 配置数据库表、字段和索引
    /// </summary>
    public void Configure(EntityTypeBuilder<NotificationSetting> builder) {
        _ = builder.ToTable("notification_settings");

        _ = builder.HasKey(setting => setting.UserId);
        _ = builder.Property(setting => setting.UserId).ValueGeneratedNever();

        _ = builder.Property(setting => setting.LastAnniversaryOn).HasMaxLength(10).IsRequired();
        _ = builder.Property(setting => setting.WebPushEnabled).HasDefaultValue(true);
        _ = builder.Property(setting => setting.EmailEnabled).HasDefaultValue(false);
        _ = builder.Property(setting => setting.EmailAddress).HasMaxLength(320).IsRequired();

        _ = builder.HasOne(setting => setting.User)
            .WithMany()
            .HasForeignKey(setting => setting.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
