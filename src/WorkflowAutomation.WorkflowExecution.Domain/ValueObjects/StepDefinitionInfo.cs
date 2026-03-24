using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Lightweight structural description of one step, embedded in
/// <see cref="WorkflowDefinitionSnapshot"/> so the execution engine can
/// navigate the DAG without the original WorkflowDefinition aggregate.
/// </summary>
public sealed class StepDefinitionInfo : ValueObject
{
    private readonly Dictionary<string, IReadOnlyList<StepId>>? _conditionBranches;

    public StepId StepId { get; }
    public StepType StepType { get; }

    /// <summary>
    /// Non-null only for <see cref="StepType.Condition"/> steps.
    /// Maps each branch value to the ordered list of step IDs in that branch.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<StepId>>? ConditionBranches =>
        _conditionBranches?.AsReadOnly();

    /// <summary>Creates a non-condition step info.</summary>
    public StepDefinitionInfo(StepId stepId, StepType stepType)
    {
        if (stepType == StepType.Condition)
            throw new ArgumentException(
                "Use the overload that accepts condition branches for Condition steps.", nameof(stepType));

        StepId = stepId;
        StepType = stepType;
    }

    /// <summary>Creates a Condition step info with its branches.</summary>
    public StepDefinitionInfo(
        StepId stepId,
        Dictionary<string, IReadOnlyList<StepId>> conditionBranches)
    {
        ArgumentNullException.ThrowIfNull(conditionBranches);
        if (conditionBranches.Count == 0)
            throw new ArgumentException("A condition step must have at least one branch.", nameof(conditionBranches));

        StepId = stepId;
        StepType = StepType.Condition;
        _conditionBranches = new Dictionary<string, IReadOnlyList<StepId>>(conditionBranches);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StepId;
        yield return StepType;

        if (_conditionBranches is null) yield break;

        foreach (var kvp in _conditionBranches.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            yield return kvp.Key;
            foreach (var sid in kvp.Value)
                yield return sid;
        }
    }
}
