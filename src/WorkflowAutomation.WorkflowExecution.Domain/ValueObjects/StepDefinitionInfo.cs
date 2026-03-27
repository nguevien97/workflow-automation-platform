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
    public string Name { get; }
    public StepType StepType { get; }
    public StepId? NextStepId { get; }

    protected StepDefinitionInfo(
        StepId stepId,
        string name,
        StepType stepType,
        StepId? nextStepId = null)
    {
        if (stepId == default)
            throw new ArgumentException("Step ID must not be empty.", nameof(stepId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        StepId = stepId;
        Name = name;
        StepType = stepType;
        NextStepId = nextStepId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StepId;
        yield return Name;
        yield return StepType;
        yield return NextStepId;
    }
}