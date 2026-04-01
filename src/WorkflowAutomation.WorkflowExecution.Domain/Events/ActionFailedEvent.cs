using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionFailedEvent(
    ActionExecutionId ActionExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    string Error,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionFailedEvent(
        ActionExecutionId actionExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        string error)
        : this(actionExecutionId, workflowExecutionId, stepExecutionId, error, DateTime.UtcNow) { }
}
