namespace AssessmentBL.DTOs.QuizAttempt
{
    public class QuizAnswerOptionDto
    {
        public int OptionId { get; set; }

        public string OptionText { get; set; } = null!;

        public short DisplayOrder { get; set; }

    }
}
