using System.Globalization;
using AssessmentBL.Interfaces;
using AssessmentBL.Services.Constants;
using AssessmentDA.Context;
using AssessmentDA.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Assessment.AI;
using Shared.Common.Abstractions;

namespace AssessmentBL.Services
{
    /// <summary>
    /// AI evaluation of essay answers. The AI sees the question and the child's
    /// answer and PROPOSES a score and feedback; the backend decides
    /// (<see cref="Decide"/>) whether that proposal becomes the grade or waits for
    /// a person.
    ///
    /// Runs twice over, never on the path to the commit: inline right after a
    /// submission commits (inside that submission's AI budget) so the child
    /// usually gets feedback at once, and from EssayEvaluationWorker for anything
    /// that did not finish. Every run first CLAIMS its answers in one UPDATE, so
    /// two app instances never evaluate the same answer; one AI request carries
    /// one attempt — one child — so a child's text can never influence another
    /// child's evaluation.
    /// </summary>
    public sealed class EssayEvaluationService : IEssayEvaluationService
    {
        private const int BatchSize = 20;

        private readonly AssessmentDbContext _db;
        private readonly IAiEssayEvaluator _evaluator;
        private readonly AiRequestBuilder _aiRequests;
        private readonly IDateTimeProvider _clock;
        private readonly AssessmentSettings _settings;
        private readonly ILogger<EssayEvaluationService> _logger;

        public EssayEvaluationService(
            AssessmentDbContext db,
            IAiEssayEvaluator evaluator,
            AiRequestBuilder aiRequests,
            IDateTimeProvider clock,
            IOptions<AssessmentSettings> settings,
            ILogger<EssayEvaluationService> logger)
        {
            _db = db;
            _evaluator = evaluator;
            _aiRequests = aiRequests;
            _clock = clock;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task EvaluateAttemptAsync(long attemptId, CancellationToken cancellationToken)
        {
            // Not configured: nothing is attempted, so nothing is counted against
            // the essay — it is evaluated once the AI is set up. Budget already
            // spent (slow hints): leave it to the background run.
            if (!_evaluator.IsConfigured || cancellationToken.IsCancellationRequested)
                return;

            var candidateIds = await _db.QuizAttemptEssayAnswers
                .AsNoTracking()
                .Where(e => e.QuizAttemptId == attemptId
                         && e.Status == EssayAnswerStatuses.Pending
                         && e.AiOutcome == null)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count > 0)
                await ClaimAndEvaluateAsync(candidateIds, cancellationToken);
        }

        public async Task<int> EvaluateDueAsync(CancellationToken cancellationToken)
        {
            if (!_evaluator.IsConfigured)
                return 0;

            var now = _clock.UtcNow;
            var maxAttempts = _settings.EffectiveEssayEvaluationMaxAttempts;
            var settledBefore = now - _settings.EssayInlineGrace;
            var retryMinutes = (int)_settings.EssayEvaluationRetryDelay.TotalMinutes;

            // Answers that used up their attempts under an older, higher limit would
            // otherwise sit unclaimed forever: close them.
            await _db.QuizAttemptEssayAnswers
                .Where(e => e.Status == EssayAnswerStatuses.Pending
                         && e.AiOutcome == null
                         && e.AiEvaluationAttempts >= maxAttempts)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.AiOutcome, EssayAiOutcomes.Failed), cancellationToken);

