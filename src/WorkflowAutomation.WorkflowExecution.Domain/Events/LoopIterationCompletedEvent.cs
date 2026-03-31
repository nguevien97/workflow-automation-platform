using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopIterationCompletedEvent(
    LoopExecutionId LoopExecutionId,
    LoopIterationId LoopIterationId,
    int IterationIndex,
    StepOutput Output,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopIterationCompletedEvent(
        LoopExecutionId loopExecutionId,
        LoopIterationId loopIterationId,
        int iterationIndex,
        StepOutput output)
        : this(loopExecutionId, loopIterationId, iterationIndex, output, DateTime.UtcNow) { }
}
