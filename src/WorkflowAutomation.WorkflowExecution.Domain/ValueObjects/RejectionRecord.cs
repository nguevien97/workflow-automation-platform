using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Tracks a single rejection event in the workflow's rejection history.
/// Mutable (supersession can be applied later), so this is intentionally
/// not a <c>ValueObject</c>.
/// </summary>
public sealed class RejectionRecord
{
    public StepId ReviewStepId { get; }
    public StepId TargetStepId { get; }
    public string Reason { get; }
    public DateTime OccurredOn { get; }
    public IReadOnlyList<InvalidatedStepExecution> InvalidatedSteps { get; }
    public StepId? SupersededByReviewStepId { get; private set; }

    public RejectionRecord(
        StepId reviewStepId,
        StepId targetStepId,
        string reason,
        List<InvalidatedStepExecution> invalidatedSteps)
    {
        ReviewStepId = reviewStepId;
        TargetStepId = targetStepId;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
        InvalidatedSteps = invalidatedSteps;
        SupersededByReviewStepId = null;
    }

    public void MarkSupersededBy(StepId reviewStepId)
    {
        SupersededByReviewStepId = reviewStepId;
    }
}

public sealed class InvalidatedStepExecution : ValueObject
{
    public StepExecutionId StepExecutionId { get; }
    public StepId StepId { get; }
    public StepInput? Input { get; }
    public StepOutput? Output { get; }

    public InvalidatedStepExecution(
        StepExecutionId stepExecutionId,
        StepId stepId,
        StepInput? input,
        StepOutput? output)
    {
        StepExecutionId = stepExecutionId;
        StepId = stepId;
        Input = input;
        Output = output;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StepExecutionId;
        yield return StepId;
        yield return Input;
        yield return Output;
    }
}
