# First-run placement test

_Assessment module · 2026-09-11 · requires `db/migrations/006` then `007`._

A new learner takes a short test right after registering. It decides which
Content-module **level** they should start at.

## How it decides

The placement quiz owns **no questions of its own**. It samples the content that
already defines "what a child at level N must know": each level's active
**LevelAssessment** quiz.

1. Take the levels in learning order (`LearningContent.Levels.Order`).
2. From each level's newest active LevelAssessment quiz, take the first
   `Assessment:PlacementQuestionsPerLevel` (default **4**) active,
   auto-graded (MCQ / True-False), answerable questions, in that quiz's
   `DisplayOrder`. Essays are never used; they cannot be scored on submit.
3. Serve them as one attempt, easiest level first, numbered 1..n.
4. On submit, a level is **mastered** when the child got at least
   `Assessment:PlacementPassPercentage` (default **75** — 3 of 4) of its
   questions right.
5. The child is placed at the **first level that is not mastered**. A level with
   no placement questions cannot be shown mastered, so the test never skips a
   child past a level it could not check. Mastering every level places them at
   the last one.

The rule is `PlacementEngine.Decide` (pure, unit-tested in `PlacementTests.cs`).

## App flow

```
register / login
  └─ GET  /api/placement                    (Child role only)
       ├─ Required     → new learner: show the placement test now
       ├─ Optional     → learner already has quiz history: may offer it
       ├─ InProgress   → an open test exists: start resumes it
       ├─ Completed    → result.levelId is where to start them
       └─ Unavailable  → nothing to place against: skip placement
  └─ POST /api/placement/start              → QuizAttemptResponseDto (resumes if open)
  └─ POST /api/quiz-attempts/{id}/submit    → QuizAttemptResultDto with "placement"
```

* Submit needs **an answer for every question** (true of every quiz now).
* The result carries `placement` (level id, title, order, per-level breakdown).
  `retryQuestions` is empty and `hintsStatus` is `NotRequired`: a placement test
  is never retried, so the AI is not called.
* Lost the submit response? `GET /api/quiz-attempts/{id}/result` and
  `GET /api/placement` both return the stored placement.
* A learner is placed **once**. Starting or submitting another placement → 409.
* An open placement attempt expires like any attempt
  (`InProgressAttemptTimeoutMinutes`); after that, start creates a fresh one.

Mocks: `docs/mocks/mock_placement_status_required.json`,
`mock_placement_status_completed.json`, `mock_submit_placement_result.json`.

## Admin setup

1. For each level, create a quiz with `QuizType = "LevelAssessment"` and its
   `LevelId`, and give it at least 4 active MCQ/True-False questions.
2. Create **one** quiz with `QuizType = "Placement"` (no LevelId, no LessonId).
   Its title and description are what the intro screen shows. Only one may be
   active (409 otherwise). Adding questions to it directly is rejected; they
   belong on the level assessments.
3. Check readiness with the last query in `007` §5: every level should show an
   assessment quiz and at least 4 usable questions.

## Where the result lives

`Assessment.UserPlacements`: one row per user (`UserId` unique), with
`PlacedLevelId`, the overall score, the threshold used, and the placing attempt.
It is written in the same transaction that completes the attempt, so a saved
placement always has its answers, and vice versa.

There is no per-user "current level" anywhere else in the system (the Content
module has no user progress). The app reads `placement.levelId` and opens that
level. If the Content module later gains user progress, it should read this
table through a Shared contract, as `ILessonAvailability` / `ILevelCatalog` do in
the other direction.

## Configuration (all optional)

| Key | Default | Meaning |
|---|---|---|
| `Assessment:PlacementQuestionsPerLevel` | 4 | questions sampled per level (1–20) |
| `Assessment:PlacementPassPercentage` | 75 | mastery threshold per level (1–100) |
