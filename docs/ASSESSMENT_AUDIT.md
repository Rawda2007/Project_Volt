# Assessment Module — Fixes, Second Audit, Schema Comparison, AI Contract

_2026-09-11 · branch `basmala-dev` · written against the working tree and the
table scripts in `DatabaseScripts/` (source of truth for the live schema)._

> **Not compiled or run.** No .NET SDK is installed on the machine this was
> written on. Run `dotnet build` and `dotnet test Tests/Assessment.Tests` before
> merging, then apply `db/migrations/006_attempt_lifecycle_and_schema_alignment.sql`.

---

## 0. Two things that block the app on the live database today

Both come from comparing the code with `DatabaseScripts/`, and both were already
broken before this change. `db/migrations/006` fixes them.

| # | Problem | Effect | Fix |
|---|---------|--------|-----|
| C1 | `Assessment.QuestionTranslations` is **EAV** in the live DB (`QuestionId, LanguageCode varchar(5), Field, Value`, FK → `dbo.Languages`). The code — and the other **four** translation tables in the same live DB — use the typed shape (`Id, QuestionId, LanguageCode nvarchar(5), QuestionText`, FK → `Assessment.Languages`). | Every query that shows a question to a child selects `[Id]`/`[QuestionText]` → `Invalid column name` → **HTTP 500** on Start attempt, resume attempt, the submit retry set, and quiz-for-lesson. | 006 §3 (gated, lossless; keeps the old table as `QuestionTranslations_EAV_Backup`) |
| C2 | `QuizAttemptQuestions.CorrectOptionId` is **NOT NULL** in the live DB, while the live `CK_QuizAttemptQuestions_EssayHasNoKey` **requires NULL** for an Essay. | An Essay snapshot row cannot exist → starting any quiz with an active Essay → error 515 → **HTTP 500**. Essay is unusable end to end. | 006 §2 |

---

## 1. What was implemented

### 1.1 AI can no longer control submit success (items 1 and 3)

**Before.** `SubmitAsync` called the AI **before** opening the transaction, and
let every AI exception escape:

| AI outcome | What the child got | Saved? |
|---|---|---|
| Endpoint not configured (the current config: `Ai:HintsEndpoint` is `""` in Development and absent in `appsettings.json`) | **400** "AI hints endpoint is not configured" | nothing |
| Network error / HTTP 4xx-5xx | **500** | nothing |
| Timeout (HttpClient default: 100 s) | **500** after up to 100 s | nothing |
| Malformed JSON | **500** | nothing |
| Response missing or blank for one question | **400** "The AI response did not contain one valid hint…" | nothing |

So **with the current configuration, every submission containing at least one
wrong answer failed and saved nothing.** A second bug sat inside the transaction:
`PersistHintsAsync` indexed `hintTextByQuestion[m.QuestionId]`, so a mistake that
had been *skipped* for hint generation (image-only option without a description)
threw `KeyNotFoundException` → **404**, rolling back the whole submission.

**After.** `SubmitAsync` is two phases with a hard boundary:

```
Phase A — CRITICAL (GradeAndCommitAsync → CommitSubmissionAsync)
  load attempt → ownership → state (Completed ⇒ replay, Abandoned/expired ⇒ 410)
  → validate answers against the frozen snapshot → grade
  → BEGIN TRAN
      insert QuizAttemptMistakes + QuizAttemptEssayAnswers
      UPDATE QuizAttempts  InProgress → Completed   (RowVersion-guarded)
      UserTopicStats += …
    COMMIT
  (runs with CancellationToken.None: a dropped connection cannot cancel it half-way)

Phase B — OPTIONAL (BuildRetryQuestionsWithHintsAsync) — never throws
  load retry questions
  → AI call, bounded by Assessment:AiHintTimeoutSeconds (15 s), NOT tied to the
    request token, so hints still get generated if the child's connection drops
  → keep only well-formed hints (bad or missing ones are dropped per question)
  → save hints + refresh HintsUsedCount   (own try/catch; failure is logged)
```

The response reports what happened in a new field, **`hintsStatus`**:
`NotRequired` (no wrong answers) · `Generated` · `Partial` · `Unavailable`.
The score in the same response is final whatever it says.

### 1.2 N+1 in `UserTopicStatService` (item 2)

**Before.** Inside a LINQ-to-Objects grouping:

```csharp
Correct = g.Count(q => !_db.QuizAttemptMistakes.Any(m =>
    m.QuizAttemptId == quizAttemptId && m.QuestionId == q.QuestionId)),
```

That is one **synchronous** SQL round-trip per auto-graded question, from inside
an `async` method: an N+1 *and* sync-over-async (a thread blocked on I/O per
question).

**After.** One query before the loop, materialised into a `HashSet<int>`:

```sql
SELECT [q].[QuestionId] FROM [Assessment].[QuizAttemptMistakes] AS [q]
WHERE [q].[QuizAttemptId] = @quizAttemptId
```

Two other hidden costs in the same workflow were also removed:

* The hint count aggregated **every hint the child ever received, across every
  topic**, on every submission. It is now filtered to the topics being written.
* `existingStats.FirstOrDefault(...)` per bucket (in-memory, O(n²)) → dictionary.

