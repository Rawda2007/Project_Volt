namespace AssessmentBL.DTOs.QuizAttempt
{
    /// <summary>One press of the Hint button.</summary>
    public class HintResponseDto
    {
        public int QuestionId { get; set; }

        /// <summary>
        /// Which press this was: 1 = a soft nudge, 2 = a more direct hint. A third
        /// press is refused with 409.
        /// </summary>
        public byte AttemptNumber { get; set; }

        /// <summary>Null when no hint could be produced — see HintsStatus.</summary>
        public string? Hint { get; set; }

        /// <summary>
        /// Generated — the hint is here. Partial — the AI answered but the hint
        /// gave the answer away or was unusable, so nothing was saved and this
        /// press did not use up a level. Unavailable — the AI could not be reached.
        /// </summary>
        public string HintsStatus { get; set; } = null!;

        public string Language { get; set; } = null!;
    }
}
