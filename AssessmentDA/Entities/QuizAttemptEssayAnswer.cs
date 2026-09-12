using System;
using System.Collections.Generic;

namespace AssessmentDA.Entities;

/// <summary>
/// A child's free-text answer to an Essay question. Separate from
/// QuizAttemptMistake because it has a grading lifecycle and does not mean
/// "this answer was wrong", and separate from QuizAttemptQuestion because that
/// row is written once at attempt start and never modified.
/// </summary>
public partial class QuizAttemptEssayAnswer
{
    public long Id { get; set; }

    public long QuizAttemptId { get; set; }

    public int QuestionId { get; set; }

    public string AnswerText { get; set; } = null!;

    /// <summary>Pending | Graded | Skipped.</summary>
    public string Status { get; set; } = null!;

    public byte? AwardedPoints { get; set; }

    public string? Feedback { get; set; }

    /// <summary>Human | Ai. Null until graded.</summary>
    public string? GradedBy { get; set; }

    public DateTime? GradedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>The language the child answered in; AI feedback is written in it.</summary>
    public string LanguageCode { get; set; } = null!;

    /// <summary>
    /// The question's Points when the answer was submitted — the grade's
    /// ceiling (CK_QuizAttemptEssayAnswers_AwardedWithinMax). Frozen so a later
    /// edit of the question cannot put a grade above the maximum shown.
    /// </summary>
    public byte MaxPoints { get; set; }

    // ---- AI evaluation: the proposal and its processing state. The grade the
    // ---- child sees is Status / AwardedPoints / Feedback above — only a
    // ---- proposal the backend accepted is copied there.

    /// <summary>Accepted | NeedsReview | Failed. Null until the AI has been decided on.</summary>
    public string? AiOutcome { get; set; }

    public byte AiEvaluationAttempts { get; set; }

    public DateTime? AiLastAttemptAt { get; set; }

    public byte? AiProposedPoints { get; set; }

    public decimal? AiConfidence { get; set; }

    public string? AiFeedback { get; set; }

    /// <summary>
    /// Which evaluation run owns the answer right now. Set atomically when a run
    /// claims it, so two app instances never evaluate the same answer at once.
    /// </summary>
    public Guid? AiClaimId { get; set; }

    public virtual QuizAttempt QuizAttempt { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}
