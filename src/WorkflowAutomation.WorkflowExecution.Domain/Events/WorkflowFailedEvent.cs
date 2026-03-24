using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record WorkflowFailedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    string Error,
    DateTime OccurredOn) : IDomainEvent
{
    public WorkflowFailedEvent(WorkflowExecutionId workflowExecutionId, string error)
        : this(workflowExecutionId, error, DateTime.UtcNow) { }
}
