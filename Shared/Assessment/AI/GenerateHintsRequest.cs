namespace Shared.Assessment.AI;

public sealed class GenerateHintsRequest
{
    public IReadOnlyList<GenerateHintQuestion> Questions { get; init; } = [];
}

public sealed class GenerateHintQuestion
{
    public int QuestionId { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public string WrongOptionText { get; init; } = string.Empty;
    public IReadOnlyList<string> PreviousHints { get; init; } = [];
}
