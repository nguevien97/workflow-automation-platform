using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class LoopStepDefinition : StepDefinition
{
    public TemplateReference SourceArray { get; }
    public StepOutputSchema TriggerOutputSchema { get; }
    public IReadOnlyList<StepDefinition> Steps { get; }
    public ConcurrencyMode ConcurrencyMode { get; }
    public int? MaxConcurrency { get; }
    public IterationFailureStrategy IterationFailureStrategy { get; }
    public int RetryCount { get; }

    public LoopStepDefinition(
        StepId id,
        string name,
        TemplateReference sourceArray,
        StepOutputSchema triggerOutputSchema,
        IReadOnlyList<StepDefinition> steps,
        ConcurrencyMode concurrencyMode,
        IterationFailureStrategy iterationFailureStrategy,
        int retryCount = 0,
        StepId? nextStepId = null,
        int? maxConcurrency = null)
        : base(id, StepType.Loop, name, nextStepId)
    {
        ArgumentNullException.ThrowIfNull(sourceArray);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        if (concurrencyMode == ConcurrencyMode.Parallel && maxConcurrency.HasValue)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency.Value, 1);

        SourceArray = sourceArray;
        TriggerOutputSchema = triggerOutputSchema;
        Steps = steps;
        ConcurrencyMode = concurrencyMode;
        MaxConcurrency = maxConcurrency;
        IterationFailureStrategy = iterationFailureStrategy;
        RetryCount = retryCount;
    }
}
