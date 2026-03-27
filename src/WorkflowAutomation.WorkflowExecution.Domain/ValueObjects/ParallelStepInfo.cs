using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class ParallelStepInfo : StepDefinitionInfo
{
    public IReadOnlyList<StepId> BranchEntryStepIds { get; }

    public ParallelStepInfo(
        StepId stepId,
        string name,
        IReadOnlyList<StepId> branchEntryStepIds,
        StepId? nextStepId = null)
        : base(stepId, name, StepType.Parallel, nextStepId)
    {
        ArgumentNullException.ThrowIfNull(branchEntryStepIds);
        if (branchEntryStepIds.Count < 2)
            throw new ArgumentException(
                "At least two branch entry steps are required for a Parallel step.",
                nameof(branchEntryStepIds));

        BranchEntryStepIds = branchEntryStepIds;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var c in base.GetEqualityComponents()) yield return c;
        foreach (var id in BranchEntryStepIds) yield return id;
    }
}
