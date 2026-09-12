namespace Shared.Assessment.AI;

/// <summary>
/// Essay answers to evaluate. The AI proposes a score and feedback; the backend
/// decides whether that proposal becomes the grade. Carries no user, attempt or
/// account identifier of any kind.
/// </summary>
public sealed class EssayEvaluationRequest
{
    public string ContractVersion { get; init; } = AiContract.Version;

    /// <summary>Random per call. Never a database id.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Always "EssayEvaluation".</summary>
    public string Task { get; init; } = AiTasks.EssayEvaluation;

    /// <summary>The language the child answered in; feedback must be in it too.</summary>
    public string Language { get; init; } = "ar";

    public IReadOnlyList<EssayRequestItem> Items { get; init; } = [];
}

public sealed class EssayRequestItem
{
    /// <summary>
    /// Opaque key for this item within this request ("1", "2", …), echoed back.
    /// Not a database id — one request may hold answers of different children.
    /// </summary>
    public string ItemId { get; init; } = string.Empty;

    public string? Difficulty { get; init; }

    public string? Topic { get; init; }

    public AiQuestion Question { get; init; } = new();

    /// <summary>The most the answer can earn. A proposal above it is rejected.</summary>
    public int MaxPoints { get; init; }

    public EssayStudentAnswer StudentAnswer { get; init; } = new();
}

public sealed class EssayStudentAnswer
{
    public string Text { get; init; } = string.Empty;
}

public sealed class EssayEvaluationResponse
{
    public string? ContractVersion { get; init; }

    public string? RequestId { get; init; }

    public IReadOnlyList<EssayEvaluationResult> Results { get; init; } = [];
}

public sealed class EssayEvaluationResult
{
    public string? ItemId { get; init; }

    /// <summary>Ok | Skipped (the AI declined — a person grades it). Missing = Ok.</summary>
    public string? Status { get; init; }

    /// <summary>0 … MaxPoints.</summary>
    public int? ProposedPoints { get; init; }

    /// <summary>Child-facing feedback, in the request language.</summary>
    public string? Feedback { get; init; }

    /// <summary>0.00 … 1.00.</summary>
    public decimal? Confidence { get; init; }

    /// <summary>
    /// Anything a person should look at: "OffTopic", "Unsafe", "PersonalData",
    /// "Unclear"… Any flag sends the answer to review instead of auto-grading.
    /// </summary>
    public IReadOnlyList<string>? Flags { get; init; }
}

public interface IAiEssayEvaluator
{
    /// <summary>False while no essay endpoint is configured — nothing is attempted or counted.</summary>
    bool IsConfigured { get; }

    Task<EssayEvaluationResponse> EvaluateAsync(
        EssayEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
