using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class ActionStepInfo : StepDefinitionInfo
{
    public IntegrationId IntegrationId { get; }
    public string CommandName { get; }
    public IReadOnlyDictionary<string, string> InputMappings { get; }
    public FailureStrategy FailureStrategy { get; }
    public int MaxRetries { get; }

    public ActionStepInfo(
        StepId stepId,
        string name,
        IntegrationId integrationId,
        string commandName,
        IReadOnlyDictionary<string, string> inputMappings,
        FailureStrategy failureStrategy,
        int maxRetries = 0,
        StepId? nextStepId = null)
        : base(stepId, name, StepType.Action, nextStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(inputMappings);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);

        if (failureStrategy == FailureStrategy.Retry && maxRetries <= 0)
            throw new ArgumentException(
                "MaxRetries must be greater than zero when FailureStrategy is Retry.",
                nameof(maxRetries));

        IntegrationId = integrationId;
        CommandName = commandName;
        InputMappings = inputMappings;
        FailureStrategy = failureStrategy;
        MaxRetries = maxRetries;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var c in base.GetEqualityComponents()) yield return c;
        yield return IntegrationId;
        yield return CommandName;
        foreach (var kvp in InputMappings.OrderBy(x => x.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
        yield return FailureStrategy;
        yield return MaxRetries;
    }
}
