using AssessmentBL.DTOs.QuizAttempt;
using AssessmentBL.Interfaces;
using AssessmentBL.Services.Constants;
using AssessmentDA.Context;
using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Assessment.AI;
using Shared.Common.Abstractions;

namespace AssessmentBL.Services
{
    public class QuizAttemptService : IQuizAttemptService
    {
        private readonly AssessmentDbContext _db;
        private readonly IUserTopicStatService _userTopicStatService;
        private readonly IDateTimeProvider _clock;
        private readonly IAiHintGenerator _aiHintGenerator;

        public QuizAttemptService(
            AssessmentDbContext db,
            IUserTopicStatService userTopicStatService,
            IDateTimeProvider clock,
            IAiHintGenerator aiHintGenerator)
        {
            _db = db;
            _userTopicStatService = userTopicStatService;
            _clock = clock;
            _aiHintGenerator = aiHintGenerator;
        }

        public async Task<QuizAttemptResponseDto> StartAsync(
            int quizId,
            Guid userId,
            long? previousAttemptId = null,
            CancellationToken cancellationToken = default)
        {
            var quizIsActive = await _db.Quizzes
                .AsNoTracking()
                .Where(q => q.Id == quizId)
                .Select(q => (bool?)q.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (quizIsActive is null)
                throw new KeyNotFoundException($"الاختبار رقم {quizId} غير موجود");

            if (quizIsActive == false)
                throw new InvalidOperationException($"الاختبار رقم {quizId} غير مفعّل");

            return previousAttemptId is null
                ? await StartFirstAttemptAsync(quizId, userId, cancellationToken)
                : await StartRetryAttemptAsync(quizId, userId, previousAttemptId.Value, cancellationToken);
        }

        private async Task<QuizAttemptResponseDto> StartFirstAttemptAsync(
            int quizId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var questions = await ProjectQuestions(
                    _db.Questions.AsNoTracking().Where(q => q.QuizId == quizId && q.IsActive))
                .ToListAsync(cancellationToken);

            if (questions.Count == 0)
                throw new InvalidOperationException(
                    $"الاختبار رقم {quizId} لا يحتوي على أسئلة مفعّلة");

            return await PersistAttemptAsync(quizId, userId, previousAttemptId: null, questions, cancellationToken);
        }

        private async Task<QuizAttemptResponseDto> StartRetryAttemptAsync(
            int quizId,
            Guid userId,
            long previousAttemptId,
            CancellationToken cancellationToken)
        {
            var previousAttempt = await _db.QuizAttempts
                .AsNoTracking()
                .Where(a => a.Id == previousAttemptId)
                .Select(a => new { a.Id, a.UserId, a.QuizId, a.Status })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"المحاولة رقم {previousAttemptId} غير موجودة");

            if (previousAttempt.UserId != userId)
                throw new UnauthorizedAccessException("لا يمكنك إعادة محاولة مستخدم آخر");

            if (previousAttempt.QuizId != quizId)
                throw new InvalidOperationException(
                    $"المحاولة رقم {previousAttemptId} لا تخص الاختبار رقم {quizId}");

            if (previousAttempt.Status != QuizAttemptStatuses.Completed)
                throw new InvalidOperationException(
                    $"لا يمكن إعادة المحاولة رقم {previousAttemptId} لأنها لم تكتمل");

            var alreadyRetried = await _db.QuizAttempts
                .AsNoTracking()
                .AnyAsync(a => a.PreviousAttemptId == previousAttemptId, cancellationToken);

            if (alreadyRetried)
                throw new InvalidOperationException(
                    $"تمت إعادة المحاولة رقم {previousAttemptId} من قبل");

            var wrongQuestionIds = await _db.QuizAttemptMistakes
                .AsNoTracking()
                .Where(m => m.QuizAttemptId == previousAttemptId)
                .Select(m => m.QuestionId)
                .ToListAsync(cancellationToken);

            if (wrongQuestionIds.Count == 0)
                throw new InvalidOperationException(
                    $"المحاولة رقم {previousAttemptId} لا تحتوي على إجابات خاطئة لإعادتها");

            var questions = await ProjectQuestions(
                    _db.Questions.AsNoTracking().Where(q => wrongQuestionIds.Contains(q.Id)))
                .ToListAsync(cancellationToken);

            var latestHints = await GetLatestHintPerQuestionAsync(previousAttemptId, cancellationToken);

            foreach (var question in questions)
            {
                if (latestHints.TryGetValue(question.QuestionId, out var hintText))
                    question.CurrentHint = hintText;
            }

            return await PersistAttemptAsync(quizId, userId, previousAttemptId, questions, cancellationToken);
        }

