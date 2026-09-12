/* ============================================================================
   Migration 006 — Quiz-attempt lifecycle + live-schema alignment
   ----------------------------------------------------------------------------
   Written against the table scripts in DatabaseScripts/ (scripted 2026-09-11
   from the live VoltDB), which are the source of truth.

   §1  IX_QuizAttempts_InProgress_StartedAt   additive   serves the abandoned-attempt sweep
   §2  QuizAttemptQuestions.CorrectOptionId   widening   NOT NULL → NULL, so an Essay can be snapshotted
   §3  QuestionTranslations                   GATED      EAV (Field/Value) → typed, like its four siblings
   §4  Verification

   Idempotent: every step checks the current state first; re-running is a no-op.
   Nothing is dropped outright — §3 keeps the old table as a renamed backup.
   Run in SSMS as one script; batches run in order.
   ========================================================================== */

USE VoltDB;
GO

-- Required for filtered indexes (§1); sqlcmd defaults QUOTED_IDENTIFIER to OFF.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


/* ============================================================================
   §1 — Filtered index for the abandoned-attempt sweep
   Query   : UPDATE TOP (500) Assessment.QuizAttempts SET Status = N'Abandoned'
             WHERE Status = N'InProgress' AND StartedAt <= @cutoff
   Why     : no existing index leads on Status or StartedAt
             (IX_QuizAttempts_UserId_QuizId_StartedAt leads on UserId), so every
             sweep would scan the whole table. Filtered to InProgress, the index
             only ever holds live attempts and stays tiny.
   Impact  : additive.
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Assessment.QuizAttempts')
                 AND name = N'IX_QuizAttempts_InProgress_StartedAt')
    CREATE NONCLUSTERED INDEX IX_QuizAttempts_InProgress_StartedAt
        ON Assessment.QuizAttempts (StartedAt)
        WHERE Status = N'InProgress';
GO


/* ============================================================================
   §2 — QuizAttemptQuestions.CorrectOptionId must accept NULL
   Problem : the live column is NOT NULL, while the live
             CK_QuizAttemptQuestions_EssayHasNoKey REQUIRES it to be NULL for an
             Essay. Together they make an Essay snapshot row impossible: starting
             any quiz that contains an active Essay question fails with
             error 515 → HTTP 500. Essay is unusable end to end until this runs.
   Impact  : widening only. Existing rows are non-Essay rows with a key, so the
             restored FK and CHECK pass on every one of them.
   ⚠ If an index you created by hand includes CorrectOptionId, the ALTER fails
     and the whole step rolls back (XACT_ABORT). Drop that index first.
   ========================================================================== */

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'Assessment.QuizAttemptQuestions')
             AND name = N'CorrectOptionId' AND is_nullable = 0)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    -- A column referenced by a constraint cannot be altered: drop, alter, restore.
    ALTER TABLE Assessment.QuizAttemptQuestions
        DROP CONSTRAINT IF EXISTS FK_QuizAttemptQuestions_QuestionId_CorrectOptionId;
    ALTER TABLE Assessment.QuizAttemptQuestions
        DROP CONSTRAINT IF EXISTS CK_QuizAttemptQuestions_EssayHasNoKey;

    ALTER TABLE Assessment.QuizAttemptQuestions
        ALTER COLUMN CorrectOptionId INT NULL;

    -- A NULL CorrectOptionId is exempt from FK checking — exactly what an Essay needs.
    ALTER TABLE Assessment.QuizAttemptQuestions WITH CHECK
        ADD CONSTRAINT FK_QuizAttemptQuestions_QuestionId_CorrectOptionId
            FOREIGN KEY (QuestionId, CorrectOptionId)
            REFERENCES Assessment.QuestionOptions (QuestionId, Id);

    ALTER TABLE Assessment.QuizAttemptQuestions WITH CHECK
        ADD CONSTRAINT CK_QuizAttemptQuestions_EssayHasNoKey
            CHECK ((QuestionType = 'Essay' AND CorrectOptionId IS NULL)
                OR (QuestionType <> 'Essay' AND CorrectOptionId IS NOT NULL));

    COMMIT TRANSACTION;
END
GO


/* ============================================================================
   §3 — GATED: Assessment.QuestionTranslations, EAV → typed
   Problem : the live table is
                 (QuestionId, LanguageCode VARCHAR(5), Field, Value)
             with an FK to dbo.Languages. The application maps
                 (Id, QuestionId, LanguageCode NVARCHAR(5), QuestionText)
             with an FK to Assessment.Languages — the shape the four sibling
             tables (QuizTranslations, QuestionOptionTranslations,
             TopicTranslations, CategoryTranslations) ALREADY have in the live
             database. Every child-facing question query (start/resume an
             attempt, submit's retry set, quiz-for-lesson) reads [Id] and
             [QuestionText] from it and fails with "Invalid column name".
   Why here: it is the only outlier among five translation tables in the live
             DB. CK_QuestionTranslations_Field admits only 'QuestionText', so the
             conversion is lossless — one row per (question, language).
   Impact  : the old table is RENAMED to QuestionTranslations_EAV_Backup, not
             dropped. Drop it by hand once §4 confirms the row counts match.
   Gate    : every LanguageCode must exist in Assessment.Languages (the new FK
             target). If one does not, this section stops and changes nothing.
   ========================================================================== */

