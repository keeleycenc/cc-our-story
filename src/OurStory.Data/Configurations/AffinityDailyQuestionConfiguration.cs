// Copyright (c) 2026 Keeleycenc.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OurStory.Core.Entities;

namespace OurStory.Data.Configurations;

public class AffinityDailyQuestionConfiguration : IEntityTypeConfiguration<AffinityDailyQuestion> {
    public void Configure(EntityTypeBuilder<AffinityDailyQuestion> builder) {
        _ = builder.ToTable("affinity_daily_questions");
        _ = builder.HasKey(item => item.Id);
        _ = builder.Property(item => item.Day).HasMaxLength(10).IsRequired();
        _ = builder.Property(item => item.QuestionText).HasMaxLength(300).IsRequired();
        _ = builder.Property(item => item.Category).HasMaxLength(30).IsRequired();
        _ = builder.Property(item => item.OptionsJson).IsRequired();
        _ = builder.HasIndex(item => item.Day).IsUnique();
        _ = builder.HasOne(item => item.Question).WithMany(item => item.DailyQuestions)
            .HasForeignKey(item => item.QuestionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AffinityAnswerConfiguration : IEntityTypeConfiguration<AffinityAnswer> {
    public void Configure(EntityTypeBuilder<AffinityAnswer> builder) {
        _ = builder.ToTable("affinity_answers");
        _ = builder.HasKey(item => item.Id);
        _ = builder.HasIndex(item => new { item.DailyQuestionId, item.Role }).IsUnique();
        _ = builder.HasIndex(item => new { item.DailyQuestionId, item.UserId }).IsUnique();
        _ = builder.HasOne(item => item.DailyQuestion).WithMany(item => item.Answers)
            .HasForeignKey(item => item.DailyQuestionId).OnDelete(DeleteBehavior.Cascade);
        _ = builder.HasOne(item => item.User).WithMany()
            .HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
