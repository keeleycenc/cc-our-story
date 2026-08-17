// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 配置推送设备实体映射
/// </summary>
public class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice> {
    /// <summary>
    /// 配置数据库表、字段和索引
    /// </summary>
    public void Configure(EntityTypeBuilder<PushDevice> builder) {
        _ = builder.ToTable("push_devices");
        _ = builder.HasKey(device => device.Id);

        // 推送服务给的地址可以很长，Apple 和 FCM 的都在几百字符上下
        _ = builder.Property(device => device.Endpoint).HasMaxLength(500).IsRequired();
        _ = builder.Property(device => device.P256dh).HasMaxLength(120).IsRequired();
        _ = builder.Property(device => device.Auth).HasMaxLength(48).IsRequired();
        _ = builder.Property(device => device.DeviceName).HasMaxLength(80).IsRequired();

        _ = builder.HasOne(device => device.User)
            .WithMany()
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 同一台设备重新授权拿到的还是这个地址，靠唯一索引把它认回来
        _ = builder.HasIndex(device => device.Endpoint).IsUnique();
        _ = builder.HasIndex(device => device.UserId);
    }
}
