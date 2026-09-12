using AssessmentBL.DTOs.UserTopicStat;
using AssessmentBL.Interfaces;
using AssessmentDA.Context;
using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Abstractions;
using System.Linq.Expressions;
using AssessmentBL.Services.Constants;

namespace AssessmentBL.Services
{
    public class UserTopicStatService : IUserTopicStatService
    {
        private static readonly Expression<Func<UserTopicStat, UserTopicStatResponseDto>> ProjectToResponse =
            stat => new UserTopicStatResponseDto
            {
                Id = stat.Id,
                UserId = stat.UserId,
                TopicId = stat.TopicId,
                Difficulty = stat.Difficulty,
                QuestionsAnsweredCount = stat.QuestionsAnsweredCount,
                CorrectCount = stat.CorrectCount,
                // Computed persisted column; the CLR property is nullable only
                // because the scaffolder made it so.
                WrongCount = stat.WrongCount ?? 0,
                HintsUsedCount = stat.HintsUsedCount,
                LastQuizAttemptId = stat.LastQuizAttemptId,
                LastPracticedAt = stat.LastPracticedAt,
                UpdatedAt = stat.UpdatedAt
            };

        private readonly AssessmentDbContext _db;
        private readonly IDateTimeProvider _clock;

        public UserTopicStatService(AssessmentDbContext db, IDateTimeProvider clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<IReadOnlyList<UserTopicStatResponseDto>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _db.UserTopicStats
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.TopicId)
                .ThenBy(s => s.Difficulty)
                .Select(ProjectToResponse)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserTopicStatResponseDto?> GetByTopicAndDifficultyAsync(
            Guid userId,
            int topicId,
            string difficulty,
            CancellationToken cancellationToken = default)
        {
            // Hits UQ_UserTopicStats_UserId_TopicId_Difficulty directly.
            return await _db.UserTopicStats
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.TopicId == topicId && s.Difficulty == difficulty)
                .Select(ProjectToResponse)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Folds one completed attempt into the (UserId, TopicId, Difficulty)
        /// aggregates. Counts come from that attempt's own QuizAttemptQuestion
        /// rows, so a retry contributes only the questions it actually
        /// contained. WrongCount is never assigned — SQL Server computes it.
        ///
        /// A fixed number of round trips whatever the attempt size: every read
        /// happens before the bucket loop, and the loop itself is in-memory only.
        /// </summary>
        public async Task UpdateAfterQuizAttemptAsync(
            long quizAttemptId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var attempt = await _db.QuizAttempts
                .AsNoTracking()
                .Where(a => a.Id == quizAttemptId)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.Status,
                    a.CompletedAt
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"المحاولة رقم {quizAttemptId} غير موجودة");

            if (attempt.UserId != userId)
                throw new UnauthorizedAccessException("لا يمكنك الوصول إلى محاولة مستخدم آخر");

            if (attempt.Status != QuizAttemptStatuses.Completed)
                throw new InvalidOperationException(
                    $"المحاولة رقم {quizAttemptId} لم تكتمل بعد");

            // The authoritative set of questions this attempt contained, with
            // the classification frozen at attempt start. Reading TopicId /
            // Difficulty from the live Question row here would re-attribute
            // this attempt's counters if an admin later re-classified it.
            //
            // Essay answers are not auto-graded, so counting them as "answered"
            // with no possible "correct" would permanently depress the child's
            // mastery for that topic. They are excluded until a grader scores them.
            var attemptQuestions = await _db.QuizAttemptQuestions
                .AsNoTracking()
                .Where(aq => aq.QuizAttemptId == quizAttemptId && aq.QuestionType != QuestionTypes.Essay)
                .Select(aq => new
                {
                    aq.QuestionId,
                    aq.TopicId,
                    aq.Difficulty
                })
                .ToListAsync(cancellationToken);

            if (attemptQuestions.Count == 0)
                return;

