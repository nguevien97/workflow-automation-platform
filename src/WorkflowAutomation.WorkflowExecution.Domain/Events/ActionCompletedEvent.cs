using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionCompletedEvent(
    ActionExecutionId ActionExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepOutput Output,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionCompletedEvent(
        ActionExecutionId actionExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepOutput output)
        : this(actionExecutionId, workflowExecutionId, stepExecutionId, output, DateTime.UtcNow) { }
}
