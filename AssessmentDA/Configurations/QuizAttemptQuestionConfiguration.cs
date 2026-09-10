using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssessmentDA.Configurations;

public class QuizAttemptQuestionConfiguration : IEntityTypeConfiguration<QuizAttemptQuestion>
{
    public void Configure(EntityTypeBuilder<QuizAttemptQuestion> entity)
    {
        entity.ToTable("QuizAttemptQuestions", "Assessment");

        entity.HasIndex(e => new { e.QuizAttemptId, e.QuestionId }, "UQ_QuizAttemptQuestions_AttemptId_QuestionId")
            .IsUnique();

        entity.HasIndex(e => e.QuestionId, "IX_QuizAttemptQuestions_QuestionId");

        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasDefaultValueSql("(sysutcdatetime())");

        // DB: ON DELETE CASCADE — a row here has no meaning outside its
        // attempt.
        entity.HasOne(d => d.QuizAttempt).WithMany(p => p.QuizAttemptQuestions)
            .HasForeignKey(d => d.QuizAttemptId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuizAttemptQuestions_QuizAttempts");

        // DB: no ON DELETE clause (NO ACTION) — Questions are durable
        // content and must not be orphaned by attempt records.
        entity.HasOne(d => d.Question).WithMany(p => p.QuizAttemptQuestions)
            .HasForeignKey(d => d.QuestionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_QuizAttemptQuestions_Questions");
    }
}