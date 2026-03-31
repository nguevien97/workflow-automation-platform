using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopCompletedEvent(
    LoopExecutionId LoopExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepOutput AggregatedOutput,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopCompletedEvent(
        LoopExecutionId loopExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepOutput aggregatedOutput)
        : this(loopExecutionId, workflowExecutionId, stepExecutionId, aggregatedOutput, DateTime.UtcNow) { }
}
