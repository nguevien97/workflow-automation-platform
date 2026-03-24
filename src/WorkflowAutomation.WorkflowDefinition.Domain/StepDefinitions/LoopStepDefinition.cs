using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class LoopStepDefinition : StepDefinition
{
    public TemplateReference SourceArray { get; }
    public WorkflowVersionId BodyWorkflowVersionId { get; }
    public ConcurrencyMode ConcurrencyMode { get; }
    public int? MaxConcurrency { get; }
    public FailureStrategy IterationFailureStrategy { get; }
    public int RetryCount { get; }

    public LoopStepDefinition(
        StepId id,
        string name,
        TemplateReference sourceArray,
        WorkflowVersionId bodyWorkflowVersionId,
        ConcurrencyMode concurrencyMode,
        FailureStrategy iterationFailureStrategy,
        int retryCount = 0,
        int? maxConcurrency = null)
        : base(id, StepType.Loop, name)
    {
        ArgumentNullException.ThrowIfNull(sourceArray);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        if (concurrencyMode == ConcurrencyMode.Parallel && maxConcurrency.HasValue)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency.Value, 1);

        if (iterationFailureStrategy == FailureStrategy.Retry && retryCount <= 0)
            throw new ArgumentException("RetryCount must be greater than zero when IterationFailureStrategy is Retry.", nameof(retryCount));

        SourceArray = sourceArray;
        BodyWorkflowVersionId = bodyWorkflowVersionId;
        ConcurrencyMode = concurrencyMode;
        MaxConcurrency = maxConcurrency;
        IterationFailureStrategy = iterationFailureStrategy;
        RetryCount = retryCount;
    }
}
