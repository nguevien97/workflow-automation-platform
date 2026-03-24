using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class ConditionStepDefinition : StepDefinition
{
    private readonly Dictionary<string, IReadOnlyList<StepId>> _branches;

    public string Expression { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<StepId>> Branches => _branches.AsReadOnly();

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
            kvp => (IReadOnlyList<StepId>)kvp.Value.AsReadOnly());
    }
}
