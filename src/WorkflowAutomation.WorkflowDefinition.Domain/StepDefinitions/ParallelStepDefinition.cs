using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class ParallelStepDefinition : StepDefinition
{
    public IReadOnlyList<StepId> BranchEntryStepIds { get; }

    public ParallelStepDefinition(
        StepId id,
        string name,
        IReadOnlyList<StepId> branchEntryStepIds,
        StepId? nextStepId = null)
        : base(id, StepType.Parallel, name, nextStepId)
    {
        ArgumentNullException.ThrowIfNull(branchEntryStepIds);
        if (branchEntryStepIds.Count < 2)
            throw new ArgumentException("At least two branch entry steps are required for a Parallel step.", nameof(branchEntryStepIds));
        BranchEntryStepIds = branchEntryStepIds;
    }
}