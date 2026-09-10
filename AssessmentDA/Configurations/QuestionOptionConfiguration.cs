using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssessmentDA.Configurations;

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> entity)
    {
        entity.ToTable("QuestionOptions", "Assessment");

        // Filtered unique index: at most one IsCorrect = 1 row per question.
        // The full rule ("exactly one, never zero") remains a service-layer
        // responsibility — this only enforces the DB-appropriate half.
        entity.HasIndex(e => e.QuestionId, "UQ_QuestionOptions_OneCorrectPerQuestion")
            .IsUnique()
            .HasFilter("([IsCorrect]=(1))");

        entity.HasIndex(e => new { e.QuestionId, e.DisplayOrder }, "UQ_QuestionOptions_QuestionId_DisplayOrder").IsUnique();

        // Composite alternate key backing the composite FK declared on
        // QuizAttemptMistake (QuestionId, SelectedOptionId) -> here
        // (QuestionId, Id). The single-column PK on Id is unchanged; this
        // is an additional covering unique index required by SQL Server
        // for the composite FK to be valid.
        entity.HasIndex(e => new { e.QuestionId, e.Id }, "UQ_QuestionOptions_QuestionId_Id").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(3)
            .HasDefaultValueSql("(sysutcdatetime())");

        entity.HasOne(d => d.Question).WithMany(p => p.QuestionOptions)
            .HasForeignKey(d => d.QuestionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_QuestionOptions_Questions");
    }
}