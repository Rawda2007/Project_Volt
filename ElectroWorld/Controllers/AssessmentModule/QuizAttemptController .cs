using AssessmentBL.DTOs.QuizAttempt;
using AssessmentBL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Users;

namespace ElectroWorld.Api.Controllers;

[ApiController]
[Route("api/quiz-attempts")]
[Authorize]
public class QuizAttemptController : ControllerBase
{
    private readonly IQuizAttemptService _quizAttemptService;

    public QuizAttemptController(IQuizAttemptService quizAttemptService)
    {
        _quizAttemptService = quizAttemptService;
    }

    // First attempt: only quizId is supplied.
    // Retry attempt: previousAttemptId is also supplied (service resolves
    // which questions to serve and attaches the latest hints).
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuizAttemptResponseDto>> Start(
        [FromQuery] int quizId,
        [FromQuery] long? previousAttemptId,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var attempt = await _quizAttemptService.StartAsync(quizId, userId, previousAttemptId, ct);
        return CreatedAtAction(nameof(GetById), new { attemptId = attempt.AttemptId }, attempt);
    }

    [HttpPost("{attemptId:long}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuizAttemptResultDto>> Submit(
        long attemptId,
        [FromBody] SubmitQuizAttemptDto dto,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _quizAttemptService.SubmitAsync(attemptId, userId, dto, ct);
        return Ok(result);
    }

    [HttpGet("{attemptId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuizAttemptResponseDto>> GetById(long attemptId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var attempt = await _quizAttemptService.GetByIdAsync(attemptId, userId, ct);
        return Ok(attempt);
    }
}