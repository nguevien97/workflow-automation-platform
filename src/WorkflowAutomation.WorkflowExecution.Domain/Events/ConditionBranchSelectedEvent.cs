using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ConditionBranchSelectedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepId ConditionStepId,
    StepId SelectedBranchEntryStepId,
    DateTime OccurredOn) : IDomainEvent
{
    public ConditionBranchSelectedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepId conditionStepId,
        StepId selectedBranchEntryStepId)
        : this(workflowExecutionId, conditionStepId, selectedBranchEntryStepId, DateTime.UtcNow) { }
}