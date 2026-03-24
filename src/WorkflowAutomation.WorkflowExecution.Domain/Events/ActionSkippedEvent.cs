using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionSkippedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionSkippedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId)
        : this(workflowExecutionId, stepExecutionId, DateTime.UtcNow) { }
}
