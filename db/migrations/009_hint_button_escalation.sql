/* ============================================================================
   Migration 009 — Hint button (escalating hints)
   ----------------------------------------------------------------------------
   Run AFTER 008 and BEFORE deploying the matching code. Idempotent.

   A hint used to hang off a mistake row, which only exists after a submission.
   The Hint button produces hints while the attempt is still in progress, so a
   hint now belongs to (QuizAttemptId, QuestionId) directly:

     QuizAttemptId, QuestionId   NEW, backfilled from the mistake
     AttemptNumber               NEW, escalation level 1..5 (NULL for a hint
                                 generated after a submission)
     QuizAttemptMistakeId        now NULL-able (NULL = Hint-button hint)

   Uniqueness moves from (mistake, language, sequence) to
   (attempt, question, language, sequence) — the same guarantee, since a mistake
   is one per (attempt, question), but it also covers hints with no mistake.

   Cascade: the hint's new FK to QuizAttemptQuestions carries ON DELETE CASCADE,
   and the mistake FK becomes NO ACTION, so there is exactly one cascade path
   from QuizAttempts down to a hint (SQL Server rejects two).
   ========================================================================== */

USE VoltDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* §1 — new columns, nullable for now so existing rows survive. */

IF COL_LENGTH(N'Assessment.QuestionHints', N'QuizAttemptId') IS NULL
    ALTER TABLE Assessment.QuestionHints ADD QuizAttemptId BIGINT NULL;
GO

IF COL_LENGTH(N'Assessment.QuestionHints', N'QuestionId') IS NULL
    ALTER TABLE Assessment.QuestionHints ADD QuestionId INT NULL;
GO

IF COL_LENGTH(N'Assessment.QuestionHints', N'AttemptNumber') IS NULL
    ALTER TABLE Assessment.QuestionHints ADD AttemptNumber TINYINT NULL;
GO

/* §2 — backfill from the mistake each existing hint belongs to. */
UPDATE h
SET h.QuizAttemptId = m.QuizAttemptId,
    h.QuestionId    = m.QuestionId
FROM Assessment.QuestionHints AS h
JOIN Assessment.QuizAttemptMistakes AS m ON m.Id = h.QuizAttemptMistakeId
WHERE h.QuizAttemptId IS NULL OR h.QuestionId IS NULL;
GO

/* §3 — GATE: every hint must now know its attempt and question. Must return 0
   rows; a row here is a hint whose mistake vanished, which has to be resolved by
   hand (delete it, or set the columns from whatever record you have). */
SELECT Id, QuizAttemptMistakeId
FROM Assessment.QuestionHints
WHERE QuizAttemptId IS NULL OR QuestionId IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM Assessment.QuestionHints WHERE QuizAttemptId IS NULL OR QuestionId IS NULL)
BEGIN
    ALTER TABLE Assessment.QuestionHints ALTER COLUMN QuizAttemptId BIGINT NOT NULL;
    ALTER TABLE Assessment.QuestionHints ALTER COLUMN QuestionId INT NOT NULL;
END
ELSE
BEGIN
    -- Everything below assumes both columns are filled: the new unique index
    -- treats NULLs as equal and would collide on them. Stop the whole script.
    RAISERROR (N'§3 stopped: some hints have no attempt/question (listed above). Resolve them, then re-run.', 16, 1);
    SET NOEXEC ON;
END
GO

/* §4 — the mistake link becomes optional, and NO ACTION.
   Gated on EITHER symptom: on a re-run after a partial failure the column may
   already be NULL-able while the FK still cascades, and §5 would then fail with
   1785 (multiple cascade paths). */
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'Assessment.QuestionHints')
             AND name = N'QuizAttemptMistakeId' AND is_nullable = 0)
   OR EXISTS (SELECT 1 FROM sys.foreign_keys
              WHERE name = N'FK_QuestionHints_QuizAttemptMistakes'
                AND delete_referential_action <> 0)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    ALTER TABLE Assessment.QuestionHints DROP CONSTRAINT IF EXISTS FK_QuestionHints_QuizAttemptMistakes;
    ALTER TABLE Assessment.QuestionHints DROP CONSTRAINT IF EXISTS UQ_QuestionHints_MistakeId_Language_Sequence;

    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Assessment.QuestionHints')
                 AND name = N'UQ_QuestionHints_MistakeId_Language_Sequence')
        DROP INDEX UQ_QuestionHints_MistakeId_Language_Sequence ON Assessment.QuestionHints;

    ALTER TABLE Assessment.QuestionHints ALTER COLUMN QuizAttemptMistakeId BIGINT NULL;

    ALTER TABLE Assessment.QuestionHints WITH CHECK
        ADD CONSTRAINT FK_QuestionHints_QuizAttemptMistakes
            FOREIGN KEY (QuizAttemptMistakeId) REFERENCES Assessment.QuizAttemptMistakes (Id);

    COMMIT TRANSACTION;
END
GO

/* §5 — the hint's own link to the attempt's question, and the new uniqueness. */

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_QuestionHints_QuizAttemptQuestions')
    ALTER TABLE Assessment.QuestionHints WITH CHECK
        ADD CONSTRAINT FK_QuestionHints_QuizAttemptQuestions
            FOREIGN KEY (QuizAttemptId, QuestionId)
            REFERENCES Assessment.QuizAttemptQuestions (QuizAttemptId, QuestionId)
            ON DELETE CASCADE;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Assessment.QuestionHints')
                 AND name = N'UQ_QuestionHints_AttemptId_QuestionId_Language_Sequence')
    CREATE UNIQUE NONCLUSTERED INDEX UQ_QuestionHints_AttemptId_QuestionId_Language_Sequence
        ON Assessment.QuestionHints (QuizAttemptId, QuestionId, LanguageCode, HintSequence);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_QuestionHints_AttemptNumber')
    ALTER TABLE Assessment.QuestionHints WITH CHECK
        ADD CONSTRAINT CK_QuestionHints_AttemptNumber
            CHECK (AttemptNumber IS NULL OR AttemptNumber BETWEEN 1 AND 5);
GO

/* §6 — verification: expect the three columns, the FK, the index and the CHECK. */
SELECT name, is_nullable FROM sys.columns
WHERE object_id = OBJECT_ID(N'Assessment.QuestionHints')
  AND name IN (N'QuizAttemptId', N'QuestionId', N'AttemptNumber', N'QuizAttemptMistakeId');

SELECT name, delete_referential_action_desc FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID(N'Assessment.QuestionHints');

SELECT name, is_unique FROM sys.indexes
WHERE object_id = OBJECT_ID(N'Assessment.QuestionHints') AND name LIKE N'UQ_QuestionHints%';
GO

SET NOEXEC OFF;
GO
