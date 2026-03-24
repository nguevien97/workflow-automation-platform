using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionFailedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    string Error,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionFailedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        string error)
        : this(workflowExecutionId, stepExecutionId, error, DateTime.UtcNow) { }
}
