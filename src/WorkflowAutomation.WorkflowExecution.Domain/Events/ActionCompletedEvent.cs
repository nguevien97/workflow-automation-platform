using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionCompletedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepOutput Output,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionCompletedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepOutput output)
        : this(workflowExecutionId, stepExecutionId, output, DateTime.UtcNow) { }
}
