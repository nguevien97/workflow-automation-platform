using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Aggregates;

public sealed class ActionExecution : AggregateRoot<ActionExecutionId>
{
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public StepExecutionId StepExecutionId { get; }
    public ActionStepDefinitionSnapshot StepDefinition { get; }
    public int RetryCount { get; private set; }
    public ActionExecutionStatus Status { get; private set; }
    public StepInput Input { get; }
    public StepOutput? Output { get; private set; }
    public string? Error { get; private set; }

    public ActionExecution(
        ActionExecutionId id,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        ActionStepDefinitionSnapshot stepDefinition,
        StepInput input)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(stepDefinition);
        ArgumentNullException.ThrowIfNull(input);

        WorkflowExecutionId = workflowExecutionId;
        StepExecutionId = stepExecutionId;
        StepDefinition = stepDefinition;
        Input = input;
        Status = ActionExecutionStatus.Pending;
    }

    public void MarkRunning()
    {
        if (Status is not (ActionExecutionStatus.Pending or ActionExecutionStatus.Failed))
            throw new InvalidOperationException(
                $"Cannot start an action in '{Status}' status.");

        Status = ActionExecutionStatus.Running;
        Error = null;
    }

    public void MarkCompleted(StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardRunning(nameof(MarkCompleted));

        Status = ActionExecutionStatus.Completed;
        Output = output;

        AddDomainEvent(new ActionCompletedEvent(WorkflowExecutionId, StepExecutionId, output));
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardRunning(nameof(MarkFailed));

        RetryCount++;
        Error = error;
        Status = ActionExecutionStatus.Failed;

        if (RetryCount > StepDefinition.MaxRetries ||
            StepDefinition.FailureStrategy != FailureStrategy.Retry)
        {
            AddDomainEvent(StepDefinition.FailureStrategy == FailureStrategy.Skip
                ? new ActionSkippedEvent(WorkflowExecutionId, StepExecutionId)
                : new ActionFailedEvent(WorkflowExecutionId, StepExecutionId, error));
        }
    }

    public bool CanRetry =>
        Status == ActionExecutionStatus.Failed &&
        StepDefinition.FailureStrategy == FailureStrategy.Retry &&
        RetryCount <= StepDefinition.MaxRetries;

    /// <summary>
    /// Cancels this action mid-execution — used when a sibling parallel
    /// branch has failed with a Stop strategy.  Distinct from a Skip
    /// (which is the step's own FailureStrategy).
    /// </summary>
    public void Cancel()
    {
        if (Status is ActionExecutionStatus.Completed or ActionExecutionStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot cancel an action execution in '{Status}' status.");

        Status = ActionExecutionStatus.Cancelled;
        AddDomainEvent(new ActionCancelledEvent(WorkflowExecutionId, StepExecutionId));
    }

    private void GuardRunning(string operation)
    {
        if (Status != ActionExecutionStatus.Running)
            throw new InvalidOperationException(
                $"Cannot {operation} an action in '{Status}' status. Expected 'Running'.");
    }
}
