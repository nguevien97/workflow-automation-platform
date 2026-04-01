using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionSkippedEvent(
    ActionExecutionId ActionExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionSkippedEvent(
        ActionExecutionId actionExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId)
        : this(actionExecutionId, workflowExecutionId, stepExecutionId, DateTime.UtcNow) { }
}
