using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class ConditionRuleInfo : ValueObject
{
    public string Expression { get; }
    public StepId TargetStepId { get; }

    public ConditionRuleInfo(string expression, StepId targetStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (targetStepId == default)
            throw new ArgumentException("Target step ID must not be empty.", nameof(targetStepId));

        Expression = expression;
        TargetStepId = targetStepId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Expression;
        yield return TargetStepId;
    }
}
