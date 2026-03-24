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
    private readonly List<StepId> _topLevelStepIds;

    // All steps (top-level + branch steps) keyed for fast lookup.
    private readonly Dictionary<StepId, StepDefinitionInfo> _allSteps;

    // Maps every branch step to the condition step that owns it.
    private readonly Dictionary<StepId, StepId> _branchStepToCondition;

    // Maps every branch step to its next sibling within the same branch (null if last).
    private readonly Dictionary<StepId, StepId?> _branchStepNext;

    public WorkflowVersionId WorkflowVersionId { get; }
    public IReadOnlyList<StepId> TopLevelStepIds => _topLevelStepIds.AsReadOnly();

    public WorkflowDefinitionSnapshot(
        WorkflowVersionId workflowVersionId,
        IEnumerable<StepDefinitionInfo> topLevelSteps)
    {
        ArgumentNullException.ThrowIfNull(topLevelSteps);

        WorkflowVersionId = workflowVersionId;
        _topLevelStepIds = [];
        _allSteps = [];
        _branchStepToCondition = [];
        _branchStepNext = [];

        foreach (var step in topLevelSteps)
        {
            _topLevelStepIds.Add(step.StepId);
            _allSteps[step.StepId] = step;

            if (step.StepType == StepType.Condition && step.ConditionBranches is not null)
            {
                foreach (var (_, branchSteps) in step.ConditionBranches)
                {
                    for (var i = 0; i < branchSteps.Count; i++)
                    {
                        var branchStepId = branchSteps[i];
                        _branchStepToCondition[branchStepId] = step.StepId;
                        _branchStepNext[branchStepId] = i + 1 < branchSteps.Count
                            ? branchSteps[i + 1]
                            : null;
                    }
                }
            }
        }

        if (_topLevelStepIds.Count == 0)
            throw new ArgumentException(
                "Workflow definition snapshot must contain at least one step.", nameof(topLevelSteps));
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public StepId GetFirstStepId() => _topLevelStepIds[0];

    public StepDefinitionInfo GetStepInfo(StepId stepId)
    {
        if (!_allSteps.TryGetValue(stepId, out var info))
            throw new InvalidOperationException($"Step '{stepId}' not found in workflow snapshot.");
        return info;
    }

    public StepType GetStepType(StepId stepId) => GetStepInfo(stepId).StepType;

    /// <summary>
    /// Returns the IDs of the first step in every branch of a condition step.
    /// Used to start parallel branch execution.
    /// </summary>
    public IReadOnlyDictionary<string, StepId> GetConditionBranchEntries(StepId conditionStepId)
    {
        var info = GetStepInfo(conditionStepId);
        if (info.StepType != StepType.Condition || info.ConditionBranches is null)
            throw new InvalidOperationException(
                $"Step '{conditionStepId}' is not a condition step.");

        return info.ConditionBranches
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value[0]);
    }

    /// <summary>
    /// Given a completed step, returns the step that should start next
    /// in the same context (top-level list or branch list).
    /// Returns <c>null</c> when the completed step is the last in its
    /// context — the caller must determine whether to advance past the
    /// owning condition or complete the workflow.
    /// </summary>
    public StepId? GetNextStepIdInContext(StepId completedStepId)
    {
        // Branch step → next sibling in branch (or null = branch done)
        if (_branchStepNext.TryGetValue(completedStepId, out var branchNext))
            return branchNext;

        // Top-level step → next in top-level list
        var idx = _topLevelStepIds.IndexOf(completedStepId);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Step '{completedStepId}' not found in snapshot.");

        var nextIdx = idx + 1;
        return nextIdx < _topLevelStepIds.Count ? _topLevelStepIds[nextIdx] : null;
    }

    /// <summary>
    /// Returns the step that follows the given condition step in the
    /// top-level list (i.e. the merge point).  Returns <c>null</c> if
    /// the condition is the last top-level step.
    /// </summary>
    public StepId? GetStepAfterCondition(StepId conditionStepId)
    {
        var idx = _topLevelStepIds.IndexOf(conditionStepId);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Condition step '{conditionStepId}' not found in top-level list.");

        var nextIdx = idx + 1;
        return nextIdx < _topLevelStepIds.Count ? _topLevelStepIds[nextIdx] : null;
    }

    /// <summary>
    /// If the given step belongs to a condition branch, returns the
    /// owning condition step ID.  Returns <c>null</c> for top-level steps.
    /// </summary>
    public StepId? GetOwningConditionStepId(StepId stepId) =>
        _branchStepToCondition.TryGetValue(stepId, out var condId) ? condId : null;

    // ── ValueObject equality ─────────────────────────────────────────────────

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return WorkflowVersionId;
        foreach (var sid in _topLevelStepIds)
            yield return sid;
        foreach (var info in _allSteps.Values.OrderBy(s => s.StepId.Value))
            yield return info;
    }
}
