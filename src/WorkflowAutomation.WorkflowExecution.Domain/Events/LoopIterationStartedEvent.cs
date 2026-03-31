using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopIterationStartedEvent(
    LoopExecutionId LoopExecutionId,
    LoopIterationId LoopIterationId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepId LoopStepId,
    StepId LoopEntryStepId,
    int IterationIndex,
    object? IterationItem,
    IReadOnlyDictionary<string, StepOutput> UpstreamStepOutputs,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopIterationStartedEvent(
        LoopExecutionId loopExecutionId,
        LoopIterationId loopIterationId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId loopStepId,
        StepId loopEntryStepId,
        int iterationIndex,
        object? iterationItem,
        IReadOnlyDictionary<string, StepOutput> upstreamStepOutputs)
        : this(
            loopExecutionId, loopIterationId,
            workflowExecutionId, stepExecutionId,
            loopStepId, loopEntryStepId,
            iterationIndex, iterationItem,
            upstreamStepOutputs, DateTime.UtcNow) { }
}
