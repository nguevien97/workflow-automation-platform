using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record WorkflowStartedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public WorkflowStartedEvent(WorkflowExecutionId workflowExecutionId)
        : this(workflowExecutionId, DateTime.UtcNow) { }
}
