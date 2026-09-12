/* ============================================================================
   Migration 007 — First-run placement test + Assessment integrity rules
   ----------------------------------------------------------------------------
   Run AFTER 006, and BEFORE deploying the matching code: the code maps
   Assessment.UserPlacements and UserTopicStats.RowVersion, and would fail with
   "Invalid object/column name" against a database without them.

   §1  Quizzes: allow QuizType 'Placement'                    CHECK replaced
   §2  Quizzes: one active placement / one active LessonQuiz   GATED filtered unique indexes
   §3  Assessment.UserPlacements                              new table
   §4  UserTopicStats.RowVersion                              new column (concurrency)
   §5  Verification

   Idempotent: every step checks the current state first; re-running is a no-op.
   ========================================================================== */

USE VoltDB;
GO

-- Required for the filtered indexes (§2); sqlcmd defaults QUOTED_IDENTIFIER to OFF.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


/* ============================================================================
   §1 — QuizType 'Placement'
   A placement quiz references neither a level nor a lesson (like Standalone):
   it samples every level's LevelAssessment quiz.
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_Quizzes_QuizType' AND definition LIKE N'%Placement%')
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    ALTER TABLE Assessment.Quizzes DROP CONSTRAINT IF EXISTS CK_Quizzes_QuizType;
    ALTER TABLE Assessment.Quizzes WITH CHECK
        ADD CONSTRAINT CK_Quizzes_QuizType
            CHECK (QuizType IN ('LevelAssessment', 'LessonQuiz', 'LessonReview', 'Standalone', 'Placement'));

    ALTER TABLE Assessment.Quizzes DROP CONSTRAINT IF EXISTS CK_Quizzes_TypeMatchesReference;
    ALTER TABLE Assessment.Quizzes WITH CHECK
        ADD CONSTRAINT CK_Quizzes_TypeMatchesReference
            CHECK ((QuizType = 'LevelAssessment' AND LevelId IS NOT NULL AND LessonId IS NULL)
                OR (QuizType IN ('LessonQuiz', 'LessonReview') AND LessonId IS NOT NULL AND LevelId IS NULL)
                OR (QuizType IN ('Standalone', 'Placement') AND LevelId IS NULL AND LessonId IS NULL));

    COMMIT TRANSACTION;
END
GO


/* ============================================================================
   §2 — GATED: one active quiz per slot
   Why   : GET /api/quizzes/for-lesson and the placement test must never have
           to guess between two active quizzes. QuizService enforces this for new
           changes; these indexes make it a database guarantee.
   Gate  : if existing rows already break a rule, that index is NOT created and
           the offending rows are listed. Deactivate the extras, then re-run.
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Assessment.Quizzes') AND name = N'UQ_Quizzes_OneActivePlacement')
BEGIN
    IF (SELECT COUNT(*) FROM Assessment.Quizzes WHERE QuizType = N'Placement' AND IsActive = 1) > 1
    BEGIN
        SELECT Id, Title FROM Assessment.Quizzes WHERE QuizType = N'Placement' AND IsActive = 1;
        PRINT N'§2 skipped UQ_Quizzes_OneActivePlacement: more than one active placement quiz (listed above).';
    END
    ELSE
        CREATE UNIQUE NONCLUSTERED INDEX UQ_Quizzes_OneActivePlacement
            ON Assessment.Quizzes (QuizType)
            WHERE QuizType = N'Placement' AND IsActive = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Assessment.Quizzes') AND name = N'UQ_Quizzes_OneActiveLessonQuizPerLesson')
BEGIN
    IF EXISTS (SELECT LessonId FROM Assessment.Quizzes
               WHERE QuizType = N'LessonQuiz' AND IsActive = 1
               GROUP BY LessonId HAVING COUNT(*) > 1)
    BEGIN
        SELECT LessonId, Id, Title FROM Assessment.Quizzes
        WHERE QuizType = N'LessonQuiz' AND IsActive = 1
          AND LessonId IN (SELECT LessonId FROM Assessment.Quizzes
                           WHERE QuizType = N'LessonQuiz' AND IsActive = 1
                           GROUP BY LessonId HAVING COUNT(*) > 1)
        ORDER BY LessonId, Id;
        PRINT N'§2 skipped UQ_Quizzes_OneActiveLessonQuizPerLesson: some lessons have several active LessonQuiz rows (listed above).';
    END
    ELSE
        CREATE UNIQUE NONCLUSTERED INDEX UQ_Quizzes_OneActiveLessonQuizPerLesson
            ON Assessment.Quizzes (LessonId)
            WHERE QuizType = N'LessonQuiz' AND IsActive = 1;
END
GO


/* ============================================================================
   §3 — Assessment.UserPlacements
   One row per learner: the level the placement test put them at. Written in
   the same transaction that completes the placement attempt.
   UserId / PlacedLevelId have no FK by design (other modules), the same
   convention as QuizAttempts.UserId and Quizzes.LevelId.
   ========================================================================== */

