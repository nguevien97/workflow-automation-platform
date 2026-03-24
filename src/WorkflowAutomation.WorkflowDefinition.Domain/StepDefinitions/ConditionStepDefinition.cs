using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class ConditionStepDefinition : StepDefinition
{
    private readonly Dictionary<string, List<StepId>> _branches;

    public string Expression { get; }
    public IReadOnlyDictionary<string, List<StepId>> Branches => _branches.AsReadOnly();

    public ConditionStepDefinition(
        StepId id,
        string name,
        string expression,
        Dictionary<string, List<StepId>> branches)
        : base(id, StepType.Condition, name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentNullException.ThrowIfNull(branches);

        Expression = expression;
        _branches = branches.ToDictionary(
            kvp => kvp.Key,
            kvp => new List<StepId>(kvp.Value));
    }
}