            // Served by IX_QuizAttemptEssayAnswers_AiDue. The retry wait grows with
            // each attempt: 10, 20, 30… minutes.
            var candidateIds = await _db.QuizAttemptEssayAnswers
                .AsNoTracking()
                .Where(e => e.Status == EssayAnswerStatuses.Pending
                         && e.AiOutcome == null
                         && e.AiEvaluationAttempts < maxAttempts
                         && e.CreatedAt <= settledBefore
                         && (e.AiLastAttemptAt == null
                             || e.AiLastAttemptAt.Value.AddMinutes(retryMinutes * e.AiEvaluationAttempts) <= now))
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
                return 0;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_settings.EssayEvaluationTimeout);

            return await ClaimAndEvaluateAsync(candidateIds, timeout.Token);
        }

        private async Task<int> ClaimAndEvaluateAsync(List<long> candidateIds, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return 0;

            var now = _clock.UtcNow;
            var claimId = Guid.NewGuid();
            var maxAttempts = _settings.EffectiveEssayEvaluationMaxAttempts;
            var retryMinutes = (int)_settings.EssayEvaluationRetryDelay.TotalMinutes;

            // One atomic UPDATE: an answer another run claimed a moment ago no
            // longer matches the WHERE and stays theirs. The attempt is counted
            // here; ReleaseAsync hands it back if we run out of time first.
            var claimed = await _db.QuizAttemptEssayAnswers
                .Where(e => candidateIds.Contains(e.Id)
                         && e.Status == EssayAnswerStatuses.Pending
                         && e.AiOutcome == null
                         && e.AiEvaluationAttempts < maxAttempts
                         && (e.AiLastAttemptAt == null
                             || e.AiLastAttemptAt.Value.AddMinutes(retryMinutes * e.AiEvaluationAttempts) <= now))
                .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.AiClaimId, (Guid?)claimId)
                        .SetProperty(e => e.AiLastAttemptAt, (DateTime?)now)
                        .SetProperty(e => e.AiEvaluationAttempts, e => (byte)(e.AiEvaluationAttempts + 1)),
                    CancellationToken.None);

            if (claimed == 0)
                return 0;

            var essays = await _db.QuizAttemptEssayAnswers
                .Where(e => candidateIds.Contains(e.Id) && e.AiClaimId == claimId)
                .ToListAsync(CancellationToken.None);

            // One request per attempt: one child's answers, in the language they
            // answered in.
            var byAttempt = essays
                .GroupBy(e => e.QuizAttemptId)
                .Select(g => g.ToList())
                .ToList();

            var decided = 0;

            for (var i = 0; i < byAttempt.Count; i++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    decided += await EvaluateAttemptEssaysAsync(byAttempt[i], cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Out of time before the AI answered — not the AI's failure.
                    // Hand the unfinished answers back, attempt uncounted.
                    _db.ChangeTracker.Clear();

                    await ReleaseAsync(
                        byAttempt.Skip(i).SelectMany(g => g).Select(e => e.Id).ToList(), claimId);
                    break;
                }
                catch (Exception ex)
                {
                    // A database hiccup while preparing or saving — not the AI's
                    // failure either. Hand this attempt's answers back and carry on
                    // with the others; only this group's entities are dropped.
                    foreach (var essay in byAttempt[i])
                        _db.Entry(essay).State = EntityState.Detached;

                    _logger.LogWarning(ex,
                        "Essay evaluation for attempt {AttemptId} could not complete; its answers are released for the next run.",
                        byAttempt[i][0].QuizAttemptId);

                    await ReleaseAsync(byAttempt[i].Select(e => e.Id).ToList(), claimId);
                }
            }

            return decided;
        }

        private async Task<int> EvaluateAttemptEssaysAsync(
            List<QuizAttemptEssayAnswer> essays,
            CancellationToken cancellationToken)
        {
            var language = essays[0].LanguageCode;
            var questionIds = essays.Select(e => e.QuestionId).Distinct().ToList();

            var rows = (await LocalizedQuestionQuery.Project(
                        _db.Questions.AsNoTracking().Where(q => questionIds.Contains(q.Id)), language)
                    .ToListAsync(cancellationToken))
                .ToDictionary(r => r.QuestionId);

            var topics = await _aiRequests.LoadTopicNamesAsync(questionIds, language, cancellationToken);
            var imageBudget = _aiRequests.NewImageBudget();
            var now = _clock.UtcNow;
            var decided = 0;

            var items = new List<EssayRequestItem>(essays.Count);
            var awaiting = new Dictionary<string, QuizAttemptEssayAnswer>();

            foreach (var essay in essays)
            {
                if (!rows.TryGetValue(essay.QuestionId, out var row)
                    || AiRequestBuilder.QuestionSemanticText(row.QuestionText, row.ImageDescription) is null)
                {
                    // Without the question the AI has nothing to judge the answer
                    // against; retrying cannot help, so a person grades it.
                    Apply(essay, EssayDecision.NeedsReview(null, null, null), now);
                    decided++;
                    continue;
                }

                // Opaque per-request key — never a database id.
                var itemId = (items.Count + 1).ToString(CultureInfo.InvariantCulture);

                items.Add(new EssayRequestItem
                {
                    ItemId = itemId,
                    Difficulty = row.Difficulty,
                    Topic = topics.GetValueOrDefault(essay.QuestionId),
                    Question = new AiQuestion
                    {
                        Text = AiRequestBuilder.Clean(row.QuestionText),
                        Image = await _aiRequests.BuildImageAsync(
                            row.ImageUrl, row.ImageDescription, imageBudget, cancellationToken)
                    },
                    MaxPoints = essay.MaxPoints,
                    StudentAnswer = new EssayStudentAnswer { Text = essay.AnswerText }
                });

                awaiting[itemId] = essay;
            }

            if (awaiting.Count > 0)
            {
                EssayEvaluationResponse? response = null;

                try
                {
                    response = await _evaluator.EvaluateAsync(
                        new EssayEvaluationRequest { RequestId = Guid.NewGuid(), Language = language, Items = items },
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;   // our deadline, not the AI's failure — the caller releases the claim
                }
                catch (Exception ex)
                {
                    // Down, unreachable, HTTP error, malformed — retried later.
                    _logger.LogWarning(ex,
                        "AI essay evaluation failed for {Count} answer(s); they stay Pending and will be retried.",
                        awaiting.Count);
                }

                var resultsByItem = (response?.Results ?? Array.Empty<EssayEvaluationResult>())
                    .Where(r => r?.ItemId is not null)
                    .GroupBy(r => r.ItemId!)
                    .Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.Single());

                foreach (var (itemId, essay) in awaiting)
                {
                    var decision = response is null
                        ? EssayDecision.Unusable
                        : Decide(
                            resultsByItem.GetValueOrDefault(itemId),
                            essay.MaxPoints,
                            _settings.EffectiveEssayAutoAcceptConfidence,
                            _settings.EffectiveMaxEssayFeedbackLength);

                    Apply(essay, decision, now);

                    if (decision.Kind != EssayDecisionKind.Unusable)
                        decided++;
                }
            }

            // The decision is the valuable part — saved even if the deadline passes
            // right after the AI answered.
            await _db.SaveChangesAsync(CancellationToken.None);

            return decided;
        }

        /// <summary>
        /// Un-counts the attempt and frees the claim. AiLastAttemptAt is kept, so
        /// the growing retry wait still applies (an answer back at 0 attempts is
        /// due at once: its wait is 0 × delay).
        /// </summary>
        private Task<int> ReleaseAsync(List<long> essayIds, Guid claimId) =>
            _db.QuizAttemptEssayAnswers
                .Where(e => essayIds.Contains(e.Id) && e.AiClaimId == claimId && e.AiOutcome == null)
                .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.AiClaimId, (Guid?)null)
                        .SetProperty(e => e.AiEvaluationAttempts, e => (byte)(e.AiEvaluationAttempts - 1)),
                    CancellationToken.None);

        /// <summary>
        /// The backend's acceptance rule for an AI proposal. Accepted only when the
        /// AI answered Ok, the score is within 0…maxPoints, the feedback is usable,
        /// the confidence reaches the threshold, and nothing was flagged. A
        /// well-formed but unsure or flagged proposal, or an explicit Skipped, goes
        /// to a person (NeedsReview); anything missing, malformed or with an unknown
        /// status is retried (Unusable).
        /// </summary>
        public static EssayDecision Decide(
            EssayEvaluationResult? result,
            int maxPoints,
            decimal acceptConfidence,
            int maxFeedbackLength)
        {
            if (result is null)
                return EssayDecision.Unusable;

            var status = result.Status ?? AiResultStatuses.Ok;

            if (string.Equals(status, AiResultStatuses.Skipped, StringComparison.OrdinalIgnoreCase))
                return EssayDecision.NeedsReview(null, null, null);

            if (!string.Equals(status, AiResultStatuses.Ok, StringComparison.OrdinalIgnoreCase))
                return EssayDecision.Unusable;

            if (result.ProposedPoints is not int points || points < 0 || points > maxPoints)
                return EssayDecision.Unusable;

            var feedback = result.Feedback?.Trim();
            if (string.IsNullOrEmpty(feedback) || feedback.Length > maxFeedbackLength)
                return EssayDecision.Unusable;

            if (result.Confidence is not decimal confidence || confidence < 0m || confidence > 1m)
                return EssayDecision.Unusable;

            var flagged = result.Flags?.Any(f => !string.IsNullOrWhiteSpace(f)) ?? false;

            return flagged || confidence < acceptConfidence
                ? EssayDecision.NeedsReview(points, feedback, confidence)
                : EssayDecision.Accept(points, feedback, confidence);
        }

        /// <summary>The attempt was already counted when the answer was claimed.</summary>
        private void Apply(QuizAttemptEssayAnswer essay, EssayDecision decision, DateTime now)
        {
            switch (decision.Kind)
            {
                case EssayDecisionKind.Accept:
                    RecordProposal(essay, decision);
                    essay.AiOutcome = EssayAiOutcomes.Accepted;

                    // Only here does a proposal become the grade the child sees.
                    essay.Status = EssayAnswerStatuses.Graded;
                    essay.AwardedPoints = (byte)decision.Points!.Value;
                    essay.Feedback = decision.Feedback;
                    essay.GradedBy = EssayGraders.Ai;
                    essay.GradedAt = now;
                    break;

                case EssayDecisionKind.NeedsReview:
                    // Stays Pending; the proposal is kept for the reviewer.
                    RecordProposal(essay, decision);
                    essay.AiOutcome = EssayAiOutcomes.NeedsReview;
                    break;

                default:
                    if (essay.AiEvaluationAttempts >= _settings.EffectiveEssayEvaluationMaxAttempts)
                        essay.AiOutcome = EssayAiOutcomes.Failed;
                    break;
            }
        }

        private static void RecordProposal(QuizAttemptEssayAnswer essay, EssayDecision decision)
        {
            essay.AiProposedPoints = decision.Points is int points ? (byte)points : null;
            essay.AiFeedback = decision.Feedback;
            essay.AiConfidence = decision.Confidence is decimal confidence
                ? Math.Round(confidence, 2, MidpointRounding.AwayFromZero)
                : null;
        }
    }

    public enum EssayDecisionKind
    {
        Accept,
        NeedsReview,
        Unusable
    }

    public sealed record EssayDecision(EssayDecisionKind Kind, int? Points, string? Feedback, decimal? Confidence)
    {
        public static EssayDecision Unusable { get; } = new(EssayDecisionKind.Unusable, null, null, null);

        public static EssayDecision Accept(int points, string feedback, decimal confidence) =>
            new(EssayDecisionKind.Accept, points, feedback, confidence);

        public static EssayDecision NeedsReview(int? points, string? feedback, decimal? confidence) =>
            new(EssayDecisionKind.NeedsReview, points, feedback, confidence);
    }
}