            // Every question this attempt got wrong, fetched ONCE. This replaces a
            // per-question _db.QuizAttemptMistakes.Any(...) that ran synchronously
            // inside the grouping below — one blocking query per question.
            var wrongQuestionIds = (await _db.QuizAttemptMistakes
                    .AsNoTracking()
                    .Where(m => m.QuizAttemptId == quizAttemptId)
                    .Select(m => m.QuestionId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var topicIds = attemptQuestions.Select(q => q.TopicId).Distinct().ToList();

            var hintCounts = await CountHintsByBucketAsync(userId, topicIds, cancellationToken);

            var buckets = attemptQuestions
                .GroupBy(q => (q.TopicId, q.Difficulty))
                .Select(g => new
                {
                    g.Key.TopicId,
                    g.Key.Difficulty,
                    Answered = g.Count(),
                    Correct = g.Count(q => !wrongQuestionIds.Contains(q.QuestionId)),
                    Hints = hintCounts.GetValueOrDefault(g.Key)
                })
                .ToList();

            // One tracked read for every row we might touch — no per-bucket query.
            var existingStats = await _db.UserTopicStats
                .Where(s => s.UserId == userId && topicIds.Contains(s.TopicId))
                .ToDictionaryAsync(s => (s.TopicId, s.Difficulty), cancellationToken);

            // CK_QuizAttempts_CompletedRequiresAllAnswered guarantees a
            // completed attempt has CompletedAt, so the fallback is belt-and-braces.
            var practisedAt = attempt.CompletedAt ?? _clock.UtcNow;

            foreach (var bucket in buckets)
            {
                if (!existingStats.TryGetValue((bucket.TopicId, bucket.Difficulty), out var stat))
                {
                    stat = new UserTopicStat
                    {
                        UserId = userId,
                        TopicId = bucket.TopicId,
                        Difficulty = bucket.Difficulty,
                        QuestionsAnsweredCount = 0,
                        CorrectCount = 0,
                        HintsUsedCount = 0
                    };

                    _db.UserTopicStats.Add(stat);
                }

                stat.QuestionsAnsweredCount += bucket.Answered;
                stat.CorrectCount += bucket.Correct;
                stat.HintsUsedCount = bucket.Hints;
                stat.LastQuizAttemptId = quizAttemptId;
                stat.LastPracticedAt = practisedAt;
                stat.UpdatedAt = practisedAt;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Re-derives HintsUsedCount for the buckets an attempt touched. Needed
        /// because hints are now saved AFTER the submission commits. Idempotent:
        /// the value is recomputed from saved hints, never accumulated.
        /// </summary>
        public async Task RefreshHintsUsedCountAsync(
            long quizAttemptId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var buckets = await _db.QuizAttemptQuestions
                .AsNoTracking()
                .Where(aq => aq.QuizAttemptId == quizAttemptId
                          && aq.QuizAttempt.UserId == userId
                          && aq.QuestionType != QuestionTypes.Essay)
                .Select(aq => new { aq.TopicId, aq.Difficulty })
                .Distinct()
                .ToListAsync(cancellationToken);

            if (buckets.Count == 0)
                return;

            var topicIds = buckets.Select(b => b.TopicId).Distinct().ToList();
            var touched = buckets.Select(b => (b.TopicId, b.Difficulty)).ToHashSet();

            var hintCounts = await CountHintsByBucketAsync(userId, topicIds, cancellationToken);

            var stats = await _db.UserTopicStats
                .Where(s => s.UserId == userId && topicIds.Contains(s.TopicId))
                .ToListAsync(cancellationToken);

            foreach (var stat in stats)
            {
                if (touched.Contains((stat.TopicId, stat.Difficulty)))
                    stat.HintsUsedCount = hintCounts.GetValueOrDefault((stat.TopicId, stat.Difficulty));
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Hints the user has received, per (topic, difficulty) of the question
        /// they were for — limited to the topics being written. It used to
        /// aggregate every hint the child had ever received, across all topics, on
        /// every submission.
        /// </summary>
        private async Task<Dictionary<(int TopicId, string Difficulty), int>> CountHintsByBucketAsync(
            Guid userId,
            List<int> topicIds,
            CancellationToken cancellationToken)
        {
            // Through the hint's own (attempt, question) link, so Hint-button hints
            // — which have no mistake row — are counted too.
            var rows = await _db.QuestionHints
                .AsNoTracking()
                .Where(h => h.QuizAttemptQuestion.QuizAttempt.UserId == userId
                         && topicIds.Contains(h.QuizAttemptQuestion.TopicId))
                .GroupBy(h => new
                {
                    h.QuizAttemptQuestion.TopicId,
                    h.QuizAttemptQuestion.Difficulty
                })
                .Select(g => new { g.Key.TopicId, g.Key.Difficulty, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(x => (x.TopicId, x.Difficulty), x => x.Count);
        }
    }
}
