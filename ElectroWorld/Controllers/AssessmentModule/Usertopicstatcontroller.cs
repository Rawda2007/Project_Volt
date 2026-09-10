using AssessmentBL.DTOs.UserTopicStat;
using AssessmentBL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Users;

namespace ElectroWorld.Api.Controllers;

[ApiController]
[Route("api/user-topic-stats")]
[Authorize]
public class UserTopicStatController : ControllerBase
{
    private readonly IUserTopicStatService _userTopicStatService;

    public UserTopicStatController(IUserTopicStatService userTopicStatService)
    {
        _userTopicStatService = userTopicStatService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserTopicStatResponseDto>>> GetMine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var stats = await _userTopicStatService.GetByUserIdAsync(userId, ct);
        return Ok(stats);
    }

    [HttpGet("{topicId:int}/{difficulty}")]
    public async Task<ActionResult<UserTopicStatResponseDto>> GetByTopicAndDifficulty(int topicId, string difficulty, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var stat = await _userTopicStatService.GetByTopicAndDifficultyAsync(userId, topicId, difficulty, ct);
        return stat is null ? NotFound() : Ok(stat);
    }

    // See "Potential Issues Found": this recalculation path is already
    // triggered internally by QuizAttemptService.SubmitAsync on every
    // successful submit. Exposed here only because it is a public method
    // on IUserTopicStatService.
    [HttpPost("recalculate")]
    public async Task<IActionResult> UpdateAfterQuizAttempt([FromQuery] long quizAttemptId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _userTopicStatService.UpdateAfterQuizAttemptAsync(quizAttemptId, userId, ct);
        return NoContent();
    }
}