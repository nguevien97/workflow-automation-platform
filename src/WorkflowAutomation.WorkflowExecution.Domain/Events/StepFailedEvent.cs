using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record StepFailedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepId StepId,
    StepExecutionId StepExecutionId,
    string Error,
    DateTime OccurredOn) : IDomainEvent
{
    public StepFailedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepId stepId,
        StepExecutionId stepExecutionId,
        string error)
        : this(workflowExecutionId, stepId, stepExecutionId, error, DateTime.UtcNow) { }
}