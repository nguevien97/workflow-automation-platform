using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public sealed class ActionStepDefinition : StepDefinition
{
    private readonly Dictionary<string, TemplateOrLiteral> _inputMappings;

    public IntegrationId IntegrationId { get; }
    public string CommandName { get; }
    public IReadOnlyDictionary<string, TemplateOrLiteral> InputMappings => _inputMappings.AsReadOnly();
    public StepOutputSchema? OutputSchema { get; }
    public FailureStrategy FailureStrategy { get; }
    public int RetryCount { get; }

    public ActionStepDefinition(
        StepId id,
        string name,
        IntegrationId integrationId,
        string commandName,
        Dictionary<string, TemplateOrLiteral> inputMappings,
        FailureStrategy failureStrategy,
        int retryCount = 0,
        StepOutputSchema? outputSchema = null,
        StepId? nextStepId = null)
        : base(id, StepType.Action, name, nextStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(inputMappings);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        if (failureStrategy == FailureStrategy.Retry && retryCount <= 0)
            throw new ArgumentException("RetryCount must be greater than zero when FailureStrategy is Retry.", nameof(retryCount));

        IntegrationId = integrationId;
        CommandName = commandName;
        _inputMappings = new Dictionary<string, TemplateOrLiteral>(inputMappings);
        OutputSchema = outputSchema;
        FailureStrategy = failureStrategy;
        RetryCount = retryCount;
    }
}