Round trips for the stats update are now fixed at 5 reads + 1 save, whatever the
quiz size (before: 4 + one per question). Behaviour is unchanged: same buckets,
same `+=` for answered/correct, same recomputed `HintsUsedCount`. Because hints are
now saved *after* the commit, a new `RefreshHintsUsedCountAsync` re-derives
`HintsUsedCount` once they are saved, which keeps the old semantics (the count
includes this attempt's hints).

> I could not capture the generated SQL — there is no SDK here. The statements
> above are what EF Core 8 emits for these shapes; verify with
> `optionsBuilder.LogTo(Console.WriteLine)` once built.

### 1.3 `GET /api/quizzes/for-lesson/{lessonId}` (item 4)

* **Relationship used (existing):** `Quizzes.LessonId` with
  `QuizType ∈ {LessonQuiz, LessonReview}` (`CK_Quizzes_TypeMatchesReference`),
  and `Questions.QuizId` (FK). `LessonId` has no FK by design, and nothing links
  Lessons to Topics, so no parallel relationship was invented.
* **Lesson validation:** the Assessment module cannot reference ContentBL, so it
  asks a new Shared contract `ILessonAvailability` (implemented in ContentBL). This
  follows the same pattern `IAiHintGenerator` already uses. A missing **or
  unpublished** lesson → 404, with the same message for both. `QuizService`
  therefore needs the Content module registered (`Program.cs` already registers
  both).
* **Quiz chosen:** the newest **active `LessonQuiz`** of the lesson with at least
  one active question. `LessonReview` is deliberately not returned (see §2, M5).
* **Returned:** `LessonQuizResponseDto` — quiz id/title/description (localized),
  and the active questions/options in `DisplayOrder`, via the **same** child-safe
  projection the attempt endpoints use (extracted to `LocalizedQuestionQuery`, not
  duplicated). No `IsCorrect`, no `CorrectOptionId`, no `ImageDescription`, no
  admin fields.
* **No quiz** → 404 `ApiResponse` envelope (existing `KeyNotFoundException`
  convention).
* **Auth:** any authenticated user, same as `/api/quiz-attempts`. It lives in a
  separate `LessonQuizController` because `QuizController` is Admin-only at class
  level, and an action-level `[Authorize]` cannot relax that.
* **IDOR:** none — the endpoint returns catalogue content, no user-owned data.
* **Note for Flutter:** this is a preview. Answers are graded against the question
  set returned by `POST /api/quiz-attempts?quizId=…` (the frozen snapshot).

### 1.4 Abandoned attempts (item 5)

* **Rule (one place, `QuizAttemptService.AbandonCutoff`):** an attempt still
  `InProgress` **`Assessment:InProgressAttemptTimeoutMinutes` (default 180,
  minimum 30)** after `StartedAt` is `Abandoned`.
* **Enforced two ways, same rule:**
  1. Submit re-checks it and marks the attempt Abandoned on the spot. The outcome
     never depends on when the sweep last ran.
  2. `AbandonedQuizAttemptSweeper` (a `BackgroundService`) runs every
     `AbandonedAttemptSweepIntervalMinutes` (15). It calls
     `AbandonExpiredAttemptsAsync`, which does `ExecuteUpdateAsync` in batches of
     500: `UPDATE TOP(500) … SET Status='Abandoned' WHERE Status='InProgress' AND
     StartedAt <= @cutoff`. Nothing is loaded into memory.
* **Safety:** only `InProgress` rows ever match, so the sweep is idempotent, never
  touches Completed attempts, and is safe on several instances at once. A
  Submit racing the sweep loses cleanly: `RowVersion` makes its UPDATE hit 0 rows,
  it re-reads the status, and returns 410.
* **Schema:** no new column. `StartedAt` is sufficient, `CK_QuizAttempts_Status`
  already allows `Abandoned`, and `CompletedAt` stays NULL (all CHECKs allow it).
  The one addition is a filtered index for the sweep:
  `IX_QuizAttempts_InProgress_StartedAt (StartedAt) WHERE Status = N'InProgress'`
  (006 §1).
* **Infrastructure:** the project has **no Hangfire or any scheduler**. Rather
  than introduce one, this uses the host's built-in `BackgroundService`. If
  Hangfire is added later, `AbandonExpiredAttemptsAsync` is the recurring job
  body as-is.

### 1.5 `GET /api/quiz-attempts/{attemptId}/result` (item 6)

| Attempt state | Response |
|---|---|
| Completed, yours | **200** `QuizAttemptResultDto` — score, counts, retry questions with any saved hints, `hintsStatus`, `quizId`, `completedAt` |
| InProgress (not expired) | **409** "not submitted yet" → Flutter re-submits (safe, see 1.6) |
| Abandoned, or InProgress past the window | **410** "expired" → Flutter starts a new attempt |
| Someone else's | **403** (existing module convention for other users' attempts) |
| Does not exist | **404** |

Ownership comes only from the JWT `sub` claim. `wrongAnswers` is the number of
saved mistakes, not the persisted `WrongAnswersCount`, because that computed
column counts essays as wrong. The attempt id was already consistent: Start
returns it (201 + `Location`) before any submit, and Submit echoes it. So no
contract change was needed; the result DTO only gained `quizId` (to start a
retry after recovery), `completedAt` and `hintsStatus`.

### 1.6 Idempotency / double submission (item 7)

| Scenario | Before | After |
|---|---|---|
| Submit twice in sequence (retry after lost response) | 400 "already submitted"; the child sees an error and has no way to fetch the saved result | **200, the saved result replayed**. No re-grading, no stats, no AI |
| Two concurrent submits | Winner commits; loser 409 via RowVersion. **AI called twice** (it ran before the transaction) | Loser re-reads the status → **200 replay**. AI runs once, only in the winner's Phase B |
| Stats counted twice? | No (same transaction as the status change) | No (unchanged) |
| Two concurrent submits of the same attempt with **different** answers | Could deadlock (each holds rows the other needs); the victim got **500** | Deadlock victim (SQL 1205) is treated as a lost race → re-read → **200 replay** (or 409 retry if the winner has not committed yet) |
| Submit an Abandoned attempt | 400 | **410** |

State machine: `InProgress → Completed` (once, RowVersion-guarded) or
`InProgress → Abandoned` (sweep or late submit). Both are terminal.

---

## 2. Second audit — the whole module after the changes

> **Round 2** (placement test) resolved most ⚠ items below — see §7 for which.

Severity: **C** critical · **H** high · **M** medium · **L** low. Status: ✅ fixed
here · ⚠ open (decision or larger change needed).

### A. API layer
| Sev | Finding | Status |
|---|---|---|
| H | Submit returned 400/404/500 for AI failures after partial work | ✅ |
| M | `ExceptionMiddleware` maps **every** `InvalidOperationException` to 400 with its raw message. Framework/EF/provider exceptions (translation errors, "Sequence contains no elements") therefore surface as 400 with internal text. Recommend a dedicated business-rule exception and 500 for everything else. | ⚠ cross-module |
| M | `ExceptionMiddleware` logs expected 4xx at `Error` level (noise; hides real errors), and a client disconnect (`OperationCanceledException`) is logged as an error and mapped to 500 | ⚠ |
| L | `Accept-Language` was passed through raw, so a weighted header such as `en;q=0.9` or `en,ar-EG;q=0.8` resolved to Arabic. It now picks the highest-weighted supported language. | ✅ |
| L | Success bodies are raw DTOs while errors use the `ApiResponse` envelope. This is the existing Assessment convention and is kept. | ⚠ by design |
| — | Routes: `api/quizzes/for-lesson/{int}` cannot collide with admin `api/quizzes/{int}` (different segment count). All reads are GET; submit is POST. 410 documented in Swagger (`DefaultApiResponsesOperationFilter` completes it). | ✅ |

