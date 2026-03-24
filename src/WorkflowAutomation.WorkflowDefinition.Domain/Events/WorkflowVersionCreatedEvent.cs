using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Events;

public sealed record WorkflowVersionCreatedEvent(
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    DateTime OccurredOn) : IDomainEvent
{
    public WorkflowVersionCreatedEvent(WorkflowId workflowId, WorkflowVersionId workflowVersionId)
        : this(workflowId, workflowVersionId, DateTime.UtcNow) { }
}
