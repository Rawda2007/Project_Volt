/* ============================================================================
   Migration 008 — AI evaluation of essay answers
   ----------------------------------------------------------------------------
   Run AFTER 007 and BEFORE deploying the matching code (the code maps these
   columns). Idempotent. The one-time steps (§2, §4) run until the script has
   completed once: completion is recorded LAST, as the extended property
   Migration008Completed on the table, so a run that failed half-way repeats
   them. (No application code writes these rows before 008 completes — the
   code is deployed after it.)

   New columns on Assessment.QuizAttemptEssayAnswers:
     LanguageCode          the language the child answered in — AI feedback is
                           written in it, even when the background job does it
     MaxPoints             the question's Points at submission — the grade's
                           ceiling (CK ..._AwardedWithinMax)
     AiOutcome             the backend's decision on the AI proposal:
                           Accepted | NeedsReview | Failed (NULL = not decided)
     AiEvaluationAttempts  AI calls tried (retry limit)
     AiLastAttemptAt       last AI call (growing retry delay)
     AiClaimId             which evaluation run owns the row right now, so two
                           app instances never evaluate the same answer
     AiProposedPoints, AiConfidence, AiFeedback
                           the AI's proposal, kept even when not accepted so a
                           reviewer can see it

   The grade the child sees stays in Status / AwardedPoints / Feedback /
   GradedBy / GradedAt; the backend copies an AI proposal there only when it
   accepts it (CK_QuizAttemptEssayAnswers_GradedIsComplete still holds).

   ⚠ Existing Pending essays: they were submitted before AI evaluation existed,
   their language was never recorded, and a person may already be reviewing
   them. On the first run of this script they are marked AiOutcome =
   'NeedsReview' so the AI does NOT grade them. To let the AI evaluate one,
   correct its LanguageCode and set AiOutcome back to NULL.
   ========================================================================== */

USE VoltDB;
GO

-- Required for the filtered index (§5); sqlcmd defaults QUOTED_IDENTIFIER to OFF.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* §1 — columns */

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'LanguageCode') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers
        ADD LanguageCode NVARCHAR(5) NOT NULL
            CONSTRAINT DF_QuizAttemptEssayAnswers_LanguageCode DEFAULT (N'ar');
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'MaxPoints') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers
        ADD MaxPoints TINYINT NOT NULL
            CONSTRAINT DF_QuizAttemptEssayAnswers_MaxPoints DEFAULT (1);
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiOutcome') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers ADD AiOutcome NVARCHAR(20) NULL;
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiEvaluationAttempts') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers
        ADD AiEvaluationAttempts TINYINT NOT NULL
            CONSTRAINT DF_QuizAttemptEssayAnswers_AiEvaluationAttempts DEFAULT (0);
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiLastAttemptAt') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers ADD AiLastAttemptAt DATETIME2(3) NULL;
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiClaimId') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers ADD AiClaimId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiProposedPoints') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers ADD AiProposedPoints TINYINT NULL;
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiConfidence') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers ADD AiConfidence DECIMAL(3,2) NULL;
GO

IF COL_LENGTH(N'Assessment.QuizAttemptEssayAnswers', N'AiFeedback') IS NULL
    ALTER TABLE Assessment.QuizAttemptEssayAnswers ADD AiFeedback NVARCHAR(MAX) NULL;
GO

/* §2 — one-time backfill of MaxPoints (until 008 has completed, so a later
   re-run can never overwrite a frozen maximum with an edited one). The
   question's current Points is the best record there is; an answer already
   graded above it (Points lowered since) keeps its grade as the ceiling, so the
   §3 CHECK holds for every existing row. */
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE major_id = OBJECT_ID(N'Assessment.QuizAttemptEssayAnswers')
                 AND minor_id = 0 AND name = N'Migration008Completed')
    UPDATE e
    SET e.MaxPoints = CASE WHEN e.AwardedPoints > q.Points THEN e.AwardedPoints ELSE q.Points END
    FROM Assessment.QuizAttemptEssayAnswers AS e
    JOIN Assessment.Questions AS q ON q.Id = e.QuestionId;