### B. Business layer
| Sev | Finding | Status |
|---|---|---|
| **H** | **Omitted auto-graded answers are scored as correct.** `correct = autoGraded − confirmedMistakes`, so `{"mistakes":[]}` scores 100%. This is a documented compatibility rule ("clients that send only wrong answers keep working"), and `docs/mocks/mock_submit_attempt_request.json` still shows that shape. But the child DTO never contains `IsCorrect`, so a legitimate client cannot know which answers are wrong and must send all of them anyway. Requiring one answer per auto-graded question closes the hole; it breaks any client that lets a child skip, so coordinate with Flutter first. | ⚠ decision |
| M | `Points` are stored but ignored by scoring (every question weighs the same) | ⚠ product decision |
| M | Option-count invariants are only checked at activation: `CreateOptionAsync` can add a **third option to an active TrueFalse** question; `DeleteOptionAsync` can drop an active MCQ below two options | ⚠ |
| M | Several active `LessonQuiz` rows may point at one lesson. The new endpoint picks the newest deterministically; recommend a filtered unique index | ⚠ |
| M | `QuizService.Create` does not verify `LessonId`/`LevelId` exist (loose references). `ILessonAvailability` now exists and can be reused. | ⚠ |
| M | Start does not check that a lesson quiz's lesson is published, so a known quiz id of an unpublished lesson can be started. (Content's `GET lessons/{id}` also serves unpublished lessons to any user.) | ⚠ |
| L | Essay-only quiz: `scorePercentage = 0` with `autoGradedQuestions = 0`. Flutter must show "pending review", not 0%. | ⚠ client |
| L | Only **wrong** selections are persisted; a child's correct selections are not stored anywhere | ⚠ by design |
| L | `QuestionOptionService` create/update responses dropped `ImageUrl`/`ImageDescription` | ✅ |
| — | Lifecycle, idempotency, abandonment, AI isolation: see §1 | ✅ |

### C. Data access
| Sev | Finding | Status |
|---|---|---|
| H | N+1 + sync-over-async in `UpdateAfterQuizAttemptAsync` | ✅ |
| M | Hint aggregation over all topics on every submit | ✅ |
| M | Deadlock between two concurrent submits of one attempt with different answers. EF inserts the answer rows before the attempt UPDATE, and the stats read then meets the other request's uncommitted rows. It is now handled as a lost race (§1.6). Enabling `READ_COMMITTED_SNAPSHOT` on VoltDB would remove the reader/writer blocking entirely. | ✅ handled / ⚠ RCSI recommended |
| L | `PersistHintsAsync` grouped hints client-side (loading and tracking whole rows) to compute the next sequence | ✅ projected to SQL `MAX` |
| M | Concurrent submissions of **different** attempts by the same child can lose an update in `UserTopicStats` (`+=` on values read without a lock; no rowversion on that table). Needs two devices at the same instant. The unique-violation variant (both inserting a new bucket) is now a retryable 409 instead of a 500. Full fix: `UPDATE … SET x = x + @n` or a rowversion. | ⚠ |
| — | Reads are `AsNoTracking` projections (no `Include`). One transaction per submit. `ExecuteUpdate` is used for the sweep and the single-row abandon. The stats loop is in-memory only. | ✅ |
| — | Localized projection uses correlated `TOP(1)` sub-queries per question/option. One statement, not N+1; acceptable at quiz sizes. | — |
| L | Indexes could not be verified — the table scripts contain no `CREATE INDEX` statements. 006 §4 lists every index the queries rely on. | ⚠ verify |

### D. AI integration
| Sev | Finding | Status |
|---|---|---|
| C | AI failure failed the submission | ✅ |
| H | No timeout (HttpClient default 100 s) | ✅ 15 s budget in Phase B |
| M | A partially bad AI response discarded every hint | ✅ salvaged per question |
| M | AI text is shown to children with no length cap and no moderation | ⚠ |
| L | No auth header, no correlation id, no contract version on the AI call | ⚠ see §5 |
| — | Cancellation: Phase B ignores the request token on purpose (hints are finished and saved even if the client drops); only its own budget cancels it | ✅ |

