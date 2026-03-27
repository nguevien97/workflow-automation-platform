using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Lightweight structural description of one step, embedded in
/// <see cref="WorkflowDefinitionSnapshot"/> so the execution engine can
/// navigate the DAG without the original WorkflowDefinition aggregate.
/// </summary>
public abstract class StepDefinitionInfo : ValueObject
{
    public StepId StepId { get; }
    public StepType StepType { get; }
    public StepId? NextStepId { get; }

    protected StepDefinitionInfo(
        StepId stepId,
        StepType stepType,
        StepId? nextStepId = null)
    {
        if (stepId == default)
            throw new ArgumentException("Step ID must not be empty.", nameof(stepId));

        StepId = stepId;
        StepType = stepType;
        NextStepId = nextStepId;
    }

    // ── ValueObject equality ─────────────────────────────────────────────────

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StepId;
        yield return StepType;
        yield return NextStepId;
    }
}

