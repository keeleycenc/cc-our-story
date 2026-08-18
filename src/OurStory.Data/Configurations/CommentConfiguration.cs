// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

/// <summary>
/// 表示 CommentConfiguration
/// </summary>
public class CommentConfiguration : IEntityTypeConfiguration<Comment> {
    /// <summary>
    /// 配置留言实体的数据库映射
    /// </summary>
    public void Configure(EntityTypeBuilder<Comment> builder) {
        _ = builder.ToTable("comments");

        _ = builder.HasKey(comment => comment.Id);

        _ = builder.Property(comment => comment.AuthorName).HasMaxLength(64).IsRequired();
        _ = builder.Property(comment => comment.AuthorMail).HasMaxLength(160);
        _ = builder.Property(comment => comment.AuthorUrl).HasMaxLength(300);
        _ = builder.Property(comment => comment.Content).HasMaxLength(2000).IsRequired();
        _ = builder.Property(comment => comment.VisitorHash).HasMaxLength(64);
        _ = builder.Property(comment => comment.LlmMemberId).HasMaxLength(64);
        _ = builder.Property(comment => comment.LlmAvatarUrl).HasMaxLength(500);

        // Source 由这两个字段现算，不落库：留言的来路永远跟着字段走，不会对不上
        _ = builder.Ignore(comment => comment.Source);

        _ = builder.HasIndex(comment => new { comment.MomentId, comment.CreatedAt });

        // 巡检要按「这条记录上某个角色留过没有」来挡重复触发，走的就是这个索引
        _ = builder.HasIndex(comment => new { comment.MomentId, comment.LlmMemberId });

        _ = builder.HasOne(comment => comment.Moment)
            .WithMany(moment => moment.Comments)
            .HasForeignKey(comment => comment.MomentId)
            .OnDelete(DeleteBehavior.Cascade);

        // 删父留言时把回复一起带走，交给应用层显式处理，避免 SQLite 上的级联环
        _ = builder.HasOne(comment => comment.Parent)
            .WithMany(comment => comment.Replies)
            .HasForeignKey(comment => comment.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne(comment => comment.Author)
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
