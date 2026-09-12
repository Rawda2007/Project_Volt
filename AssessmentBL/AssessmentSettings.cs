namespace AssessmentBL
{
    /// <summary>
    /// Assessment module settings, bound from the "Assessment" configuration
    /// section. Every value has a safe default, so the section is optional.
    /// </summary>
    public sealed class AssessmentSettings
    {
        public const string SectionName = "Assessment";

        /// <summary>
        /// How long an attempt may stay InProgress after StartedAt. Once this has
        /// elapsed the attempt is Abandoned: it can no longer be submitted, and the
        /// sweep persists that status. Generous on purpose — a quiz takes minutes,
        /// so this only ever catches attempts the child walked away from.
        /// </summary>
        public int InProgressAttemptTimeoutMinutes { get; set; } = 180;

        /// <summary>How often the abandoned-attempt sweep runs.</summary>
        public int AbandonedAttemptSweepIntervalMinutes { get; set; } = 15;

        /// <summary>
        /// Upper bound on ALL optional AI work of a submission — hints first, then
        /// inline essay evaluation with whatever is left. The result is already
        /// committed when it starts, so this only bounds how long the child waits;
        /// essays not evaluated in time are picked up by the background evaluator.
        /// </summary>
        public int AiHintTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Send image bytes (base64) alongside image descriptions. Turn on only for
        /// a vision-capable model — otherwise it is payload the model ignores.
        /// </summary>
        public bool AiSendImageContent { get; set; } = false;

        /// <summary>Largest image sent to the AI; bigger ones go as description only.</summary>
        public int AiMaxImageBytes { get; set; } = 1_000_000;

        /// <summary>
        /// Total image bytes one AI request may carry. Once spent, remaining images
        /// in that request go as description only.
        /// </summary>
        public int AiMaxImageBytesPerRequest { get; set; } = 4_000_000;

        public long EffectiveAiMaxImageBytesPerRequest => Math.Clamp(AiMaxImageBytesPerRequest, 10_000, 20_000_000);

        /// <summary>Longest hint accepted from the AI; a longer one is dropped.</summary>
        public int MaxHintLength { get; set; } = 400;

        /// <summary>
        /// How close a hint may come to the correct answer's wording before it is
        /// treated as giving it away (0–1). 0.8 rejects a near-copy — a changed
        /// letter, a diacritic, one word of a two-word answer — while leaving a
        /// hint that merely talks about the same topic alone.
        /// </summary>
        public decimal HintSimilarityThreshold { get; set; } = 0.80m;

        /// <summary>
        /// Escalation levels the Hint button offers per question: 1 = a soft nudge,
        /// 2 = a more direct hint. A third press is refused.
        /// </summary>
        public int MaxHintLevels { get; set; } = 2;

        public decimal EffectiveHintSimilarityThreshold => Math.Clamp(HintSimilarityThreshold, 0.5m, 1m);

        public int EffectiveMaxHintLevels => Math.Clamp(MaxHintLevels, 1, 5);

        /// <summary>
        /// Lowest AI confidence (0–1) at which a proposed essay score is accepted as
        /// the grade. Below it the essay waits for a person.
        /// </summary>
        public decimal EssayAutoAcceptConfidence { get; set; } = 0.80m;

        /// <summary>Longest essay feedback accepted from the AI.</summary>
        public int MaxEssayFeedbackLength { get; set; } = 1000;

        /// <summary>How often the background essay evaluator runs.</summary>
        public int EssayEvaluationIntervalMinutes { get; set; } = 2;

        /// <summary>AI attempts per essay before it is left for a person.</summary>
        public int EssayEvaluationMaxAttempts { get; set; } = 5;

        /// <summary>Wait between two AI attempts on the same essay.</summary>
        public int EssayEvaluationRetryMinutes { get; set; } = 10;

        /// <summary>Upper bound on one background evaluation batch.</summary>
        public int EssayEvaluationTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// The background evaluator leaves essays younger than this alone, so the
        /// submission that created them gets the first try.
        /// </summary>
        public int EssayInlineGraceMinutes { get; set; } = 5;

        public long EffectiveAiMaxImageBytes => Math.Clamp(AiMaxImageBytes, 10_000, 5_000_000);

        public int EffectiveMaxHintLength => Math.Clamp(MaxHintLength, 50, 2000);

        public decimal EffectiveEssayAutoAcceptConfidence => Math.Clamp(EssayAutoAcceptConfidence, 0.5m, 1m);

        public int EffectiveMaxEssayFeedbackLength => Math.Clamp(MaxEssayFeedbackLength, 100, 4000);

        public int EffectiveEssayEvaluationMaxAttempts => Math.Clamp(EssayEvaluationMaxAttempts, 1, 20);

        public TimeSpan EssayEvaluationInterval =>
            TimeSpan.FromMinutes(Math.Max(EssayEvaluationIntervalMinutes, 1));

        public TimeSpan EssayEvaluationRetryDelay =>
            TimeSpan.FromMinutes(Math.Max(EssayEvaluationRetryMinutes, 1));

        public TimeSpan EssayEvaluationTimeout =>
            TimeSpan.FromSeconds(Math.Clamp(EssayEvaluationTimeoutSeconds, 5, 120));

        public TimeSpan EssayInlineGrace =>
            TimeSpan.FromMinutes(Math.Max(EssayInlineGraceMinutes, 1));

        /// <summary>
        /// Questions the placement test takes from each level's LevelAssessment
        /// quiz (in that quiz's DisplayOrder).
        /// </summary>
        public int PlacementQuestionsPerLevel { get; set; } = 4;

        /// <summary>
        /// Percentage of a level's placement questions a child must get right for
        /// that level to count as mastered. With 4 questions, 75 means 3 of 4.
        /// </summary>
        public int PlacementPassPercentage { get; set; } = 75;

        /// <summary>Longest essay answer accepted, in characters.</summary>
        public int EssayAnswerMaxLength { get; set; } = 4000;

        public int EffectivePlacementQuestionsPerLevel => Math.Clamp(PlacementQuestionsPerLevel, 1, 20);

        public decimal EffectivePlacementPassPercentage => Math.Clamp(PlacementPassPercentage, 1, 100);

        public int EffectiveEssayAnswerMaxLength => Math.Clamp(EssayAnswerMaxLength, 100, 20000);

        /// <summary>
        /// The abandonment rule, in one place: an attempt that is still InProgress
        /// and started at or before this instant is Abandoned.
        /// </summary>
        public DateTime AbandonCutoff(DateTime utcNow) => utcNow - InProgressAttemptTimeout;

        // Clamped so a zero or negative value in configuration can never abandon
        // live attempts the moment they start, spin the sweep, or disable the
        // AI bound entirely.
        public TimeSpan InProgressAttemptTimeout =>
            TimeSpan.FromMinutes(Math.Max(InProgressAttemptTimeoutMinutes, 30));

        public TimeSpan AbandonedAttemptSweepInterval =>
            TimeSpan.FromMinutes(Math.Max(AbandonedAttemptSweepIntervalMinutes, 1));

        public TimeSpan AiHintTimeout =>
            TimeSpan.FromSeconds(Math.Clamp(AiHintTimeoutSeconds, 1, 60));
    }
}
