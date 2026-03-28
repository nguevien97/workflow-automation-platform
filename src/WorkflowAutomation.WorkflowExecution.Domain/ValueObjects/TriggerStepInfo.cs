using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class TriggerStepInfo : StepDefinitionInfo
{
    public IntegrationId IntegrationId { get; }
    public string CommandName { get; }
    public IReadOnlyDictionary<string, string> Configuration { get; }

    public TriggerStepInfo(
        StepId stepId,
        string name,
        IntegrationId integrationId,
        string commandName,
        IReadOnlyDictionary<string, string> configuration,
        StepId? nextStepId = null)
        : base(stepId, name, StepType.Trigger, nextStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(configuration);

        IntegrationId = integrationId;
        CommandName = commandName;
        Configuration = configuration;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var c in base.GetEqualityComponents()) yield return c;
        yield return IntegrationId;
        yield return CommandName;
        foreach (var kvp in Configuration.OrderBy(x => x.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
}
