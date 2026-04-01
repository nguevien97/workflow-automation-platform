using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Immutable record of one integration dispatch attempt within an ActionExecution.
/// Tracks timing, outcome, and correlation data for audit and stale-response protection.
/// </summary>
public sealed class ActionAttempt : ValueObject
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

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AttemptNumber;
        yield return StartedAtUtc;
        yield return CompletedAtUtc;
        yield return Error;
        yield return Succeeded;
    }
}
