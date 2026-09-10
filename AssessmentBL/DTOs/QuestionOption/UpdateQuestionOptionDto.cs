namespace AssessmentBL.DTOs.QuestionOption
{
    public class UpdateQuestionOptionDto
    {
        public string OptionText { get; set; } = null!;

        public bool IsCorrect { get; set; }

        public short DisplayOrder { get; set; }
    }
}