GO

/* §3 — constraints (own batches: they reference the columns added above) */

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuizAttemptEssayAnswers_Languages')
    ALTER TABLE Assessment.QuizAttemptEssayAnswers WITH CHECK
        ADD CONSTRAINT FK_QuizAttemptEssayAnswers_Languages
            FOREIGN KEY (LanguageCode) REFERENCES Assessment.Languages (Code);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_QuizAttemptEssayAnswers_AiOutcome')
    ALTER TABLE Assessment.QuizAttemptEssayAnswers WITH CHECK
        ADD CONSTRAINT CK_QuizAttemptEssayAnswers_AiOutcome
            CHECK (AiOutcome IS NULL OR AiOutcome IN ('Accepted', 'NeedsReview', 'Failed'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_QuizAttemptEssayAnswers_AiConfidence')
    ALTER TABLE Assessment.QuizAttemptEssayAnswers WITH CHECK
        ADD CONSTRAINT CK_QuizAttemptEssayAnswers_AiConfidence
            CHECK (AiConfidence IS NULL OR AiConfidence BETWEEN 0 AND 1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_QuizAttemptEssayAnswers_AwardedWithinMax')
    ALTER TABLE Assessment.QuizAttemptEssayAnswers WITH CHECK
        ADD CONSTRAINT CK_QuizAttemptEssayAnswers_AwardedWithinMax
            CHECK (AwardedPoints IS NULL OR AwardedPoints <= MaxPoints);
GO

/* §4 — one-time: existing Pending essays are left for a person, not the AI. */
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE major_id = OBJECT_ID(N'Assessment.QuizAttemptEssayAnswers')
                 AND minor_id = 0 AND name = N'Migration008Completed')
BEGIN
    UPDATE Assessment.QuizAttemptEssayAnswers
    SET AiOutcome = N'NeedsReview'
    WHERE Status = N'Pending' AND AiOutcome IS NULL;

    PRINT CONCAT(N'§4: ', @@ROWCOUNT, N' existing Pending essay(s) marked NeedsReview (not sent to the AI).');
END
GO

/* §5 — index for the background evaluator (essays still waiting for the AI). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Assessment.QuizAttemptEssayAnswers')
                 AND name = N'IX_QuizAttemptEssayAnswers_AiDue')
    CREATE NONCLUSTERED INDEX IX_QuizAttemptEssayAnswers_AiDue
        ON Assessment.QuizAttemptEssayAnswers (CreatedAt)
        INCLUDE (AiLastAttemptAt, AiEvaluationAttempts)
        WHERE Status = N'Pending' AND AiOutcome IS NULL;
GO

/* §6 — verification: expect 9 columns, and the index. */
SELECT name, TYPE_NAME(system_type_id) AS type, is_nullable
FROM sys.columns
WHERE object_id = OBJECT_ID(N'Assessment.QuizAttemptEssayAnswers')
  AND name IN (N'LanguageCode', N'MaxPoints', N'AiOutcome', N'AiEvaluationAttempts', N'AiLastAttemptAt',
               N'AiClaimId', N'AiProposedPoints', N'AiConfidence', N'AiFeedback');

SELECT name, filter_definition
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'Assessment.QuizAttemptEssayAnswers')
  AND name = N'IX_QuizAttemptEssayAnswers_AiDue';
GO

/* §7 — record completion LAST: from now on §2 and §4 never run again. */
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE major_id = OBJECT_ID(N'Assessment.QuizAttemptEssayAnswers')
                 AND minor_id = 0 AND name = N'Migration008Completed')
    EXEC sys.sp_addextendedproperty
        @name = N'Migration008Completed', @value = N'2026-09-11',
        @level0type = N'SCHEMA', @level0name = N'Assessment',
        @level1type = N'TABLE',  @level1name = N'QuizAttemptEssayAnswers';
GO
