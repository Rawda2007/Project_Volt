namespace AssessmentBL.DTOs.QuizAttempt
{
    public class QuizAttemptResultDto
    {
        public long AttemptId { get; set; }
        public short TotalQuestions { get; set; }
        public short CorrectAnswers { get; set; }
        public short WrongAnswers { get; set; }
        public decimal ScorePercentage { get; set; }
        public List<QuizQuestionForAttemptDto> RetryQuestions { get; set; } = new();
    }
}