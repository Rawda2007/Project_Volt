using AssessmentBL;
using AssessmentBL.Services;
using AssessmentDA.Context;
using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Assessment.Tests;

/// <summary>
/// The second guard on a hint: not quoting the answer is not enough, it must not
/// come within a hair of it either.
/// </summary>
public class HintSimilarityTests
{
    private const decimal Threshold = 0.80m;

    [Theory]
    [InlineData("الوحدة هي الاووم", "الأوم")]          // one letter added
    [InlineData("the unit is the ohmm", "ohm")]        // misspelled on purpose
    [InlineData("فكّر في قانون أوم البسيط", "أوم")]     // exact word, different sentence
    public void AHintThatAllButSpellsTheAnswer_IsCaught(string hint, string correctOptionText)
        => Assert.True(HintSafety.IsTooSimilar(hint, correctOptionText, Threshold));

    [Theory]
    [InlineData("افتكر إن الوحدة اسمها على اسم عالم ألماني", "الأوم")]
    [InlineData("think about which scientist the unit is named after", "ohm")]
    [InlineData("قارن بين الجهد والتيار", "المقاومة الكهربية")]
    public void AHintThatOnlyTalksAround_TheAnswerIsAllowed(string hint, string correctOptionText)
        => Assert.False(HintSafety.IsTooSimilar(hint, correctOptionText, Threshold));

    [Fact]
    public void AMultiWordAnswer_IsCaughtEvenWhenSlightlyReworded()
        => Assert.True(HintSafety.IsTooSimilar(
            "الإجابة هي المقاومه الكهربيه يا بطل", "المقاومة الكهربية", Threshold));

    [Fact]
    public void AThresholdAboveOne_TurnsTheCheckOff()
        => Assert.False(HintSafety.IsTooSimilar("الوحدة هي الأوم", "الأوم", 1.01m));

    [Fact]
    public void AtAThresholdOfOne_OnlyWhatReadsAsTheAnswerItselfCounts()
    {
        Assert.True(HintSafety.IsTooSimilar("الوحدة هي الأوم", "الأوم", 1m));
        Assert.False(HintSafety.IsTooSimilar("افتكر في عالم ألماني", "الأوم", 1m));
    }

    [Theory]
    [InlineData("", "الأوم")]
    [InlineData("أي تلميح", null)]
    public void NothingToCompare_IsNotALeak(string hint, string? correctOptionText)
        => Assert.False(HintSafety.IsTooSimilar(hint, correctOptionText, Threshold));
}

public class HintEscalationSettingsTests
{
    [Fact]
    public void TwoLevels_AndAStrictSimilarityThreshold_AreTheDefaults()
    {
        var settings = new AssessmentSettings();

        Assert.Equal(2, settings.EffectiveMaxHintLevels);
        Assert.Equal(0.80m, settings.EffectiveHintSimilarityThreshold);
    }

    [Fact]
    public void TheSimilarityThreshold_CannotBeConfiguredBelowAHalf()
        => Assert.Equal(0.5m, new AssessmentSettings { HintSimilarityThreshold = 0.1m }.EffectiveHintSimilarityThreshold);
}

public class HintModelTests
{
    private static AssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AssessmentDbContext>()
            .UseSqlServer("Server=none;Database=VoltDB;Trusted_Connection=True;")
            .Options;

        return new AssessmentDbContext(options);
    }

    [Fact]
    public void AHint_BelongsToAnAttemptAndQuestion_NotOnlyToAMistake()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(QuestionHint))!;

        // The Hint button writes hints while the attempt is still in progress,
        // before any mistake row exists.
        Assert.False(entity.FindProperty(nameof(QuestionHint.QuizAttemptId))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(QuestionHint.QuestionId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(QuestionHint.QuizAttemptMistakeId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(QuestionHint.AttemptNumber))!.IsNullable);
    }

    [Fact]
    public void HintsAreUnique_PerAttemptQuestionLanguageAndSequence()
    {
        using var db = CreateContext();

        var index = db.Model.FindEntityType(typeof(QuestionHint))!
            .GetIndexes()
            .Single(i => i.IsUnique);

        Assert.Equal(
            new[]
            {
                nameof(QuestionHint.QuizAttemptId),
                nameof(QuestionHint.QuestionId),
                nameof(QuestionHint.LanguageCode),
                nameof(QuestionHint.HintSequence)
            },
            index.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void OnlyOneCascadePathReachesAHint()
    {
        using var db = CreateContext();

        var foreignKeys = db.Model.FindEntityType(typeof(QuestionHint))!.GetForeignKeys().ToList();

        // Deleting an attempt cascades through its questions to their hints; the
        // mistake link must not cascade too, or SQL Server rejects the schema.
        Assert.Equal(DeleteBehavior.Cascade,
            foreignKeys.Single(fk => fk.GetConstraintName() == "FK_QuestionHints_QuizAttemptQuestions").DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict,
            foreignKeys.Single(fk => fk.GetConstraintName() == "FK_QuestionHints_QuizAttemptMistakes").DeleteBehavior);
    }
}