        private async Task<QuizAttemptResponseDto> PersistAttemptAsync(
            int quizId,
            Guid userId,
            long? previousAttemptId,
            IReadOnlyList<QuizQuestionForAttemptDto> questions,
            CancellationToken cancellationToken)
        {
            var attempt = new QuizAttempt
            {
                QuizId = quizId,
                UserId = userId,
                PreviousAttemptId = previousAttemptId,
                TotalQuestionsAtAttempt = (short)questions.Count,
                QuestionsAnsweredCount = 0,
                CorrectAnswersCount = 0,
                ScorePercentage = 0m,
                Status = QuizAttemptStatuses.InProgress,
                StartedAt = _clock.UtcNow
            };

            foreach (var question in questions)
            {
                attempt.QuizAttemptQuestions.Add(new QuizAttemptQuestion
                {
                    QuestionId = question.QuestionId
                });
            }

            _db.QuizAttempts.Add(attempt);
            await _db.SaveChangesAsync(cancellationToken);

            return new QuizAttemptResponseDto
            {
                AttemptId = attempt.Id,
                QuizId = attempt.QuizId,
                StartedAt = attempt.StartedAt,
                Questions = questions.ToList()
            };
        }

        public async Task<QuizAttemptResultDto> SubmitAsync(
            long attemptId,
            Guid userId,
            SubmitQuizAttemptDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var attempt = await _db.QuizAttempts
                .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken)
                ?? throw new KeyNotFoundException($"المحاولة رقم {attemptId} غير موجودة");

            if (attempt.UserId != userId)
                throw new UnauthorizedAccessException("لا يمكنك تسليم محاولة مستخدم آخر");

            if (attempt.Status != QuizAttemptStatuses.InProgress)
                throw new InvalidOperationException(
                    $"المحاولة رقم {attemptId} تم تسليمها بالفعل");

            var attemptQuestionIds = await _db.QuizAttemptQuestions
                .AsNoTracking()
                .Where(aq => aq.QuizAttemptId == attemptId)
                .Select(aq => aq.QuestionId)
                .ToListAsync(cancellationToken);

            var attemptQuestionIdSet = attemptQuestionIds.ToHashSet();
            var submittedMistakes = ValidateSubmittedMistakes(dto, attemptQuestionIdSet);

            var selectedOptionIds = submittedMistakes.Select(m => m.SelectedOptionId).ToList();

            var selectedOptions = await _db.QuestionOptions
                .AsNoTracking()
                .Where(o => selectedOptionIds.Contains(o.Id))
                .Select(o => new { o.Id, o.QuestionId, o.IsCorrect })
                .ToListAsync(cancellationToken);

            var optionsById = selectedOptions.ToDictionary(o => o.Id);
            var confirmedMistakes = new List<QuizAttemptMistakeDto>(submittedMistakes.Count);

            foreach (var mistake in submittedMistakes)
            {
                if (!optionsById.TryGetValue(mistake.SelectedOptionId, out var option))
                    throw new ArgumentException(
                        $"الاختيار رقم {mistake.SelectedOptionId} غير موجود", nameof(dto));

                if (option.QuestionId != mistake.QuestionId)
                    throw new ArgumentException(
                        $"الاختيار رقم {mistake.SelectedOptionId} لا يخص السؤال رقم {mistake.QuestionId}",
                        nameof(dto));

                if (!option.IsCorrect)
                    confirmedMistakes.Add(mistake);
            }

            var totalQuestions = attempt.TotalQuestionsAtAttempt;
            var correctAnswers = (short)(totalQuestions - confirmedMistakes.Count);
            var completedAt = _clock.UtcNow;

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var mistake in confirmedMistakes)
            {
                _db.QuizAttemptMistakes.Add(new QuizAttemptMistake
                {
                    QuizAttemptId = attemptId,
                    QuestionId = mistake.QuestionId,
                    SelectedOptionId = mistake.SelectedOptionId
                });
            }

            attempt.QuestionsAnsweredCount = totalQuestions;
            attempt.CorrectAnswersCount = correctAnswers;
            attempt.ScorePercentage = CalculateScorePercentage(correctAnswers, totalQuestions);
            attempt.Status = QuizAttemptStatuses.Completed;
            attempt.CompletedAt = completedAt;

