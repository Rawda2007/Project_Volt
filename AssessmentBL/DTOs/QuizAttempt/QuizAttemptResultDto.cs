using AssessmentBL.DTOs.Placement;

namespace AssessmentBL.DTOs.QuizAttempt
{
    public class QuizAttemptResultDto
    {
        public long AttemptId { get; set; }

        /// <summary>Lets a client that recovered this result start a retry
        /// (POST /api/quiz-attempts?quizId=…&amp;previousAttemptId=…).</summary>
        public int QuizId { get; set; }

        /// <summary>When the result was saved (UTC).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Every question in the attempt, essays included.</summary>
        public short TotalQuestions { get; set; }

        /// <summary>
        /// Questions the backend could score by itself — total minus essays.
        /// This is the denominator of ScorePercentage.
        /// </summary>
        public short AutoGradedQuestions { get; set; }

        /// <summary>
        /// Essay answers not graded yet (waiting for the AI or a person). Essays are
        /// never part of ScorePercentage; their grades are in EssayResults.
        /// </summary>
        public short PendingEssayQuestions { get; set; }

        /// <summary>Every essay answer of the attempt, in display order.</summary>
        public List<EssayResultDto> EssayResults { get; set; } = new();

        public short CorrectAnswers { get; set; }

        public short WrongAnswers { get; set; }

        /// <summary>correct / AutoGradedQuestions, 2dp. 0 when nothing was auto-graded.</summary>
        public decimal ScorePercentage { get; set; }

        public string Language { get; set; } = null!;

        public bool LanguageFallbackApplied { get; set; }

        /// <summary>
        /// NotRequired | Generated | Partial | Unavailable. Whether the retry
        /// questions carry AI hints. Hints are optional — the score above is final
        /// whatever this says. When not Generated, CurrentHint is null on the
        /// retry questions that have no hint.
        /// </summary>
        public string HintsStatus { get; set; } = null!;

        public List<QuizQuestionForAttemptDto> RetryQuestions { get; set; } = new();

        /// <summary>
        /// Only for a placement attempt: the level the learner was placed at.
        /// Null for every other quiz. A placement attempt has no retry questions
        /// and no hints — it is not retried.
        /// </summary>
        public PlacementResultDto? Placement { get; set; }
    }
}