### E. Security
| Sev | Finding | Status |
|---|---|---|
| — | **IDOR:** every attempt endpoint compares `attempt.UserId` with the `sub` claim **before** returning data, and before a submit replay. The new result endpoint does the same. The lesson endpoint returns no user data. | ✅ |
| — | **Answer key:** no child DTO has `IsCorrect`, `CorrectOptionId` or `ImageDescription` (new test `ChildFacingDtos_NeverCarryTheAnswerKeyOrAdminMetadata`). Admin DTOs are behind `[Authorize(Roles="Admin")]`. | ✅ |
| — | **Over-posting:** request DTOs carry no user id, status or score | ✅ |
| H | Attempt manipulation via omitted answers (B-H above) | ⚠ |
| M | Essay `AnswerText` is unbounded (`nvarchar(max)`; only Kestrel's 30 MB body limit) | ⚠ pick a cap |
| L | 403 (not 404) for another user's attempt reveals that the sequential id exists. Kept for module consistency. | ⚠ |
| L | Parent/Admin tokens can take quizzes (endpoints are `[Authorize]` only) and the stats are attributed to that account | ⚠ confirm intended |
| L | `/uploads/*` images are publicly served without auth | ⚠ |
| L | The retry set reveals which answers were wrong; for TrueFalse that reveals the right answer before the retry | inherent |

---

## 3. SQL scripts vs code

| Table / object | Finding | Sev | Action |
|---|---|---|---|
| `QuestionTranslations` | EAV in DB vs typed entity; FK to `dbo.Languages` vs `Assessment.Languages`; `varchar(5)` vs `nvarchar(5)` | **C** | 006 §3 |
| `QuizAttemptQuestions.CorrectOptionId` | NOT NULL in DB vs `int?` + CHECK that requires NULL for Essay | **C** | 006 §2 |
| `CK_QuizAttemptQuestions_EssayHasNoKey` | DB is stricter (Essay ⇒ NULL **and** non-Essay ⇒ NOT NULL) than the EF metadata was | L | EF text aligned; snapshot now forces NULL for Essay |
| Two `Languages` tables | `dbo.Languages` (varchar, no IsActive) and `Assessment.Languages`. EF maps only the latter; after 006 §3 nothing in Assessment uses `dbo.Languages` | L | drop later if unused elsewhere |
| `QuizAttempts` | Matches: RowVersion `timestamp`, persisted computed columns, all 8 CHECKs incl. `Abandoned`. **Missing** index for the sweep. | M | 006 §1 |
| `QuizAttempts.WrongAnswersCount` | `answered − correct`, and `answered` is always set to total, so essays count as wrong | L | DTOs no longer read it |
| `QuizAttemptMistakes`, `QuizAttemptEssayAnswers`, `QuestionHints`, `UserTopicStats`, `Quizzes`, `Topics`, `Categories`, `QuizTranslations`, `QuestionOptionTranslations`, `TopicTranslations`, `CategoryTranslations` | Columns, nullability, FKs, cascade rules and CHECKs match the EF configuration | — | — |
| `Questions.QuestionType` / `QuizAttemptQuestions.QuestionType` | `nvarchar(30)` vs EF `HasMaxLength(20)` | L | harmless |
| `Questions/QuestionOptions.ImageUrl`, `ImageDescription` | `nvarchar(max)` vs EF 500/1000 (migration 003/004 created them at 500/1000; the live table was widened) | L | harmless for reads; admin input is not length-validated |
| `CK_QuestionOptions_ImageOptionHasDescription` | DB also requires a non-blank description; the service already trims blank → null | — | — |
| Image **questions** | Unlike options, nothing requires `ImageDescription` when a question has `ImageUrl` | L | add CHECK + service rule before AI work |
| `QuizAttempts.UserId`, `UserTopicStats.UserId` | No FK to `Users.Users` (by design: loose coupling). Deleting a user orphans attempts. | L | by design |
| `Quizzes.LessonId/LevelId` | No FK to `LearningContent` (by design); no app-level existence check on create | M | see B |
| `Quizzes` | No uniqueness for "one active LessonQuiz per lesson" | M | recommend filtered unique index |
| Indexes | Not verifiable from the scripts (`UQ_QuestionOptions_OneCorrectPerQuestion` is the one grading depends on) | ⚠ | 006 §4 verification query |
| `Users.Role` CHECK | Includes `Admin`; `UsersBL.UserRoles` has no `Admin` constant | L | — |

---

## 4. Question types — end-to-end verification

Traced: DB CHECK → EF config → entity → admin DTO/service → child DTO/projection →
submit DTO → `GradeAndCommitAsync` → score → persistence → AI → response.
Only three types exist anywhere: `CK_Questions_QuestionType`,
`CK_QuizAttemptQuestions_QuestionType` and `QuestionTypes` all list exactly
`MultipleChoice | TrueFalse | Essay`. "Image-based" is not a type; it is a
modality any of the three can carry. (`LevelAssessment/LessonQuiz/LessonReview/
Standalone` are *quiz* types.)

| Question Type | Stored | Retrieved | Submitted | Evaluated | Scored | Persisted | AI Supported | Status |
|---|---|---|---|---|---|---|---|---|
| **MCQ** | YES | YES¹ | YES | YES | YES² | PARTIAL³ | YES⁴ | **Supported, with caveats** |
| **True/False** | YES | YES¹ | YES | YES | YES² | PARTIAL³ | YES⁴ | **Supported, with caveats** |
| **Essay** | YES | YES¹ | YES | **NO** | **NO** | YES | **NO** | **Partial — stored for review only**; cannot start on the live DB until 006 §2 |

¹ On the live DB, retrieval fails for **every** type until 006 §3 (C1).
² Equal weight per question (`Points` ignored); omitted answers count as correct (B-H).
³ Only wrong selections are saved (`QuizAttemptMistakes`); correct ones are not stored.
⁴ Hints for wrong answers only. The call is implemented but not configured, so it
currently always yields `hintsStatus: "Unavailable"`.

**MultipleChoice.** `Question 1—* QuestionOption` (FK, cascade). At most one
`IsCorrect` per question (filtered unique index, service pre-check), exactly one
at activation. At Start the key is frozen into `QuizAttemptQuestions.CorrectOptionId`,
and grading uses only that snapshot. The submitted `selectedOptionId` is checked
to exist, to belong to the question (service + composite FK), and to belong to the
attempt. `IsCorrect` is never in a child DTO.

**TrueFalse.** Stored as **two `QuestionOption` rows** (e.g. "صح"/"خطأ"), one
with `IsCorrect = 1`; there is no boolean column. It is submitted exactly like
MCQ (`selectedOptionId`, an `int`), so no string-vs-bool conversion exists
anywhere. Scoring is identical to MCQ. Type-specific gap: the "exactly two
options" rule is enforced at activation only (B-M).

**Essay.** No options (enforced at activation and on option create). Submitted
as `essayAnswers[{questionId, answerText}]`. The text is persisted **trimmed** in
`QuizAttemptEssayAnswers` with `Status = 'Pending'`. Empty or whitespace → 400.
An essay the child omits is simply not stored. It is **not evaluated at all**:
there is no grading code, no endpoint, no job, and it is not sent to the AI. The
`AwardedPoints/Feedback/GradedBy('Human'|'Ai')/GradedAt` columns exist but
nothing writes them. **`pendingEssayQuestions` means exactly that: stored, not
scored, excluded from both numerator and denominator.** No model answer exists
in the schema, so none can be exposed. AI failure cannot affect essays because
the AI never sees them. Mixed quizzes work: the denominator is auto-graded
questions only.

**On sending the essay and the child's text to the AI:** you asked for this in
item 11, and item 12 says not to implement AI integration yet. I followed item
12 — nothing was added to the AI call. The essay task is fully specified in the
contract below (§5.4 Essay), ready to implement.

---

## 5. AI integration readiness

### CURRENTLY IMPLEMENTED vs RECOMMENDED

**Correction to the brief:** an AI **client** does exist — `AIIntegration/`
(`IAiHintGenerator` → `HttpExternalAiProvider`, POST to `Ai:HintsEndpoint`), called
from Submit. What does not exist is an AI **service**: the endpoint is empty, so
every call throws "not configured". Its current wire contract (pinned by
`AiProviderContractTests`) is:

```json
// CURRENTLY IMPLEMENTED — request (camelCase, no auth header)
{ "language": "ar",
  "questions": [ { "questionId": 102, "questionText": "ما وحدة قياس المقاومة الكهربية؟",
                   "wrongOptionText": "الفولت", "previousHints": ["فكّر في العالم الألماني."] } ] }
// CURRENTLY IMPLEMENTED — response
{ "hints": [ { "questionId": 102, "hintText": "…" } ] }
```

It sends no question type, no options, no reference answer, no topic and no
difficulty. With only the question text and the wrong option text, the AI has to
solve the question itself before it can hint. Everything below is
**RECOMMENDED**; none of it is implemented.

### 5.1 Current Assessment data available for AI

| Data | Where | Useful for AI? |
|---|---|---|
| Question text (localized) | `Questions.QuestionText` + `QuestionTranslations` | **Yes — required** |
| Question type | `Questions.QuestionType` (frozen in `QuizAttemptQuestions`) | **Yes — required** |
| Question image | `Questions.ImageUrl` (server-relative, e.g. `/uploads/lessons/x.png`) | No — unreachable path (see 5.8) |
| Image description | `Questions.ImageDescription`, `QuestionOptions.ImageDescription` (admin-only, designed for AI) | **Yes — required when an image carries meaning** |
| Options (localized text, image description, order) | `QuestionOptions` + `QuestionOptionTranslations` | **Yes — MCQ/TF** |
| Correct option | `QuizAttemptQuestions.CorrectOptionId` (frozen key); `QuestionOptions.IsCorrect` (live) | Hint quality — see 5.2 |
| Difficulty | frozen in `QuizAttemptQuestions.Difficulty` | Optional (tone/level) |
| Topic name/description (localized) | `Topics` + `TopicTranslations` | Optional (grounding) |
| Lesson title/content | Content module (`Lessons`, `LessonContents`) via `Quizzes.LessonId` | Optional, future (grounding) |
| Points | `Questions.Points` | Essay grading only (`maxPoints`) |
| Child's selected option | `QuizAttemptMistakes.SelectedOptionId` (wrong answers only) | **Yes — MCQ/TF** |
| Child's essay text | `QuizAttemptEssayAnswers.AnswerText` | **Yes — Essay** |
| Previous hints (same language) | `QuestionHints` (per mistake, sequence, language) | **Yes** — avoid repeating |
| Retry chain | `QuizAttempts.PreviousAttemptId` | Implicitly, via previous hints |
| Language | request (`ar`/`en`, normalized) | **Yes — required** |
| Score / stats | `QuizAttempts`, `UserTopicStats` | No |
| Explanation field | — none exists — | — |
| Essay model answer / rubric | — none exists — | — |
| Child's age | `Users.Users.Age` (Users module, nullable) | Optional, privacy review first |

### 5.2 Should the AI get the correct answer?

**Yes, for hints — as one `correctOptionId` taken from the attempt snapshot, never
as `isCorrect` flags.** (No child DTO has `isCorrect`; the flag lives only in admin
DTOs.)

* **Why it's needed:** the backend already knows the answer is wrong. Without the
  reference, the model must solve the question first. For image-described circuits
  and kids' electronics, a model that solves it wrong produces a hint that steers
  the child to a **wrong** answer, which is worse than no hint.
* **Risk:** the hint could give the answer away. The contract forbids that, and
  the backend verifies it: reject any hint that contains the correct option's text
  verbatim (treat it as "no hint").
* **Coupling/security:** the answer key leaves the system. For quiz content this
  is low sensitivity, but it requires a provider under a data-processing agreement.
  No child identifier is ever sent alongside it.

### 5.3 Proposed Assessment → AI request (v1)

One call per submission, batched (matching today's call site), for wrong
auto-graded answers:

```json
{
  "contractVersion": "1",
  "requestId": "0b6f7f0e-5a0e-4a53-9a57-8d1f0f2f4c11",
  "task": "Hints",
  "language": "ar",
  "items": [
    {
      "questionId": 102,
      "questionType": "MultipleChoice",
      "difficulty": "Medium",
      "topic": "المقاومة الكهربية",
      "question": { "text": "ما وحدة قياس المقاومة الكهربية؟", "image": null },
      "options": [
        { "optionId": 1004, "text": "الأوم",   "imageDescription": null },
        { "optionId": 1005, "text": "الفولت",  "imageDescription": null },
        { "optionId": 1006, "text": "الأمبير", "imageDescription": null }
      ],
      "studentAnswer": { "selectedOptionId": 1005 },
      "reference": { "correctOptionId": 1004 },
      "previousHints": ["فكّر في العالم الألماني."]
    }
  ]
}
```

| Field | Required? | Source |
|---|---|---|
| `contractVersion`, `requestId` (random per call, for tracing — not the attempt id), `task`, `language` | required | backend |
| `questionId` | required | echo key to map results back |
| `questionType` | required | snapshot |
| `question.text` | required unless `question.image.description` is present | localized |
| `question.image.description` | required when the question has an image | `ImageDescription` |
| `options[]` (id, text, imageDescription) | required for MCQ/TF, absent for Essay | localized |
| `studentAnswer.selectedOptionId` | MCQ/TF | mistake |
| `studentAnswer.text` | Essay | essay answer |
| `reference.correctOptionId` | MCQ/TF hints | snapshot |
| `previousHints[]` | optional (empty on first attempt) | `QuestionHints`, same language |
| `difficulty`, `topic` | optional | snapshot, `TopicTranslations` |

### 5.4 Per-type contract

**MultipleChoice** — as in 5.3. The AI writes one hint that nudges from the
selected option toward `correctOptionId` without naming it.

**TrueFalse** — the same shape; the two options are real rows, not a boolean:

```json
{
  "questionId": 104, "questionType": "TrueFalse", "difficulty": "Easy", "topic": "الدائرة الكهربية",
  "question": { "text": "المصباح يضيء بدون مصدر كهربي.", "image": null },
  "options": [ { "optionId": 1010, "text": "صح", "imageDescription": null },
               { "optionId": 1011, "text": "خطأ", "imageDescription": null } ],
  "studentAnswer": { "selectedOptionId": 1010 },
  "reference": { "correctOptionId": 1011 },
  "previousHints": []
}
```

With two options any hint points at the other one, so the hint must explain the
*concept* (why a lamp needs a source), not the choice.

**Essay** — a different task. The backend cannot know an essay is "wrong", so
there is nothing to hint; the AI's job is feedback and, optionally, a proposed
grade. Run it **asynchronously after the commit** (a job), never inline in Submit.

```json
{
  "contractVersion": "1", "requestId": "…", "task": "EssayEvaluation", "language": "ar",
  "items": [ {
    "questionId": 105, "questionType": "Essay", "difficulty": "Medium", "topic": "الدائرة الكهربية",
    "question": { "text": "اشرح بكلماتك ليه لازم نحط مقاومة مع الـ LED.", "image": null },
    "maxPoints": 3,
    "studentAnswer": { "text": "عشان التيار ما يبقاش كبير ويحرق الـ LED" },
    "reference": { "modelAnswer": "…", "rubric": ["يذكر تحديد التيار", "يذكر حماية الـ LED"] }
  } ]
}
```

`modelAnswer` and `rubric` **do not exist in the schema yet** (see 5.9).

**Image-based** (any type) — the description is mandatory; image bytes are optional:

```json
{
  "questionId": 110, "questionType": "MultipleChoice", "difficulty": "Hard", "topic": "توصيل الدوائر",
  "question": {
    "text": "أي دائرة موصلة صح؟",
    "image": { "description": "أربع دوائر A-D؛ الدائرة C فقط فيها مقاومة على التوالي مع الـ LED.",
               "content": null }
  },
  "options": [
    { "optionId": 1101, "text": null, "imageDescription": "LED متوصل مباشرة على البطارية بدون مقاومة." },
    { "optionId": 1102, "text": null, "imageDescription": "LED متوصل على التوالي مع مقاومة 220 أوم." }
  ],
  "studentAnswer": { "selectedOptionId": 1101 },
  "reference": { "correctOptionId": 1102 },
  "previousHints": []
}
```

`content` is `{ "mediaType": "image/png", "base64": "…" }` and is sent **only**
to a vision-capable provider, size-capped.

### 5.5 Proposed AI → Assessment response (v1)

```json
{
  "contractVersion": "1",
  "requestId": "0b6f7f0e-5a0e-4a53-9a57-8d1f0f2f4c11",
  "results": [
    { "questionId": 102, "status": "Ok",      "hint": "افتكر إن الوحدة اسمها على اسم عالم ألماني." },
    { "questionId": 110, "status": "Skipped", "hint": null, "reason": "InsufficientContext" }
  ]
}
```

* `hint` → `QuestionHints.HintText` (sequence per mistake + language) →
  `retryQuestions[].currentHint` now, and `currentHint` again in the retry attempt.
  It must be in `language`, ≤ ~300 characters, must not reveal the answer, and is
  validated by the backend (non-blank, asked-for id, no verbatim correct text).
* **Explanation** (why the right answer is right) is **not in v1**. It reveals the
  answer, so it can only be shown after the retry chain ends, and there is no
  column or response field for it yet.
* **Essay evaluation** (future `task`):
  `{ "questionId": 105, "status": "Ok", "proposedPoints": 2, "maxPoints": 3, "feedback": "…", "confidence": 0.82, "flags": [] }`.
  The backend writes `AwardedPoints/Feedback/GradedBy='Ai'/GradedAt/Status='Graded'`
  **only** above a confidence threshold with no flags; otherwise it stays `Pending`
  for a human.

| Term | Who | Status |
|---|---|---|
| Backend grading (MCQ/TF correctness, score) | backend only | implemented |
| AI hint | AI, optional | implemented client-side, no service |
| AI explanation | AI, optional | not designed into storage |
| AI grading (Essay) | AI proposes, backend decides | not implemented |

### 5.6 What the backend does (and must keep doing)

Authentication and ownership · attempt lifecycle, expiry, idempotency · answer
validation (option ∈ question ∈ attempt) · **MCQ/TF correctness against the
frozen key** · score · persistence of answers, score and stats · deciding which
questions get a hint · language resolution · validating AI output · saving
hints · the final say on any essay grade. **None of these may move to the AI.**

### 5.7 What the AI does

Writes an optional hint per wrong MCQ/TF answer. Later, optionally: essay
feedback plus a *proposed* score, and post-retry explanations. It never decides
correctness, never changes a committed score, and never sits on the path to the
commit. (Implemented for hints: commit → AI → attach or `hintsStatus`.)

### 5.8 Image handling requirements

1. **Is `imageUrl` enough?** No. It is a server-relative path
   (`/uploads/lessons/x.png`), meaningless outside this host.
2. **Can the AI reach it?** Only if the API host is public *and* the provider
   fetches URLs. Neither holds in development (localhost), and many text endpoints
   cannot consume images at all.
3. **What to send:** always the **structured description** (`ImageDescription`).
   Additionally, for a vision model only, **base64 bytes with a media type**, or a
   short-lived signed absolute URL. Never the raw relative path.
4. **If the AI cannot see the image**, it is missing everything the image shows.
   The description is the only carrier. With no description the item is skipped
   (the current code already skips and logs rather than sending empty text).
5. **Image-only question:** `question.text` may be null and
   `question.image.description` is then required. Today `QuestionText` is NOT NULL
   and the admin service rejects blank text, so a pure image question cannot exist
   yet; image-only **options** can, and must carry a description (DB CHECK).
6. **Empty text because the answer is an image option:** the answer is an
   `optionId`, and its meaning is that option's `imageDescription`. There is no
   image upload as an answer, and essay answers must be non-empty text.

### 5.9 What must never be sent

User id, attempt id or any internal attempt identifier (pinned by
`ActualRequest_CarriesNoUserIdentifierOfAnyKind`), names, emails, tokens, exact age ·
per-option `isCorrect` flags (one `correctOptionId` in hint/grading tasks only) ·
raw `ImageUrl` paths · `RowVersion`, `IsActive`, `CreatedAt`, scores, statistics ·
other children's answers · essay texts other than the item being evaluated.
Treat a child's essay text as personal data: minimize, no identifiers, and set
provider retention terms.

### 5.10 Missing before AI integration

1. An AI service, `Ai:HintsEndpoint`, an auth header (API key) and a contract
   version on the call.
2. The decision to send `reference.correctOptionId`, plus the backend anti-leak
   check.
3. Hint output limits (length) and moderation.
4. `topic` / `difficulty` / `options` / `questionType` in `GenerateHintQuestion`
   (all already available to the backend).
5. Essay: `ReferenceAnswer` + `Rubric` columns, an essay answer max length, an
   async grading job, a confidence policy, and a rule for whether graded essay
   points ever enter `ScorePercentage`.
6. Explanation: a storage column/table and a post-retry API surface.
7. Images: a rule (CHECK + service) that a question with `ImageUrl` has
   `ImageDescription`; an image-bytes pipeline if a vision model is chosen.
8. Optional: learner age band, after a privacy review.

### 5.11 Verdict

| Capability | Readiness |
|---|---|
| **MCQ/TF hints** | **Ready on the backend side.** The call site is failure-isolated, bounded, idempotent and persisted, and hints flow into the retry. Blocked only by the missing AI service and items 1–4 above. |
| **Image questions (hints)** | Partially ready: descriptions exist and are enforced for options, not for questions; no image-content path. |
| **Essay evaluation** | **Not ready:** no reference answer, no rubric, no async pipeline, no score policy. The storage columns for a grade already exist. |
| **Explanations** | Not ready: no storage, no response surface. |

---

## 6. Files

* **Changed:** `QuizAttemptService`, `UserTopicStatService`, `QuizService`,
  `QuestionOptionService`, `IQuizAttemptService`, `IQuizService`,
  `IUserTopicStatService`, `QuizAttemptResultDto`, `AssessmentBL/DependencyInjection`,
  `QuizAttemptConfiguration`, `QuizAttemptQuestionConfiguration`,
  `QuizAttemptController`, `ExceptionMiddleware`, `ApiResponseExamples`,
  `DefaultApiResponsesOperationFilter`, `Program`, `appsettings.json`, `ContentModule`,
  `FakeAiHintGenerator` (comments).
* **New:** `AssessmentSettings`, `HintStatuses`, `LocalizedQuestionQuery` (extracted),
  `LessonQuizResponseDto`, `LessonQuizController`, `ContentLanguageRequestExtensions`,
  `AbandonedQuizAttemptSweeper`, `GoneException`, `Shared/Content/ILessonAvailability`,
  `ContentBL/Services/LessonAvailabilityService`,
  `db/migrations/006_attempt_lifecycle_and_schema_alignment.sql`,
  `Tests/Assessment.Tests/SubmitLifecycleTests.cs`, and new/updated Flutter mocks in
  `docs/mocks/` (`mock_attempt_result*.json`, `mock_attempt_abandoned.json`,
  `mock_lesson_quiz*.json`, `mock_attempt_ai_failure.json` now 200, submit-result
  mocks gained `quizId`/`completedAt`/`hintsStatus`).
* **Config** (all optional, defaults shown): `Assessment:InProgressAttemptTimeoutMinutes=180`,
  `AbandonedAttemptSweepIntervalMinutes=15`, `AiHintTimeoutSeconds=15`.

---

## 7. Round 2 — placement test and the remaining fixes

**Deploy order:** `006` → `007` → code. The code maps `Assessment.UserPlacements`
and `UserTopicStats.RowVersion`, which only exist after 007.

**New:** the first-run placement test. See [PLACEMENT_QUIZ.md](PLACEMENT_QUIZ.md).

### Resolved from §2

| Was (§2) | Now |
|---|---|
| **H** Omitted MCQ/TF answers scored as correct (`{"mistakes":[]}` = 100%) | Every auto-graded question must be answered once; otherwise **400**, listing the missing ids. **Flutter contract change** — `mock_submit_attempt_request.json` now sends all answers. |
| **M** Option-count rules only checked at activation | A TrueFalse question can never get a third option; an option of a published question cannot be deleted if that leaves fewer than two |
| **M** Several active LessonQuiz per lesson | One active LessonQuiz per lesson and one active Placement quiz: service check (409) + `UQ_Quizzes_OneActive…` filtered unique indexes (007 §2, gated on existing duplicates) |
| **M** Quiz create did not check LessonId/LevelId exist | Checked through `ILessonAvailability` and the new `ILevelCatalog` (404) |
| **M** A quiz of an unpublished lesson could be started | Start → 404, the same rule as quiz-for-lesson |
| **M** `UserTopicStats` lost update under concurrent submits | `RowVersion` (007 §4). The losing submission is rolled back whole and gets a retryable 409. |
| **M** Essay answer unbounded | `Assessment:EssayAnswerMaxLength` (4000) → 400 |
| **M** Middleware logged 4xx as errors, client disconnects as 500 | 4xx → Warning; an aborted request is logged at Information and not answered; no write after the response started; the `" (Parameter 'dto')"` suffix no longer reaches the app |
| — Retry served questions an admin had since deactivated | Active questions only; 400 if none remain |
| — `GET /api/quiz-attempts/{id}` ordered by DisplayOrder | Ordered as served (the snapshot order). Identical for normal quizzes; required for placement, whose questions come from several quizzes. |

The question-type matrix footnote ² no longer applies to omitted answers: they
are rejected.

### Still open (decisions, or outside Assessment)

* `Points` are ignored by scoring (product decision).
* 403 rather than 404 for another user's attempt (module convention).
* Parent/Admin accounts can take regular quizzes. Placement itself is `Child` only.
* `InvalidOperationException` → 400 is still a broad mapping (it would need a
  dedicated business exception across every throw site).
* Only wrong selections are persisted.
* A question image does not require `ImageDescription`.
* `dbo.Languages` is left over after 006.
* Content module (not Assessment): `GET lessons/{id}` serves unpublished
  lessons, and `/uploads` is public.

---

## 8. Round 3 — AI integration implemented, every question required

**Deploy order:** `006` → `007` → `008` → code. **The spec for the AI service is
[AI_CONTRACT.md](AI_CONTRACT.md)**; it supersedes the proposal in §5.

| Area | Now |
|---|---|
| Unanswered questions | **Every** question — MCQ, TrueFalse and Essay — must be answered, or **400** listing all missing ones. A blank essay counts as unanswered. |
| Hints | v1 contract: question type, the full option list, the child's `selectedOptionId`, and `correctOptionId` from the frozen key. Never per-option `isCorrect`, never user/attempt ids. The backend drops any hint that names the correct answer (`HintSafety`), is over 400 chars, blank or duplicated. |
| Essays | AI evaluation of the question and the child's text. The AI proposes points, feedback and a confidence; the backend accepts only a valid, in-range, confident (≥ 0.8), unflagged proposal as the grade. Otherwise the essay waits for a person. It runs inline after the commit and in `EssayEvaluationWorker` (retry with backoff, max 5 attempts). The result carries `essayResults`. |
| Images | Description always; base64 bytes only with `Assessment:AiSendImageContent = true`, read through the Content module's `IMediaContentReader` (uploads folder only, 1 MB cap). |
| Auth to the AI | `Ai:ApiKey` sent in `Ai:ApiKeyHeaderName` when set |
| Not configured | No AI call, no warning spam. Hints are `Unavailable`; essays wait, with no attempts counted. |
| Round-2 review | Placement unique-constraint races → replay or 409; the resumed placement order is rebuilt from level + DisplayOrder; status checks InProgress before Unavailable; warning when a level without questions caps placement; `SET QUOTED_IDENTIFIER ON` in 006/007. |

**Round-3 review fixes:**
* **Evaluation runs:**
  * A run claims its essays atomically (`AiClaimId`), so no essay is evaluated twice.
  * A run that hits its deadline hands the essays back without using up an attempt.
  * Each AI request carries one attempt (one child), and the contract tells the
    AI to treat the answer as untrusted data.
* **Scoring and hints:**
  * The essay maximum is frozen on the row (`MaxPoints`, and a CHECK that
    awarded ≤ max).
  * The hint-leak check handles articles, stacked prefixes, Arabic-Indic
    digits, and TrueFalse verdicts.
  * Total image bytes per AI request are capped.
* **Retries:** the wait between retries grows with each attempt, stranded
  essays are closed, and unknown AI statuses are retried.
* **Migration 008:** it marks the pre-existing Pending backlog `NeedsReview`
  instead of sending it to the AI, and adds the filtered index the background
  evaluator uses.

Still open for AI: explanations (no storage), an essay model answer / rubric (no
schema), a review endpoint for `NeedsReview` / `Failed` essays, and a guard
against a child's own essay text steering their own grade (it is a prompt-level
defence only; essay grades never affect `scorePercentage` or statistics).

---

## 9. Addendum v1.1 (A–D)

**Deploy order is now:** `000` (empty DB only) *or* `006` → `007` → `008` → `009`,
then the code.

### A — Database-first, single schema
[`000_AssessmentSchema.sql`](../db/migrations/000_AssessmentSchema.sql) stands up
every `Assessment.*` object from an empty database and contains **no DDL for any
other schema**. Cross-module columns (`UserId`, `LessonId`, `LevelId`,
`PlacedLevelId`) are plain columns with no FK, and the script ends with two
queries that list what it created so the single-schema rule can be checked. It
equals the end state of migrations 002–009, so a database built from it needs
none of them.

⚠ Two **pre-existing** scripts do carry other-module DDL — not the schema script,
but worth knowing: `003_align_schema_to_code.sql` (an index on
`Users.RefreshTokens`, inserts into `dbo.Languages`) and
`005_password_reset_token_columns.sql` (entirely `Users.PasswordResetOTPs`).

### B — Every touch outside Assessment
All of them are Shared-contract implementations or registrations — no other
module gained a table, column, entity or endpoint, and none was regenerated.
Grep for `[Assessment-` to find them:

| Module | File | Tag | Why |
|---|---|---|---|
| Content | `LessonAvailabilityService` | `[Assessment-LessonQuiz]` | is a lesson published? |
| Content | `LevelCatalogService` | `[Assessment-Placement]` | levels in order, for placement |
| Content | `LocalMediaContentReader` | `[Assessment-AI]` | image bytes for a vision model |
| Content | `LocalImageStorageService` | `[Assessment-AI]` | uploads-folder constant made `internal` |
| Content | `ContentModule` | all three | registrations only |
| **Users** | `LearnerProfileService` + `UsersModule` | `[Assessment-AI]` | **the one Users touch**: read-only age lookup for `learnerContext.age` (§D.3) |

### C — AssessmentDA layout
Already `Entities/` + `Configurations/` (one per entity) + `Context/` with
`ApplyConfigurationsFromAssembly`, so the reconciliation keeps the existing
files. Phase 0 (run `000`, scaffold, diff against `DbContext.Model`) still has to
be run on a machine with SQL Server and the EF tools — neither exists here.

### D — Hint button
`POST /api/quiz-attempts/{attemptId}/questions/{questionId}/hint`, owner-only,
attempt must be in progress (409 once submitted, 410 once expired).

* **Escalation comes from the press count** (your choice): press 1 → soft nudge,
  press 2 → direct, press 3 → 409. **Consequence:** the "a wrong answer must
  exist first" precondition in §D.2/§D.4 is *not* enforced, because with bulk
  submit no wrong answer is on record mid-quiz. A child can reach the direct hint
  without answering. Adding a per-question answer log would restore it.
* **Level 2 relaxes nothing:** both levels face the verbatim check, the
  TrueFalse verdict check and the new similarity threshold. A rejected hint is
  not saved, returns `hintsStatus: "Partial"`, and does not use up a level.
* **Essay is supported:** no options, no correct answer, hint from the question
  wording alone; the leak checks do not apply (there is nothing to leak).
* **Storage:** one hint history per (attempt, question, language) with
  `AttemptNumber` for the button levels and NULL for post-submission hints, so
  the retry screen and result endpoint are unchanged (migration `009`).
* **Residual:** a press whose hint is rejected costs an AI call without consuming
  a level, so presses are not strictly bounded. A per-question press counter
  would cap it if that matters.

### Review fixes in this pass

| Was | Now |
|---|---|
| **`UQ_QuizAttempts_PreviousAttemptId` is a plain UNIQUE key on a NULLable column** — SQL Server allows one NULL, so a database accepts **one first attempt ever**; every later start fails with 2627 and surfaces as a 500. Present in the live schema too. | Filtered unique index (`WHERE PreviousAttemptId IS NOT NULL`) in `000`, in the EF model, and in **migration `010`** for existing databases |
| The Hint button worked on the **placement test**, inflating a placement that is written once and cannot be repeated | 409 — placement has no hints, matching the post-submission path |
| For an **image-only correct option** the leak checks compared against null text and passed everything | The option's description is resolved first and used for both checks; no description → no hint |
| The 2-press cap counted **per language**, so `ar, ar, en, en` gave four hints and two "direct" ones | Counted per question across all languages |
| `009`'s gate only printed, and later steps still ran against ungated rows | It stops the script (`RAISERROR` + `NOEXEC`) |
| `009`'s cascade fix was gated only on the column's nullability, so a re-run after a partial failure kept the cascade and then failed with 1785 | Gated on either symptom |

One behaviour change to know: deleting a **single mistake row** now fails while a
hint references it (the cascade moved to the attempt's question). Nothing in the
app deletes mistakes.
