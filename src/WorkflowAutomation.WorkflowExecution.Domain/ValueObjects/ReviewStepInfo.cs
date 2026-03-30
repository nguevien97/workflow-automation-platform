using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class ReviewStepInfo : StepDefinitionInfo
{
    public StepId RejectionTargetStepId { get; }
    public int MaxRejections { get; }

    public ReviewStepInfo(
        StepId stepId,
        string name,
        StepId rejectionTargetStepId,
        int maxRejections = 3,
        StepId? nextStepId = null)
        : base(stepId, name, StepType.Review, nextStepId)
    {
        if (rejectionTargetStepId == default)
            throw new ArgumentException(
                "Rejection target step ID must not be empty.",
                nameof(rejectionTargetStepId));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRejections, 1);

        RejectionTargetStepId = rejectionTargetStepId;
        MaxRejections = maxRejections;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var c in base.GetEqualityComponents()) yield return c;
        yield return RejectionTargetStepId;
        yield return MaxRejections;
    }
}