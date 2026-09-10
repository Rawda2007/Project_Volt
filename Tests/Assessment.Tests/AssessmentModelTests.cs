using AssessmentDA.Context;
using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Assessment.Tests;

/// <summary>
/// Guards the Database-First contract: the EF model must still build, and the
/// mappings the concurrency and historical-integrity fixes depend on must be
/// the ones the schema declares.
/// </summary>
public class AssessmentModelTests
{
    private static AssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AssessmentDbContext>()
            .UseSqlServer("Server=none;Database=VoltDB;Trusted_Connection=True;")
            .Options;

        return new AssessmentDbContext(options);
    }

    [Fact]
    public void Model_BuildsWithoutErrors()
    {
        using var db = CreateContext();

        // Forces full model construction, including the composite principal-key
        // relationships on QuizAttemptMistake and QuizAttemptQuestion.
        Assert.NotNull(db.Model);
    }

    [Fact]
    public void QuizAttempt_RowVersion_IsAConcurrencyToken()
    {
        using var db = CreateContext();

        var rowVersion = db.Model
            .FindEntityType(typeof(QuizAttempt))!
            .FindProperty(nameof(QuizAttempt.RowVersion))!;

        // This is what makes EF append RowVersion to the UPDATE's WHERE clause,
        // so the losing concurrent submit affects 0 rows and throws.
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void QuizAttemptQuestion_SnapshotColumns_AreRequired()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(typeof(QuizAttemptQuestion))!;

        Assert.False(entity.FindProperty(nameof(QuizAttemptQuestion.TopicId))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(QuizAttemptQuestion.Difficulty))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(QuizAttemptQuestion.CorrectOptionId))!.IsNullable);
    }

    [Fact]
    public void QuizAttemptQuestion_CorrectOption_UsesTheCompositeQuestionScopedFk()
    {
        using var db = CreateContext();

        var foreignKey = db.Model
            .FindEntityType(typeof(QuizAttemptQuestion))!
            .GetForeignKeys()
            .Single(fk => fk.GetConstraintName() == "FK_QuizAttemptQuestions_QuestionId_CorrectOptionId");

        // (QuestionId, CorrectOptionId) -> QuestionOptions(QuestionId, Id):
        // the answer key cannot point at another question's option.
        Assert.Equal(
            new[] { nameof(QuizAttemptQuestion.QuestionId), nameof(QuizAttemptQuestion.CorrectOptionId) },
            foreignKey.Properties.Select(p => p.Name).ToArray());
        Assert.Equal(
            new[] { nameof(QuestionOption.QuestionId), nameof(QuestionOption.Id) },
            foreignKey.PrincipalKey.Properties.Select(p => p.Name).ToArray());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
