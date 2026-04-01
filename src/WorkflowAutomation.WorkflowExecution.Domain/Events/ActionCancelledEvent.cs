using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionCancelledEvent(
    ActionExecutionId ActionExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionCancelledEvent(
        ActionExecutionId actionExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId)
        : this(actionExecutionId, workflowExecutionId, stepExecutionId, DateTime.UtcNow) { }
}
