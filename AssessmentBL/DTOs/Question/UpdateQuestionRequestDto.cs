namespace AssessmentBL.DTOs.Question
{
    public class UpdateQuestionDto
    {
        public int TopicId { get; set; }

        public string QuestionText { get; set; } = null!;

        public string Difficulty { get; set; } = null!;

        public short DisplayOrder { get; set; }

        public byte Points { get; set; }

        public bool IsActive { get; set; }
    }
}