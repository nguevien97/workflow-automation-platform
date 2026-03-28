using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Events;

public sealed record ActionExecutionRequestedEvent(
    WorkflowExecutionId WorkflowExecutionId,
    StepExecutionId StepExecutionId,
    StepId StepId,
    IntegrationId IntegrationId,
    string CommandName,
    StepInput Input,
    FailureStrategy FailureStrategy,
    int MaxRetries,
    DateTime OccurredOn) : IDomainEvent
{
    public ActionExecutionRequestedEvent(
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId stepId,
        IntegrationId integrationId,
        string commandName,
        StepInput input,
        FailureStrategy failureStrategy,
        int maxRetries)
        : this(
            workflowExecutionId,
            stepExecutionId,
            stepId,
            integrationId,
            commandName,
            input,
            failureStrategy,
            maxRetries,
            DateTime.UtcNow) { }
}