IF OBJECT_ID(N'Assessment.UserPlacements', N'U') IS NULL
BEGIN
    CREATE TABLE Assessment.UserPlacements
    (
        Id              BIGINT IDENTITY(1,1)    NOT NULL,
        UserId          UNIQUEIDENTIFIER        NOT NULL,
        QuizAttemptId   BIGINT                  NOT NULL,
        PlacedLevelId   INT                     NOT NULL,
        ScorePercentage DECIMAL(5,2)            NOT NULL,
        PassPercentage  TINYINT                 NOT NULL,
        PlacedAt        DATETIME2(3)            NOT NULL
            CONSTRAINT DF_UserPlacements_PlacedAt DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_UserPlacements PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_UserPlacements_UserId UNIQUE (UserId),
        CONSTRAINT UQ_UserPlacements_QuizAttemptId UNIQUE (QuizAttemptId),
        CONSTRAINT FK_UserPlacements_QuizAttempts FOREIGN KEY (QuizAttemptId)
            REFERENCES Assessment.QuizAttempts (Id),
        CONSTRAINT CK_UserPlacements_ScorePercentage CHECK (ScorePercentage BETWEEN 0 AND 100),
        CONSTRAINT CK_UserPlacements_PassPercentage CHECK (PassPercentage BETWEEN 1 AND 100)
    );
END
GO


/* ============================================================================
   §4 — UserTopicStats.RowVersion
   Problem : the stats update is read-modify-write (Count += n) with no
             concurrency token, so two submissions touching the same row at the
             same time silently lost one set of counts.
   Fix     : a rowversion. The losing submission now gets a concurrency
             conflict, is rolled back whole, and the client retries (409).
   Impact  : additive; SQL Server fills the column for existing rows.
   ========================================================================== */

IF COL_LENGTH(N'Assessment.UserTopicStats', N'RowVersion') IS NULL
    ALTER TABLE Assessment.UserTopicStats ADD RowVersion ROWVERSION NOT NULL;
GO


/* ============================================================================
   §5 — Verification
   ========================================================================== */

-- Expect both definitions to mention Placement.
SELECT name, definition
FROM sys.check_constraints
WHERE name IN (N'CK_Quizzes_QuizType', N'CK_Quizzes_TypeMatchesReference');

-- Expect both rows (a missing one was skipped by the §2 gate — see its output).
SELECT name, filter_definition
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'Assessment.Quizzes')
  AND name IN (N'UQ_Quizzes_OneActivePlacement', N'UQ_Quizzes_OneActiveLessonQuizPerLesson');

-- Expect the table and the column.
SELECT OBJECT_ID(N'Assessment.UserPlacements', N'U') AS UserPlacementsObjectId,
       COL_LENGTH(N'Assessment.UserTopicStats', N'RowVersion') AS RowVersionLength;

-- Readiness of the placement test: levels with an active LevelAssessment quiz
-- and how many answerable auto-graded questions each can contribute.
SELECT l.Id AS LevelId, l.Title, l.[Order], q.Id AS AssessmentQuizId,
       (SELECT COUNT(*) FROM Assessment.Questions AS qu
        WHERE qu.QuizId = q.Id AND qu.IsActive = 1 AND qu.QuestionType <> N'Essay'
          AND EXISTS (SELECT 1 FROM Assessment.QuestionOptions AS o
                      WHERE o.QuestionId = qu.Id AND o.IsCorrect = 1)) AS UsableQuestions
FROM LearningContent.Levels AS l
LEFT JOIN Assessment.Quizzes AS q
       ON q.LevelId = l.Id AND q.QuizType = N'LevelAssessment' AND q.IsActive = 1
ORDER BY l.[Order];
GO
