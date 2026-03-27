using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class LoopStepDefinition : StepDefinition
{
    public TemplateReference SourceArray { get; }
    public StepOutputSchema TriggerOutputSchema { get; }
    public StepOutputSchema OutputSchema { get; }
    public readonly StepId LoopEntryStepId;
    public ConcurrencyMode ConcurrencyMode { get; }
    public int? MaxConcurrency { get; }
    public IterationFailureStrategy IterationFailureStrategy { get; }

    public LoopStepDefinition(
        StepId id,
        string name,
        TemplateReference sourceArray,
        StepId loopEntryStepId,
        StepOutputSchema triggerOutputSchema,
        StepOutputSchema outputSchema,
        ConcurrencyMode concurrencyMode,
        IterationFailureStrategy iterationFailureStrategy,
        StepId? nextStepId = null,
        int? maxConcurrency = null)
        : base(id, StepType.Loop, name, nextStepId)
    {
        ArgumentNullException.ThrowIfNull(sourceArray);
        if (loopEntryStepId == default)
            throw new ArgumentException("Loop entry step ID must not be empty.", nameof(loopEntryStepId));
        if (concurrencyMode == ConcurrencyMode.Parallel && maxConcurrency.HasValue)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency.Value, 1);

        SourceArray = sourceArray;
        TriggerOutputSchema = triggerOutputSchema;
        OutputSchema = outputSchema;
        LoopEntryStepId = loopEntryStepId;
        ConcurrencyMode = concurrencyMode;
        MaxConcurrency = maxConcurrency;
        IterationFailureStrategy = iterationFailureStrategy;
    }
}
