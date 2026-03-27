using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Frozen copy of a workflow version's structure embedded inside
/// <see cref="Aggregates.WorkflowExecution"/> so running executions are
/// completely isolated from subsequent workflow edits.
/// </summary>
public sealed class WorkflowDefinitionSnapshot : ValueObject
{
    // Top-level step order (does NOT include steps nested inside condition branches).
    private readonly List<StepDefinitionInfo> _allSteps;
    private readonly Dictionary<StepId, StepDefinitionInfo> AllStepsById;
       

    public WorkflowDefinitionSnapshot(
        List<StepDefinitionInfo> allSteps)
    {
        ArgumentNullException.ThrowIfNull(allSteps);

        _allSteps = allSteps;
        AllStepsById = [];
        foreach (var step in _allSteps)
        {
            AllStepsById[step.StepId] = step;
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public StepDefinitionInfo GetStepInfo(StepId stepId)
    {
        if (AllStepsById.TryGetValue(stepId, out var info))
        {
            return info;
        }
        throw new KeyNotFoundException($"Step ID {stepId} not found in workflow snapshot.");
    }

    // ── ValueObject equality ─────────────────────────────────────────────────

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var info in _allSteps.OrderBy(s => s.StepId.Value))
            yield return info;
    }
}
