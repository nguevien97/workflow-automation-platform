using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ReviewStepReachedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepId StepId,
    DateTime OccurredOn) : IDomainEvent
{
    public ReviewStepReachedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId stepId)
        : this(workflowExecutionId, stepExecutionId, stepId, DateTime.UtcNow) { }
}