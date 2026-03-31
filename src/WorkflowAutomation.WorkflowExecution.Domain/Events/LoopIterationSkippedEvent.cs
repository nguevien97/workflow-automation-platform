using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopIterationSkippedEvent(
    LoopExecutionId LoopExecutionId,
    LoopIterationId LoopIterationId,
    int IterationIndex,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopIterationSkippedEvent(
        LoopExecutionId loopExecutionId,
        LoopIterationId loopIterationId,
        int iterationIndex)
        : this(loopExecutionId, loopIterationId, iterationIndex, DateTime.UtcNow) { }
}
