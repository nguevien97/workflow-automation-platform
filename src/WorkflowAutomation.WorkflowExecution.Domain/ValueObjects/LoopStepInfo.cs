using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class LoopStepInfo : StepDefinitionInfo
{
    public string SourceArrayExpression { get; }
    public StepId LoopEntryStepId { get; }
    public ConcurrencyMode ConcurrencyMode { get; }
    public int? MaxConcurrency { get; }
    public IterationFailureStrategy IterationFailureStrategy { get; }

    public LoopStepInfo(
        StepId stepId,
        string name,
        string sourceArrayExpression,
        StepId loopEntryStepId,
        ConcurrencyMode concurrencyMode,
        IterationFailureStrategy iterationFailureStrategy,
        StepId? nextStepId = null,
        int? maxConcurrency = null)
        : base(stepId, name, StepType.Loop, nextStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceArrayExpression);
        if (loopEntryStepId == default)
            throw new ArgumentException(
                "Loop entry step ID must not be empty.", nameof(loopEntryStepId));
        if (concurrencyMode == ConcurrencyMode.Parallel && maxConcurrency.HasValue)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency.Value, 1);

        SourceArrayExpression = sourceArrayExpression;
        LoopEntryStepId = loopEntryStepId;
        ConcurrencyMode = concurrencyMode;
        MaxConcurrency = maxConcurrency;
        IterationFailureStrategy = iterationFailureStrategy;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var c in base.GetEqualityComponents()) yield return c;
        yield return SourceArrayExpression;
        yield return LoopEntryStepId;
        yield return ConcurrencyMode;
        yield return MaxConcurrency;
        yield return IterationFailureStrategy;
    }
}
