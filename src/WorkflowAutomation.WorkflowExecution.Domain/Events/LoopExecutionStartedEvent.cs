using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record LoopExecutionStartedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepId LoopStepId,
    StepId LoopEntryStepId,
    object ResolvedSource,
    ConcurrencyMode ConcurrencyMode,
    int? MaxConcurrency,
    IterationFailureStrategy IterationFailureStrategy,
    IReadOnlyDictionary<string, StepOutput> UpstreamStepOutputs,
    DateTime OccurredOn) : IDomainEvent
{
    public LoopExecutionStartedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId loopStepId,
        StepId loopEntryStepId,
        object resolvedSource,
        ConcurrencyMode concurrencyMode,
        int? maxConcurrency,
        IterationFailureStrategy iterationFailureStrategy,
        IReadOnlyDictionary<string, StepOutput> upstreamStepOutputs)
        : this(
            workflowExecutionId,
            stepExecutionId,
            loopStepId,
            loopEntryStepId,
            resolvedSource,
            concurrencyMode,
            maxConcurrency,
            iterationFailureStrategy,
            upstreamStepOutputs,
            DateTime.UtcNow) { }
}