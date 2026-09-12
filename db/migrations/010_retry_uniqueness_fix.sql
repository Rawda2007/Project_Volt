/* ============================================================================
   Migration 010 — one retry per attempt, not one first attempt per database
   ----------------------------------------------------------------------------
   Problem : UQ_QuizAttempts_PreviousAttemptId is a plain UNIQUE key on a
             NULLable column. SQL Server treats NULLs as equal in a unique key,
             so it permits exactly ONE row with PreviousAttemptId = NULL — one
             first attempt in the entire database. Every later StartAsync for a
             first attempt fails with 2627, and PersistAttemptAsync only
             translates that violation for a RETRY, so it surfaces as a 500.
   Why now : the rule was only ever "an attempt may be retried at most once".
   Fix     : replace it with the same uniqueness, filtered to non-NULL rows.
   Impact  : none on data. Existing databases are almost certainly already
             stuck at one first attempt; this unblocks them.

   Idempotent. Run any time (it is independent of 006–009).
   ========================================================================== */

USE VoltDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* GATE — must return 0 rows: two attempts already retrying the same attempt
   would break the replacement index. (It cannot happen under the old key, which
   was stricter; this is belt and braces.) */
SELECT PreviousAttemptId, COUNT(*) AS Retries
FROM Assessment.QuizAttempts
WHERE PreviousAttemptId IS NOT NULL
GROUP BY PreviousAttemptId
HAVING COUNT(*) > 1;
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints
           WHERE name = N'UQ_QuizAttempts_PreviousAttemptId'
             AND parent_object_id = OBJECT_ID(N'Assessment.QuizAttempts'))
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    ALTER TABLE Assessment.QuizAttempts DROP CONSTRAINT UQ_QuizAttempts_PreviousAttemptId;

    CREATE UNIQUE NONCLUSTERED INDEX UQ_QuizAttempts_PreviousAttemptId
        ON Assessment.QuizAttempts (PreviousAttemptId)
        WHERE PreviousAttemptId IS NOT NULL;

    COMMIT TRANSACTION;
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes
                    WHERE name = N'UQ_QuizAttempts_PreviousAttemptId'
                      AND object_id = OBJECT_ID(N'Assessment.QuizAttempts'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_QuizAttempts_PreviousAttemptId
        ON Assessment.QuizAttempts (PreviousAttemptId)
        WHERE PreviousAttemptId IS NOT NULL;
GO

/* Verification: expect one row, is_unique = 1, with a filter on the column. */
SELECT name, is_unique, filter_definition
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'Assessment.QuizAttempts')
  AND name = N'UQ_QuizAttempts_PreviousAttemptId';
GO
