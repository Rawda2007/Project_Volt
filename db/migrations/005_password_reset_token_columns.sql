/* ============================================================================
   Migration 005 — Restore the password-reset token columns
   ----------------------------------------------------------------------------
   PROBLEM (verified against the live schema scripted 2026-09-11)
     Users.PasswordResetOTPs has: Id, UserId, OTPHash, ExpiresAt, VerifiedAt,
     Attempts, CreatedAt.
     The EF model (UsersDbContext + PasswordResetOtp entity) also maps
     ResetTokenHash and ResetTokenExpiresAt, and AuthService reads and writes
     both. Every reset step therefore fails with "Invalid column name":

       POST /api/auth/forgot-password    → 500 for a REGISTERED email,
                                           200 for an unknown one: an
                                           account-enumeration oracle.
           First failure: the SELECT in
           PasswordResetOtpRepository.DeleteAllUsableForUserAsync —
           .ToListAsync() materializes full entities, so the missing columns
           are selected. It runs outside ForgotPasswordAsync's try/catch
           (which only guards the email send). Unknown emails return early,
           before this line, which is what creates the oracle.
       POST /api/auth/verify-reset-otp   → 500, in GetLatestUsableForUserAsync
       POST /api/auth/reset-password     → 500, in GetByResetTokenHashAsync
                                           (WHERE ResetTokenHash = …)

   CHANGE
     Add the two columns exactly as the entity maps them, plus the lookup index
     and a both-or-neither CHECK that mirrors how AuthService uses them
     (VerifyResetOtpAsync sets both; ResetPasswordAsync clears both).

   IMPACT
     None. Nullable additions. Existing rows get NULL/NULL, which satisfies the
     CHECK. Idempotent. No EF change required — the entity already matches.
   ========================================================================== */

USE VoltDB;
GO

IF COL_LENGTH('Users.PasswordResetOTPs', 'ResetTokenHash') IS NULL
    ALTER TABLE Users.PasswordResetOTPs ADD ResetTokenHash NVARCHAR(500) NULL;
GO

-- datetime2(7) matches every other date column in the Users schema and the
-- unconfigured EF default for this property.
IF COL_LENGTH('Users.PasswordResetOTPs', 'ResetTokenExpiresAt') IS NULL
    ALTER TABLE Users.PasswordResetOTPs ADD ResetTokenExpiresAt DATETIME2(7) NULL;
GO

-- A reset token is always issued with an expiry and always cleared with it.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = 'CK_PasswordResetOTPs_ResetTokenPair')
    ALTER TABLE Users.PasswordResetOTPs
        ADD CONSTRAINT CK_PasswordResetOTPs_ResetTokenPair
            CHECK (
                (ResetTokenHash IS NULL     AND ResetTokenExpiresAt IS NULL)
             OR (ResetTokenHash IS NOT NULL AND ResetTokenExpiresAt IS NOT NULL)
            );
GO

-- ResetPasswordAsync looks rows up by hash; without this it is a scan.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_PasswordResetOTPs_ResetTokenHash')
    CREATE INDEX IX_PasswordResetOTPs_ResetTokenHash
        ON Users.PasswordResetOTPs (ResetTokenHash)
        WHERE ResetTokenHash IS NOT NULL;
GO

-- Verification — expect exactly 2 rows.
SELECT name, TYPE_NAME(system_type_id) AS type, max_length, is_nullable
FROM sys.columns
WHERE object_id = OBJECT_ID('Users.PasswordResetOTPs')
  AND name IN ('ResetTokenHash', 'ResetTokenExpiresAt');
GO
