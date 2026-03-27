using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class ConditionStepInfo : StepDefinitionInfo
{
    public IReadOnlyList<ConditionRuleInfo> Rules { get; }
    public StepId? FallbackStepId { get; }

    public ConditionStepInfo(
        StepId stepId,
        string name,
        IReadOnlyList<ConditionRuleInfo> rules,
        StepId? nextStepId = null,
        StepId? fallbackStepId = null)
        : base(stepId, name, StepType.Condition, nextStepId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Count == 0)
            throw new ArgumentException(
                "At least one condition rule must be provided.", nameof(rules));

        Rules = rules;
        FallbackStepId = fallbackStepId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var c in base.GetEqualityComponents()) yield return c;
        foreach (var rule in Rules) yield return rule;
        yield return FallbackStepId;
    }
}
