using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record StepCompletedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepId StepId,
    StepExecutionId StepExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public StepCompletedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepId stepId,
        StepExecutionId stepExecutionId)
        : this(workflowExecutionId, stepId, stepExecutionId, DateTime.UtcNow) { }
}