-- 3a. Gate. Lists offending rows, then halts the rest of the script if any exist.
IF COL_LENGTH(N'Assessment.QuestionTranslations', N'Field') IS NOT NULL
   AND EXISTS (SELECT 1
               FROM Assessment.QuestionTranslations AS t
               WHERE NOT EXISTS (SELECT 1 FROM Assessment.Languages AS l
                                 WHERE l.Code = t.LanguageCode))
BEGIN
    SELECT DISTINCT t.LanguageCode AS MissingFromAssessmentLanguages
    FROM Assessment.QuestionTranslations AS t
    WHERE NOT EXISTS (SELECT 1 FROM Assessment.Languages AS l WHERE l.Code = t.LanguageCode);

    RAISERROR (N'§3 stopped: QuestionTranslations uses language codes missing from Assessment.Languages. Insert them there, then re-run.', 16, 1);
    SET NOEXEC ON;   -- the remaining §3 batches compile but do not execute
END
GO

-- 3b. Move the EAV table aside. Constraint names are schema-wide, so the
--     canonical names are freed for the new table.
IF COL_LENGTH(N'Assessment.QuestionTranslations', N'Field') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    EXEC sp_rename N'Assessment.QuestionTranslations', N'QuestionTranslations_EAV_Backup';
    EXEC sp_rename N'Assessment.PK_QuestionTranslations', N'PK_QuestionTranslations_EAV_Backup', N'OBJECT';
    EXEC sp_rename N'Assessment.FK_QuestionTranslations_Questions', N'FK_QuestionTranslations_EAV_Backup_Questions', N'OBJECT';
    EXEC sp_rename N'Assessment.FK_QuestionTranslations_Languages', N'FK_QuestionTranslations_EAV_Backup_Languages', N'OBJECT';
    EXEC sp_rename N'Assessment.CK_QuestionTranslations_Field', N'CK_QuestionTranslations_EAV_Backup_Field', N'OBJECT';

    COMMIT TRANSACTION;
END
GO

-- 3c. The typed table, identical in shape to its four siblings and to
--     QuestionTranslationConfiguration.
IF OBJECT_ID(N'Assessment.QuestionTranslations', N'U') IS NULL
BEGIN
    CREATE TABLE Assessment.QuestionTranslations
    (
        Id              INT IDENTITY(1,1)   NOT NULL,
        QuestionId      INT                 NOT NULL,
        LanguageCode    NVARCHAR(5)         NOT NULL,
        QuestionText    NVARCHAR(MAX)       NOT NULL,

        CONSTRAINT PK_QuestionTranslations PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_QuestionTranslations_QuestionId_Language UNIQUE (QuestionId, LanguageCode),
        CONSTRAINT FK_QuestionTranslations_Questions FOREIGN KEY (QuestionId)
            REFERENCES Assessment.Questions (Id) ON DELETE CASCADE,
        CONSTRAINT FK_QuestionTranslations_Languages FOREIGN KEY (LanguageCode)
            REFERENCES Assessment.Languages (Code)
    );
END
GO

-- 3d. Copy the data across, once.
IF OBJECT_ID(N'Assessment.QuestionTranslations_EAV_Backup', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Assessment.QuestionTranslations)
BEGIN
    INSERT INTO Assessment.QuestionTranslations (QuestionId, LanguageCode, QuestionText)
    SELECT b.QuestionId, CAST(b.LanguageCode AS NVARCHAR(5)), b.[Value]
    FROM Assessment.QuestionTranslations_EAV_Backup AS b
    WHERE b.Field = N'QuestionText';
END
GO

SET NOEXEC OFF;
GO


/* ============================================================================
   §4 — Verification
   ========================================================================== */

-- Expect is_nullable = 1.
SELECT name, is_nullable
FROM sys.columns
WHERE object_id = OBJECT_ID(N'Assessment.QuizAttemptQuestions') AND name = N'CorrectOptionId';

-- Expect: Id, QuestionId, LanguageCode, QuestionText.
SELECT name
FROM sys.columns
WHERE object_id = OBJECT_ID(N'Assessment.QuestionTranslations')
ORDER BY column_id;

-- Expect BackupRows = NewRows. Then the backup table can be dropped by hand.
IF OBJECT_ID(N'Assessment.QuestionTranslations_EAV_Backup', N'U') IS NOT NULL
    SELECT (SELECT COUNT(*) FROM Assessment.QuestionTranslations_EAV_Backup WHERE Field = N'QuestionText') AS BackupRows,
           (SELECT COUNT(*) FROM Assessment.QuestionTranslations) AS NewRows;

-- Indexes the application's queries rely on. The supplied table scripts
-- contain no CREATE INDEX statements, so none of these could be verified from
-- them. Every name below should come back; a missing one means a scan.
SELECT OBJECT_SCHEMA_NAME(i.object_id) + N'.' + OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName,
       i.is_unique,
       i.filter_definition
FROM sys.indexes AS i
WHERE i.name IN (N'IX_QuizAttempts_InProgress_StartedAt',
                 N'IX_QuizAttempts_UserId_QuizId_StartedAt',
                 N'IX_QuizAttempts_QuizId',
                 N'IX_Quizzes_LessonId',
                 N'IX_QuizAttemptMistakes_QuestionId',
                 N'IX_QuizAttemptQuestions_QuestionId',
                 N'IX_QuizAttemptEssayAnswers_QuestionId',
                 N'IX_Questions_TopicId',
                 N'IX_UserTopicStats_TopicId',
                 N'UQ_QuestionOptions_OneCorrectPerQuestion')
ORDER BY TableName, IndexName;
GO
