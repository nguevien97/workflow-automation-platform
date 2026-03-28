using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class ConditionStepDefinition : StepDefinition
{
    public IReadOnlyList<ConditionRule> Rules { get; }
    public StepId? FallbackStepId { get; private set; }

    public ConditionStepDefinition(
        StepId id,
        string name,
        IReadOnlyList<ConditionRule> rules,
        StepId? nextStepId = null,
        StepId? fallbackStepId = null)
        : base(id, StepType.Condition, name, nextStepId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Count == 0)
            throw new ArgumentException("At least one condition rule must be provided.", nameof(rules));

        Rules = rules;
        FallbackStepId = fallbackStepId;
    }
}
