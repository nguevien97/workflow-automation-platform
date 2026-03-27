using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ParallelBranchesMergedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepId ParallelStepId,
    DateTime OccurredOn) : IDomainEvent
{
    public ParallelBranchesMergedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepId parallelStepId)
        : this(workflowExecutionId, parallelStepId, DateTime.UtcNow) { }
}