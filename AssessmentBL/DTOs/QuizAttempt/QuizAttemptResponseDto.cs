namespace AssessmentBL.DTOs.QuizAttempt
{
	public class QuizAttemptResponseDto
    {
		public long AttemptId { get; set; }

		public int QuizId { get; set; }

		public DateTime StartedAt { get; set; }

		public List<QuizQuestionForAttemptDto> Questions { get; set; } = new();
	}
}