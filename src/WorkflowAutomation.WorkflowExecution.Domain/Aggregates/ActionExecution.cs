using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Aggregates;

/// <summary>
/// Runtime aggregate for one action step execution. Owns integration dispatch,
/// retry policy, deadline expiry, and terminal action outcome.
/// One instance per StepExecutionId.
/// </summary>
public sealed class ActionExecution : AggregateRoot<ActionExecutionId>
{
    private readonly List<ActionAttempt> _attempts = [];

    // ── Identity and correlation ────────────────────────────────────────────

    public WorkflowExecutionId WorkflowExecutionId { get; }
    public StepExecutionId StepExecutionId { get; }
    public StepId StepId { get; }

    // ── Immutable runtime snapshot ──────────────────────────────────────────

    public ActionStepDefinitionSnapshot StepDefinition { get; }
    public StepInput Input { get; }
    public DateTime DeadlineUtc { get; }

    // ── Execution state ─────────────────────────────────────────────────────

    public ActionExecutionStatus Status { get; private set; }
    public StepOutput? Output { get; private set; }
    public string? LastError { get; private set; }
    public int AttemptCount { get; private set; }
    public int RetryCount => Math.Max(AttemptCount - 1, 0);
    public bool HasRemainingRetries => RetryCount < StepDefinition.MaxRetries;

    // ── Timestamps ──────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? LastAttemptStartedAtUtc { get; private set; }
    public DateTime? LastAttemptCompletedAtUtc { get; private set; }
    public DateTime? NextRetryAtUtc { get; private set; }

    // ── Attempt history ─────────────────────────────────────────────────────

    public IReadOnlyList<ActionAttempt> Attempts => _attempts.AsReadOnly();

    // ── Constructor ─────────────────────────────────────────────────────────

    public ActionExecution(
        ActionExecutionId id,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId stepId,
        ActionStepDefinitionSnapshot stepDefinition,
        StepInput input,
        DateTime deadlineUtc)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(stepDefinition);
        ArgumentNullException.ThrowIfNull(input);

        WorkflowExecutionId = workflowExecutionId;
        StepExecutionId = stepExecutionId;
        StepId = stepId;
        StepDefinition = stepDefinition;
        Input = input;
        DeadlineUtc = deadlineUtc;
        Status = ActionExecutionStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    // ── Public behavior ─────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches the action to the integration layer.
    /// Valid from Pending (first attempt) or WaitingForRetry (subsequent attempts).
    /// </summary>
    public void Execute(DateTime nowUtc)
    {
        if (Status == ActionExecutionStatus.WaitingForRetry)
        {
            if (nowUtc < NextRetryAtUtc)
                throw new InvalidOperationException(
                    $"Cannot execute before scheduled retry time. " +
                    $"Current: {nowUtc:O}, NextRetryAt: {NextRetryAtUtc:O}.");
        }
        else if (Status != ActionExecutionStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot execute an action in '{Status}' status. " +
                $"Expected 'Pending' or 'WaitingForRetry'.");
        }

        if (nowUtc >= DeadlineUtc)
            throw new InvalidOperationException(
                $"Cannot execute an action after its deadline. " +
                $"Current: {nowUtc:O}, Deadline: {DeadlineUtc:O}.");

        Status = ActionExecutionStatus.Running;
        AttemptCount++;
        StartedAtUtc ??= nowUtc;
        LastAttemptStartedAtUtc = nowUtc;
        LastAttemptCompletedAtUtc = null;
        NextRetryAtUtc = null;
        LastError = null;

        _attempts.Add(new ActionAttempt(AttemptCount, nowUtc));