            await _db.SaveChangesAsync(cancellationToken);

            var retryQuestions = await GenerateAndPersistHintsAsync(
                attemptId,
                userId,
                confirmedMistakes,
                cancellationToken);

            await _userTopicStatService.UpdateAfterQuizAttemptAsync(attemptId, userId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new QuizAttemptResultDto
            {
                AttemptId = attempt.Id,
                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                WrongAnswers = (short)(totalQuestions - correctAnswers),
                ScorePercentage = attempt.ScorePercentage,
                RetryQuestions = retryQuestions
            };
        }

        private async Task<List<QuizQuestionForAttemptDto>> GenerateAndPersistHintsAsync(
            long attemptId,
            Guid userId,
            IReadOnlyList<QuizAttemptMistakeDto> mistakes,
            CancellationToken cancellationToken)
        {
            if (mistakes.Count == 0)
                return [];

            var questionIds = mistakes.Select(m => m.QuestionId).ToList();
            var selectedOptionIds = mistakes.Select(m => m.SelectedOptionId).ToList();
            var questions = await _db.Questions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => new
                {
                    q.Id,
                    q.QuestionText,
                    q.Difficulty,
                    q.DisplayOrder,
                    q.Points,
                    Options = q.QuestionOptions
                        .OrderBy(o => o.DisplayOrder)
                        .Select(o => new QuizAnswerOptionDto
                        {
                            OptionId = o.Id,
                            OptionText = o.OptionText,
                            DisplayOrder = o.DisplayOrder
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            var options = await _db.QuestionOptions
                .AsNoTracking()
                .Where(o => selectedOptionIds.Contains(o.Id))
                .Select(o => new { o.Id, o.OptionText })
                .ToDictionaryAsync(o => o.Id, cancellationToken);

            var previousHints = await _db.QuestionHints
                .AsNoTracking()
                .Where(h => questionIds.Contains(h.QuizAttemptMistake.QuestionId)
                         && h.QuizAttemptMistake.QuizAttempt.UserId == userId)
                .OrderBy(h => h.QuizAttemptMistake.QuizAttemptId)
                .ThenBy(h => h.HintSequence)
                .Select(h => new { h.QuizAttemptMistake.QuestionId, h.HintText })
                .ToListAsync(cancellationToken);

            var hintsByQuestion = previousHints
                .GroupBy(h => h.QuestionId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(h => h.HintText).ToList());
            var questionsById = questions.ToDictionary(q => q.Id);

            var request = new GenerateHintsRequest
            {
                Questions = mistakes.Select(m => new GenerateHintQuestion
                {
                    QuestionId = m.QuestionId,
                    QuestionText = questionsById[m.QuestionId].QuestionText,
                    WrongOptionText = options[m.SelectedOptionId].OptionText,
                    PreviousHints = hintsByQuestion.TryGetValue(m.QuestionId, out var history)
                        ? history
                        : []
                }).ToList()
            };

            var response = await _aiHintGenerator.GenerateHintsAsync(request, cancellationToken);
            var expectedIds = questionIds.ToHashSet();
            var generatedByQuestion = response.Hints
                .Where(h => expectedIds.Contains(h.QuestionId))
                .GroupBy(h => h.QuestionId)
                .ToDictionary(g => g.Key, g => g.Single().HintText.Trim());

            if (generatedByQuestion.Count != expectedIds.Count
                || generatedByQuestion.Values.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("The AI response did not contain one valid hint per wrong question");

            var currentMistakes = await _db.QuizAttemptMistakes
                .Where(m => m.QuizAttemptId == attemptId)
                .ToListAsync(cancellationToken);
            var nextSequences = await _db.QuestionHints
                .Where(h => currentMistakes.Select(m => m.Id).Contains(h.QuizAttemptMistakeId))
                .GroupBy(h => h.QuizAttemptMistakeId)
                .ToDictionaryAsync(g => g.Key, g => (byte)(g.Max(h => h.HintSequence) + 1), cancellationToken);

            var persistedHints = currentMistakes.Select(m => new QuestionHint
            {
                QuizAttemptMistakeId = m.Id,
                HintText = generatedByQuestion[m.QuestionId],
                HintSequence = nextSequences.TryGetValue(m.Id, out var sequence) ? sequence : (byte)1
            }).ToList();

            _db.QuestionHints.AddRange(persistedHints);
            await _db.SaveChangesAsync(cancellationToken);

            return questions
                .OrderBy(q => q.DisplayOrder)
                .Select(q => new QuizQuestionForAttemptDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    Difficulty = q.Difficulty,
                    DisplayOrder = q.DisplayOrder,
                    Points = q.Points,
                    CurrentHint = generatedByQuestion[q.Id],
                    Options = q.Options
                })
                .ToList();
        }

        public async Task<QuizAttemptResponseDto> GetByIdAsync(
            long attemptId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var attempt = await _db.QuizAttempts
                .AsNoTracking()
                .Where(a => a.Id == attemptId)
                .Select(a => new { a.Id, a.QuizId, a.UserId, a.StartedAt })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"المحاولة رقم {attemptId} غير موجودة");

            if (attempt.UserId != userId)
                throw new UnauthorizedAccessException("لا يمكنك الوصول إلى محاولة مستخدم آخر");

            var questions = await ProjectQuestions(
                    _db.QuizAttemptQuestions
                        .AsNoTracking()
                        .Where(aq => aq.QuizAttemptId == attemptId)
                        .Select(aq => aq.Question))
                .ToListAsync(cancellationToken);

            var latestHints = await GetLatestHintPerQuestionAsync(attemptId, cancellationToken);

            foreach (var question in questions)
            {
                if (latestHints.TryGetValue(question.QuestionId, out var hintText))
                    question.CurrentHint = hintText;
            }

            return new QuizAttemptResponseDto
            {
                AttemptId = attempt.Id,
                QuizId = attempt.QuizId,
                StartedAt = attempt.StartedAt,
                Questions = questions
            };
        }

        private static List<QuizAttemptMistakeDto> ValidateSubmittedMistakes(
            SubmitQuizAttemptDto dto,
            HashSet<int> attemptQuestionIds)
        {
            var mistakes = dto.Mistakes ?? new List<QuizAttemptMistakeDto>();
            var seenQuestionIds = new HashSet<int>(mistakes.Count);

            foreach (var mistake in mistakes)
            {
                if (mistake is null)
                    throw new ArgumentException("قائمة الأخطاء تحتوي على عنصر فارغ", nameof(dto));

                if (!seenQuestionIds.Add(mistake.QuestionId))
                    throw new ArgumentException(
                        $"السؤال رقم {mistake.QuestionId} مكرر في قائمة الأخطاء", nameof(dto));

                if (!attemptQuestionIds.Contains(mistake.QuestionId))
                    throw new ArgumentException(
                        $"السؤال رقم {mistake.QuestionId} لا يخص هذه المحاولة", nameof(dto));
            }

            return mistakes.ToList();
        }

        private static decimal CalculateScorePercentage(short correctAnswers, short totalQuestions)
        {
            if (totalQuestions <= 0)
                return 0m;

            return Math.Round(correctAnswers * 100m / totalQuestions, 2, MidpointRounding.AwayFromZero);
        }

        private async Task<Dictionary<int, string>> GetLatestHintPerQuestionAsync(
            long quizAttemptId,
            CancellationToken cancellationToken)
        {
            var hints = await _db.QuestionHints
                .AsNoTracking()
                .Where(h => h.QuizAttemptMistake.QuizAttemptId == quizAttemptId)
                .Select(h => new
                {
                    h.QuizAttemptMistake.QuestionId,
                    h.HintSequence,
                    h.HintText
                })
                .ToListAsync(cancellationToken);

            return hints
                .GroupBy(h => h.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(h => h.HintSequence).First().HintText);
        }

        private static IQueryable<QuizQuestionForAttemptDto> ProjectQuestions(IQueryable<Question> questions) =>
            questions
                .OrderBy(q => q.DisplayOrder)
                .Select(q => new QuizQuestionForAttemptDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    Difficulty = q.Difficulty,
                    DisplayOrder = q.DisplayOrder,
                    Points = q.Points,
                    CurrentHint = null,
                    Options = q.QuestionOptions
                        .OrderBy(o => o.DisplayOrder)
                        .Select(o => new QuizAnswerOptionDto
                        {
                            OptionId = o.Id,
                            OptionText = o.OptionText,
                            DisplayOrder = o.DisplayOrder,
                        })
                        .ToList()
                });
    }
}
