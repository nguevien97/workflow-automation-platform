using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class TriggerStepDefinition : StepDefinition
{
    private readonly Dictionary<string, string> _configuration;

    public IntegrationId IntegrationId { get; }
    public string CommandName { get; }
    public IReadOnlyDictionary<string, string> Configuration => _configuration.AsReadOnly();
    public StepOutputSchema? OutputSchema { get; }

    public TriggerStepDefinition(
        StepId id,
        string name,
        IntegrationId integrationId,
        string commandName,
        Dictionary<string, string> configuration,
        StepId nextStepId,
        StepOutputSchema? outputSchema = null)
        : base(id, StepType.Trigger, name, nextStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(configuration);

        IntegrationId = integrationId;
        CommandName = commandName;
        _configuration = new Dictionary<string, string>(configuration);
        OutputSchema = outputSchema;
    }
}
