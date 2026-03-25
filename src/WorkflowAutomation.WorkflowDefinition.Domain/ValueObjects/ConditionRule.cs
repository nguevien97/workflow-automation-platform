using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

public sealed class ConditionRule : ValueObject
{
    public string Expression { get; }
    public StepId TargetStepId { get; }

    public ConditionRule(string expression, StepId targetStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        Expression = expression;
        TargetStepId = targetStepId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Expression;
        yield return TargetStepId;
    }
}