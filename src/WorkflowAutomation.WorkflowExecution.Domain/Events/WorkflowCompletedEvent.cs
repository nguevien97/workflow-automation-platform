using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record WorkflowCompletedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public WorkflowCompletedEvent(WorkflowExecutionId workflowExecutionId)
        : this(workflowExecutionId, DateTime.UtcNow) { }
}
