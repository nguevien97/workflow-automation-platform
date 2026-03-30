using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class ReviewStepDefinition : StepDefinition
{
    public StepId RejectionTargetStepId { get; }
    public int MaxRejections { get; }

    public ReviewStepDefinition(
        StepId id,
        string name,
        StepId rejectionTargetStepId,
        int maxRejections = 3,
        StepId? nextStepId = null)
        : base(id, StepType.Review, name, nextStepId)
    {
        if (rejectionTargetStepId == default)
            throw new ArgumentException(
                "Rejection target step ID must not be empty.",
                nameof(rejectionTargetStepId));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRejections, 1);

        RejectionTargetStepId = rejectionTargetStepId;
        MaxRejections = maxRejections;
    }
}