using AssessmentBL.DTOs.UserTopicStat;
namespace AssessmentBL.Interfaces
{
    public interface IUserTopicStatService
    {
        Task<IReadOnlyList<UserTopicStatResponseDto>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<UserTopicStatResponseDto?> GetByTopicAndDifficultyAsync(
            Guid userId,
            int topicId,
            string difficulty,
            CancellationToken cancellationToken = default);

        Task UpdateAfterQuizAttemptAsync(
            long quizAttemptId,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Recomputes HintsUsedCount for the (topic, difficulty) buckets of one
        /// attempt from the hints saved so far. Idempotent.
        /// </summary>
        Task RefreshHintsUsedCountAsync(
            long quizAttemptId,
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}