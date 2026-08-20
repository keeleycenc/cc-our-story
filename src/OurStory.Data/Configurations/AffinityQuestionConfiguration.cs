// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

public class AffinityQuestionConfiguration : IEntityTypeConfiguration<AffinityQuestion> {
    public void Configure(EntityTypeBuilder<AffinityQuestion> builder) {
        _ = builder.ToTable("affinity_questions");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Text).HasMaxLength(300).IsRequired();
        _ = builder.Property(item => item.Category).HasMaxLength(30).IsRequired();
        _ = builder.HasIndex(item => item.IsActive);
        _ = builder.HasIndex(item => item.CreatedByUserId);
        _ = builder.HasOne(item => item.CreatedByUser).WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AffinityQuestionOptionConfiguration : IEntityTypeConfiguration<AffinityQuestionOption> {
    public void Configure(EntityTypeBuilder<AffinityQuestionOption> builder) {
        _ = builder.ToTable("affinity_question_options");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Text).HasMaxLength(120).IsRequired();
        _ = builder.HasIndex(item => new { item.QuestionId, item.SortOrder }).IsUnique();
        _ = builder.HasOne(item => item.Question).WithMany(item => item.Options)
            .HasForeignKey(item => item.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}
