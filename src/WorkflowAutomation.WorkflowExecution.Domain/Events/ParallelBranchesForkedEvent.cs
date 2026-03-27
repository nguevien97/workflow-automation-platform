using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ParallelBranchesForkedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepId ParallelStepId,
    IReadOnlyList<StepId> BranchEntryStepIds,
    DateTime OccurredOn) : IDomainEvent
{
    public ParallelBranchesForkedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepId parallelStepId,
        IReadOnlyList<StepId> branchEntryStepIds)
        : this(workflowExecutionId, parallelStepId, branchEntryStepIds, DateTime.UtcNow) { }
}