using System.Reflection;
using AssessmentBL;
using AssessmentBL.Services;
using Shared.Assessment.AI;
using Xunit;

namespace Assessment.Tests;

/// <summary>A hint must never name the correct answer.</summary>
public class HintSafetyTests
{
    [Theory]
    [InlineData("الإجابة هي الأوم", "الأوم")]
    [InlineData("بالأوم نقيس المقاومة", "الأوم")]      // fused prefix ب
    [InlineData("فكّر في وحدة الأوم", "أوم")]          // article ال
    [InlineData("الإجابةُ هي الأُوم", "الأوم")]         // diacritics
    [InlineData("الإجابة صح", "صح")]
    [InlineData("It is the Ohm, of course", "ohm")]  // case
    [InlineData("the answer is 20 volts", "20")]
    [InlineData("وحدة أوم", "الأوم")]                  // article dropped on the hint side
    [InlineData("نقيس وبالأوم المقاومة", "الأوم")]      // two fused prefixes
    [InlineData("للأوم علاقة بالمقاومة", "الأوم")]      // ل + ال contracted
    [InlineData("الناتج ٢٠ فولت", "20")]               // Arabic-Indic digits
    public void NamingTheAnswer_IsCaught(string hint, string correctOptionText)
        => Assert.True(HintSafety.RevealsAnswer(hint, correctOptionText));

    [Theory]
    [InlineData("الإجابة خطأ", "خطأ")]
    [InlineData("الإجابة الصحيحة هي صح", "صح")]
    [InlineData("العبارة خطأ لأن المصباح يحتاج بطارية", "خطأ")]
    [InlineData("The answer is true", "true")]
    public void TrueFalse_AnExplicitVerdict_IsCaught(string hint, string correctOptionText)
        => Assert.True(HintSafety.RevealsAnswer(hint, correctOptionText, isBinaryChoice: true));

    [Theory]
    [InlineData("الإجابة ليست صح", "خطأ", "صح")]
    [InlineData("the answer is not true", "false", "true")]
    [InlineData("The answer isn't true", "false", "true")]
    public void TrueFalse_NegatingTheOtherOption_IsCaught(string hint, string correct, string other)
        => Assert.True(HintSafety.RevealsAnswer(hint, correct, isBinaryChoice: true, otherOptionText: other));

    [Theory]
    [InlineData("خطأ شائع إن المصباح يضيء لوحده — فكّر في مصدر الكهرباء", "خطأ")]
    [InlineData("Think about whether a lamp can glow with nothing true to power it", "true")]
    public void TrueFalse_TheWordAlone_IsNotAVerdict(string hint, string correctOptionText)
        => Assert.False(HintSafety.RevealsAnswer(hint, correctOptionText, isBinaryChoice: true));

    [Theory]
    [InlineData("افتكر إن الوحدة اسمها على اسم عالم ألماني", "الأوم")]
    [InlineData("هل هذا صحيح؟ فكّر في مصدر الكهرباء", "صح")]   // "صحيح" is not "صح"
    [InlineData("Ohmic behaviour depends on temperature", "ohm")]
    [InlineData("the answer is A", "A")]                         // one character: unchecked
    [InlineData("any hint", null)]
    [InlineData("any hint", "   ")]
    public void AHintThatDoesNotNameTheAnswer_IsAllowed(string hint, string? correctOptionText)
        => Assert.False(HintSafety.RevealsAnswer(hint, correctOptionText));
}

/// <summary>The AI proposes; the backend decides.</summary>
public class EssayDecisionTests
{
    private const decimal Threshold = 0.8m;
    private const int MaxFeedback = 1000;

    private static EssayDecision Decide(
        int? points = 2, string? feedback = "أحسنت، ولكن اذكر دور المقاومة.", decimal? confidence = 0.9m,
        string? status = "Ok", IReadOnlyList<string>? flags = null, int maxPoints = 3) =>
        EssayEvaluationService.Decide(
            new EssayEvaluationResult
            {
                ItemId = "1", Status = status, ProposedPoints = points,
                Feedback = feedback, Confidence = confidence, Flags = flags
            },
            maxPoints, Threshold, MaxFeedback);

