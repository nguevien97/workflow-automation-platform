using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopFailedEvent(
    LoopExecutionId LoopExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    string Error,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopFailedEvent(
        LoopExecutionId loopExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        string error)
        : this(loopExecutionId, workflowExecutionId, stepExecutionId, error, DateTime.UtcNow) { }
}
