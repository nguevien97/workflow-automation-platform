using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class ActionStepDefinitionSnapshot : ValueObject
{
    public StepId StepId { get; }
    public IntegrationId IntegrationId { get; }
    public string CommandName { get; }
    public FailureStrategy FailureStrategy { get; }
    public int MaxRetries { get; }

    public ActionStepDefinitionSnapshot(
        StepId stepId,
        IntegrationId integrationId,
        string commandName,
        FailureStrategy failureStrategy,
        int maxRetries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);

        StepId = stepId;
        IntegrationId = integrationId;
        CommandName = commandName;
        FailureStrategy = failureStrategy;
        MaxRetries = maxRetries;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StepId;
        yield return IntegrationId;
        yield return CommandName;
        yield return FailureStrategy;
        yield return MaxRetries;
    }
}
