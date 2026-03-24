using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionStartedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepInput Input,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionStartedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepInput input)
        : this(workflowExecutionId, stepExecutionId, input, DateTime.UtcNow) { }
}