    [Fact]
    public void AConfidentWellFormedProposal_IsAccepted()
    {
        var decision = Decide();

        Assert.Equal(EssayDecisionKind.Accept, decision.Kind);
        Assert.Equal(2, decision.Points);
    }

    [Fact]
    public void AnUnsureProposal_GoesToAPerson_AndIsKept()
    {
        var decision = Decide(confidence: 0.6m);

        Assert.Equal(EssayDecisionKind.NeedsReview, decision.Kind);
        Assert.Equal(2, decision.Points);
    }

    [Fact]
    public void AFlaggedAnswer_GoesToAPerson_HoweverConfident()
        => Assert.Equal(EssayDecisionKind.NeedsReview, Decide(confidence: 0.99m, flags: ["PersonalData"]).Kind);

    [Fact]
    public void TheAiDeclining_GoesToAPerson()
        => Assert.Equal(EssayDecisionKind.NeedsReview, Decide(status: "Skipped").Kind);

    [Fact]
    public void AMissingStatus_IsReadAsOk()
        => Assert.Equal(EssayDecisionKind.Accept, Decide(status: null).Kind);

    [Fact]
    public void AnUnknownStatus_IsRetried_NotSentToReview()
        => Assert.Equal(EssayDecisionKind.Unusable, Decide(status: "Error").Kind);

    [Theory]
    [InlineData(4)]    // above MaxPoints
    [InlineData(-1)]
    public void AScoreOutOfRange_IsUnusable(int points)
        => Assert.Equal(EssayDecisionKind.Unusable, Decide(points: points).Kind);

    [Fact]
    public void MissingPiecesOrAMissingResult_AreUnusable()
    {
        Assert.Equal(EssayDecisionKind.Unusable, Decide(points: null).Kind);
        Assert.Equal(EssayDecisionKind.Unusable, Decide(feedback: "  ").Kind);
        Assert.Equal(EssayDecisionKind.Unusable, Decide(feedback: new string('x', MaxFeedback + 1)).Kind);
        Assert.Equal(EssayDecisionKind.Unusable, Decide(confidence: null).Kind);
        Assert.Equal(EssayDecisionKind.Unusable, Decide(confidence: 1.5m).Kind);
        Assert.Equal(EssayDecisionKind.Unusable,
            EssayEvaluationService.Decide(null, 3, Threshold, MaxFeedback).Kind);
    }

    [Fact]
    public void TheThresholdIsInclusive()
        => Assert.Equal(EssayDecisionKind.Accept, Decide(confidence: Threshold).Kind);
}

public class SubmissionCompletenessTests
{
    private static void EnsureEveryQuestionAnswered(IEnumerable<int> all, IReadOnlySet<int> answered) =>
        typeof(QuizAttemptService)
            .GetMethod("EnsureEveryQuestionAnswered", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [all, answered]);

    [Fact]
    public void EveryQuestionAnswered_Passes()
        => EnsureEveryQuestionAnswered([101, 102, 104], new HashSet<int> { 101, 102, 104 });

    [Fact]
    public void AnyUnansweredQuestion_IsRejected_ListingThemAll()
    {
        // 102 is an MCQ, 104 an essay: both must be answered.
        var ex = Assert.Throws<TargetInvocationException>(() =>
            EnsureEveryQuestionAnswered([101, 102, 104], new HashSet<int> { 101 }));

        var argument = Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("102, 104", argument.Message);
    }
}

public class AiSettingsDefaultsTests
{
    [Fact]
    public void Defaults_AreConservative()
    {
        var settings = new AssessmentSettings();

        Assert.False(settings.AiSendImageContent);          // no image bytes unless a vision model is set up
        Assert.Equal(400, settings.EffectiveMaxHintLength);
        Assert.Equal(0.80m, settings.EffectiveEssayAutoAcceptConfidence);
        Assert.Equal(5, settings.EffectiveEssayEvaluationMaxAttempts);
    }

    [Fact]
    public void TheAcceptThreshold_CannotBeConfiguredBelowAHalf()
        => Assert.Equal(0.5m, new AssessmentSettings { EssayAutoAcceptConfidence = 0.1m }.EffectiveEssayAutoAcceptConfidence);
}
