
namespace AssessmentBL.DTOs.QuestionOption
{
        public class CreateQuestionOptionDto
        {
            public int QuestionId { get; set; }

            public string OptionText { get; set; } = null!;

            public bool IsCorrect { get; set; }

            public short DisplayOrder { get; set; }
        }
    }
