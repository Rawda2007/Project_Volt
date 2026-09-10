namespace AssessmentBL.DTOs.QuestionOption
{
    public class AdminQuestionOptionResponseDto
    {
        public int Id { get; set; }

        public string OptionText { get; set; } = null!;

        public bool IsCorrect { get; set; }

        public short DisplayOrder { get; set; }
    }
}