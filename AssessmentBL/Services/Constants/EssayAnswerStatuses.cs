namespace AssessmentBL.Services.Constants
{
    /// <summary>Mirrors CK_QuizAttemptEssayAnswers_Status.</summary>
    public static class EssayAnswerStatuses
    {
        public const string Pending = "Pending";
        public const string Graded = "Graded";
        public const string Skipped = "Skipped";
    }

    /// <summary>Mirrors CK_QuizAttemptEssayAnswers_GradedBy.</summary>
    public static class EssayGraders
    {
        public const string Human = "Human";
        public const string Ai = "Ai";
    }

    /// <summary>
    /// What the backend decided about the AI's proposal for an essay. Mirrors
    /// CK_QuizAttemptEssayAnswers_AiOutcome.
    /// </summary>
    public static class EssayAiOutcomes
    {
        /// <summary>The proposal met the acceptance rules and became the grade.</summary>
        public const string Accepted = "Accepted";

        /// <summary>The AI answered but was unsure, flagged the answer, or declined — a person grades it.</summary>
        public const string NeedsReview = "NeedsReview";

        /// <summary>Every allowed attempt failed to produce a usable answer — a person grades it.</summary>
        public const string Failed = "Failed";
    }
}
