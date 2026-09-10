namespace AssessmentBL.DTOs.QuizAttempt
{
    public class QuizQuestionForAttemptDto
    {
        public int QuestionId { get; set; }

        public string QuestionText { get; set; } = null!;

        public string Difficulty { get; set; } = null!;

        public short DisplayOrder { get; set; }

        public byte Points { get; set; }
        public string? CurrentHint { get; set; }

        public IReadOnlyList<QuizAnswerOptionDto> Options { get; set; }
            = [];
    }
}