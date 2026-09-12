namespace AssessmentBL.DTOs.QuizAttempt
{
    /// <summary>
    /// The state of one essay answer, as the child may see it. Only a grade the
    /// backend accepted is shown — an AI proposal waiting for review is not.
    /// </summary>
    public class EssayResultDto
    {
        public int QuestionId { get; set; }

        /// <summary>
        /// Graded | Pending. Pending = waiting for the AI, or for a person to
        /// review. Ask again later (GET /api/quiz-attempts/{id}/result).
        /// </summary>
        public string Status { get; set; } = null!;

        /// <summary>Only when Graded.</summary>
        public byte? AwardedPoints { get; set; }

        public byte MaxPoints { get; set; }

        /// <summary>Only when Graded.</summary>
        public string? Feedback { get; set; }

        /// <summary>"Ai" | "Human", only when Graded.</summary>
        public string? GradedBy { get; set; }
    }
}
