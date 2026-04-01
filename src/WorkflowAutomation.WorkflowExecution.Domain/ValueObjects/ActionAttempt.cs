namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Mutable record of one integration dispatch attempt within an ActionExecution.
/// Tracks timing, outcome, and correlation data for audit and stale-response protection.
///
/// Not a <c>ValueObject</c> because its state is mutated after construction
/// (when the attempt completes). Owned exclusively by <c>ActionExecution</c>
/// and never compared by value.
/// </summary>
public sealed class ActionAttempt
{
    public int AttemptNumber { get; }
    public DateTime StartedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public bool Succeeded { get; private set; }

    public ActionAttempt(int attemptNumber, DateTime startedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptNumber, 1);

        AttemptNumber = attemptNumber;
        StartedAtUtc = startedAtUtc;
    }

    internal void RecordSuccess(DateTime completedAtUtc)
    {
        CompletedAtUtc = completedAtUtc;
        Succeeded = true;
    }

    internal void RecordFailure(DateTime completedAtUtc, string error)
    {
        CompletedAtUtc = completedAtUtc;
        Error = error;
        Succeeded = false;
    }
}
