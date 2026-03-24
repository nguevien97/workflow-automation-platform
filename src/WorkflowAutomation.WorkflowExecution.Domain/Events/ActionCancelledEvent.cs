using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionCancelledEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionCancelledEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId)
        : this(workflowExecutionId, stepExecutionId, DateTime.UtcNow) { }
}
