using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Events;

public sealed class WorkflowDefinitionCreatedEvent(
    WorkflowVersionId workflowVersionId,
    WorkflowId workflowId) : IDomainEvent
{
    public WorkflowVersionId WorkflowVersionId { get; } = workflowVersionId;
    public WorkflowId WorkflowId { get; } = workflowId;
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
