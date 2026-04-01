using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopCancelledEvent(
    LoopExecutionId LoopExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopCancelledEvent(
        LoopExecutionId loopExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId)
        : this(loopExecutionId, workflowExecutionId, stepExecutionId, DateTime.UtcNow) { }
}
