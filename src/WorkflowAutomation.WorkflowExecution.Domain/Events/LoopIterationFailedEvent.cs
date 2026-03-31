using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopIterationFailedEvent(
    LoopExecutionId LoopExecutionId,
    LoopIterationId LoopIterationId,
    int IterationIndex,
    string Error,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopIterationFailedEvent(
        LoopExecutionId loopExecutionId,
        LoopIterationId loopIterationId,
        int iterationIndex,
        string error)
        : this(loopExecutionId, loopIterationId, iterationIndex, error, DateTime.UtcNow) { }
}
