using System.Collections.Generic;

namespace AssessmentBL.DTOs.QuizAttempt
{
    public class SubmitQuizAttemptDto
    {
        public List<QuizAttemptMistakeDto> Mistakes { get; set; } = new();
    }
}