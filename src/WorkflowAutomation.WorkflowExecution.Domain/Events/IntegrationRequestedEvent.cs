using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

/// <summary>
/// Emitted by ActionExecution.Execute() — the handoff from action policy
/// to integration execution. The integration side consumes this to dispatch
/// the actual external call.
/// </summary>
public sealed record IntegrationRequestedEvent(
    ActionExecutionId ActionExecutionId,
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepId StepId,
    IntegrationId IntegrationId,
    string CommandName,
    StepInput Input,
    int AttemptNumber,
    DateTime DeadlineUtc,
    DateTime OccurredOn) : IDomainEvent
{
    public IntegrationRequestedEvent(
        ActionExecutionId actionExecutionId,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId stepId,
        IntegrationId integrationId,
        string commandName,
        StepInput input,
        int attemptNumber,
        DateTime deadlineUtc)
        : this(
            actionExecutionId, workflowExecutionId, stepExecutionId, stepId,
            integrationId, commandName, input,
            attemptNumber, deadlineUtc, DateTime.UtcNow) { }
}
