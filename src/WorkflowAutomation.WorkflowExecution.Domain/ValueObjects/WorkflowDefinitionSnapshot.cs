using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Frozen copy of a workflow version's structure embedded inside
/// <see cref="Aggregates.WorkflowExecution"/> so running executions are
/// completely isolated from subsequent workflow edits.
/// All steps (top-level, branch, parallel branch, loop body) are stored
/// in a single flat list; graph structure is defined by ID references.
/// </summary>
public sealed class WorkflowDefinitionSnapshot : ValueObject
{
    private readonly List<StepDefinitionInfo> _allSteps;
    private readonly Dictionary<StepId, StepDefinitionInfo> _stepsById;
    private readonly Dictionary<string, StepDefinitionInfo> _stepsByName;

    public WorkflowDefinitionSnapshot(List<StepDefinitionInfo> allSteps)
    {
        ArgumentNullException.ThrowIfNull(allSteps);
        if (allSteps.Count == 0)
            throw new ArgumentException("Snapshot must contain at least one step.", nameof(allSteps));

        _allSteps = allSteps;
        _stepsById = [];
        _stepsByName = [];
        foreach (var step in _allSteps)
        {
            _stepsById[step.StepId] = step;
            _stepsByName[step.Name] = step;
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public StepDefinitionInfo GetStepInfo(StepId stepId)
    {
        if (_stepsById.TryGetValue(stepId, out var info))
            return info;
        throw new KeyNotFoundException($"Step ID '{stepId}' not found in workflow snapshot.");
    }

    public StepDefinitionInfo? GetStepInfoByName(string stepName)
    {
        return _stepsByName.TryGetValue(stepName, out var info) ? info : null;
    }

    /// <summary>
    /// Walks the graph to find the <see cref="ParallelStepInfo"/> that owns
    /// the branch containing <paramref name="branchStepId"/>.
    /// Returns null if the step is not inside a parallel branch (i.e. it's
    /// the last step of the top-level chain or a condition branch at top level).
    /// </summary>
    public ParallelStepInfo? FindOwningParallelStep(StepId branchStepId)
    {
        var parallelSteps = _allSteps.OfType<ParallelStepInfo>();
        foreach (var parallel in parallelSteps)
        {
            foreach (var entryId in parallel.BranchEntryStepIds)
            {
                if (LocalPathContainsStep(entryId, branchStepId))
                    return parallel;
            }
        }
        return null;
    }

    /// <summary>
    /// Walks the graph to find the <see cref="ConditionStepInfo"/> that owns
    /// the branch containing <paramref name="branchStepId"/>.
    /// Returns null if the step is not inside a condition branch.
    /// </summary>
    public ConditionStepInfo? FindOwningConditionStep(StepId branchStepId)
    {
        var conditionSteps = _allSteps.OfType<ConditionStepInfo>();
        foreach (var condition in conditionSteps)
        {
            foreach (var rule in condition.Rules)
            {
                if (LocalPathContainsStep(rule.TargetStepId, branchStepId))
                    return condition;
            }

            if (condition.FallbackStepId.HasValue
                && LocalPathContainsStep(condition.FallbackStepId.Value, branchStepId))
            {
                return condition;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether all branches of a parallel step have a completed
    /// terminal step (a step with NextStepId == null) in the given set
    /// of completed step IDs.
    /// </summary>
    public bool AreAllParallelBranchesCompleted(
        ParallelStepInfo parallelStep,
        HashSet<StepId> completedStepIds)
    {
        foreach (var entryId in parallelStep.BranchEntryStepIds)
        {
            var terminalStepId = FindBranchTerminalStep(entryId);
            if (terminalStepId is null || !completedStepIds.Contains(terminalStepId.Value))
                return false;
        }
        return true;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Walks a local path from <paramref name="entryStepId"/> following
    /// only <c>NextStepId</c> links. Does <b>not</b> recurse into nested
    /// scopes (parallel branches, condition branches, loop bodies).
    /// This ensures each owner-finding method returns the <b>direct</b>
    /// (innermost) structural parent rather than a transitive ancestor.
    /// </summary>
    private bool LocalPathContainsStep(StepId entryStepId, StepId targetStepId)
    {
        StepId? currentId = entryStepId;
        while (currentId.HasValue)
        {
            if (currentId.Value == targetStepId)
                return true;

            var current = GetStepInfo(currentId.Value);
            currentId = current.NextStepId;
        }
        return false;
    }

    /// <summary>
    /// Follows a branch chain to find the terminal step (NextStepId == null).
    /// </summary>
    private StepId? FindBranchTerminalStep(StepId entryStepId)
    {
        StepId? currentId = entryStepId;
        StepId? lastId = null;
        while (currentId.HasValue)
        {
            lastId = currentId;
            var current = GetStepInfo(currentId.Value);
            currentId = current.NextStepId;
        }
        return lastId;
    }

    // ── ValueObject equality ─────────────────────────────────────────────────

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var info in _allSteps.OrderBy(s => s.StepId.Value))
            yield return info;
    }
}