        AddDomainEvent(new IntegrationRequestedEvent(
            Id, WorkflowExecutionId, StepExecutionId, StepId,
            StepDefinition.IntegrationId, StepDefinition.CommandName,
            Input, AttemptCount, DeadlineUtc));
    }

    /// <summary>
    /// Records a successful integration response for the current attempt.
    /// </summary>
    public void RecordIntegrationSucceeded(int attemptNumber, StepOutput output, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardRunning(nameof(RecordIntegrationSucceeded));
        GuardAttemptNumber(attemptNumber, nameof(RecordIntegrationSucceeded));

        Status = ActionExecutionStatus.Completed;
        Output = output;
        CompletedAtUtc = nowUtc;
        LastAttemptCompletedAtUtc = nowUtc;

        GetCurrentAttempt().RecordSuccess(nowUtc);

        AddDomainEvent(new ActionCompletedEvent(
            Id, WorkflowExecutionId, StepExecutionId, output));
    }

    /// <summary>
    /// Records a failed integration response for the current attempt.
    /// Applies the failure strategy decision table to determine the next state.
    /// </summary>
    public void RecordIntegrationFailed(
        int attemptNumber, string error, DateTime nowUtc, DateTime? retryAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardRunning(nameof(RecordIntegrationFailed));
        GuardAttemptNumber(attemptNumber, nameof(RecordIntegrationFailed));

        LastError = error;
        LastAttemptCompletedAtUtc = nowUtc;
        GetCurrentAttempt().RecordFailure(nowUtc, error);

        ApplyFailureStrategy(error, nowUtc, retryAtUtc);
    }

    /// <summary>
    /// Records that the action exceeded its overall deadline before reaching a
    /// terminal success state. This is a terminal failure regardless of the
    /// step's retry or skip policy because the action has no time budget left.
    /// </summary>
    public void RecordDeadlineExceeded(DateTime nowUtc, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status is ActionExecutionStatus.Completed
                   or ActionExecutionStatus.Skipped
                   or ActionExecutionStatus.Failed
                   or ActionExecutionStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot record deadline exceeded for an action in '{Status}' status.");

        if (nowUtc < DeadlineUtc)
            throw new InvalidOperationException(
                $"Cannot record deadline exceeded before the deadline has passed. " +
                $"Current: {nowUtc:O}, Deadline: {DeadlineUtc:O}.");

        LastError = reason;
        NextRetryAtUtc = null;

        if (Status == ActionExecutionStatus.Running)
        {
            LastAttemptCompletedAtUtc = nowUtc;
            GetCurrentAttempt().RecordFailure(nowUtc, reason);
        }

        TerminalFail(reason, nowUtc);
    }

    /// <summary>
    /// Cancels this action execution. Valid from Pending, Running, or WaitingForRetry.
    /// Distinct from Skip (which is the step's own failure strategy).
    /// </summary>
    public void Cancel(DateTime nowUtc)
    {
        if (Status is ActionExecutionStatus.Completed
                   or ActionExecutionStatus.Skipped
                   or ActionExecutionStatus.Failed
                   or ActionExecutionStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot cancel an action execution in '{Status}' status.");

        Status = ActionExecutionStatus.Cancelled;
        CompletedAtUtc = nowUtc;
            NextRetryAtUtc = null;

        AddDomainEvent(new ActionCancelledEvent(
            Id, WorkflowExecutionId, StepExecutionId));
    }

    /// <summary>
    /// Whether this action is waiting for a retry and can be re-executed.
    /// </summary>
    public bool CanRetry => Status == ActionExecutionStatus.WaitingForRetry;

    // ── Private helpers ─────────────────────────────────────────────────────

    private void ApplyFailureStrategy(string error, DateTime nowUtc, DateTime? retryAtUtc)
    {
        switch (StepDefinition.FailureStrategy)
        {
            case FailureStrategy.Stop:
                TerminalFail(error, nowUtc);
                break;

            case FailureStrategy.Skip:
                Status = ActionExecutionStatus.Skipped;
                CompletedAtUtc = nowUtc;
                AddDomainEvent(new ActionSkippedEvent(
                    Id, WorkflowExecutionId, StepExecutionId));
                break;

            case FailureStrategy.Retry:
                if (HasRemainingRetries && retryAtUtc.HasValue && retryAtUtc.Value < DeadlineUtc)
                {
                    Status = ActionExecutionStatus.WaitingForRetry;
                    NextRetryAtUtc = retryAtUtc.Value;
                }
                else
                {
                    TerminalFail(error, nowUtc);
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported failure strategy '{StepDefinition.FailureStrategy}'.");
        }
    }

    private void TerminalFail(string error, DateTime nowUtc)
    {
        Status = ActionExecutionStatus.Failed;
        CompletedAtUtc = nowUtc;
        NextRetryAtUtc = null;
        AddDomainEvent(new ActionFailedEvent(
            Id, WorkflowExecutionId, StepExecutionId, error));
    }

    private ActionAttempt GetCurrentAttempt() =>
        _attempts[^1];

    private void GuardRunning(string operation)
    {
        if (Status != ActionExecutionStatus.Running)
            throw new InvalidOperationException(
                $"Cannot {operation} — status is '{Status}', expected 'Running'.");
    }

    private void GuardAttemptNumber(int attemptNumber, string operation)
    {
        if (attemptNumber != AttemptCount)
            throw new InvalidOperationException(
                $"Cannot {operation} — attempt number {attemptNumber} does not match " +
                $"current attempt {AttemptCount}. The response may be stale.");
    }
}
