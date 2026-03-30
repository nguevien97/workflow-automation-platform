using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ReviewStepRejectedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepId ReviewStepId,
    StepId TargetStepId,
    string Reason,
    DateTime OccurredOn) : IDomainEvent
{
    public ReviewStepRejectedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepId reviewStepId,
        StepId targetStepId,
        string reason)
        : this(workflowExecutionId, reviewStepId, targetStepId, reason, DateTime.UtcNow) { }
}