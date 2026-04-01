using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Aggregates;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Tests;

public class ActionExecutionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ActionExecutionId AeId() => ActionExecutionId.New();
    private static WorkflowExecutionId WeId() => WorkflowExecutionId.New();
    private static StepExecutionId SeId() => StepExecutionId.New();
    private static StepId SId() => StepId.New();
    private static IntegrationId IId() => IntegrationId.New();

    private static readonly DateTime T0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = T0.AddSeconds(10);
    private static readonly DateTime T2 = T0.AddSeconds(20);
    private static readonly DateTime T3 = T0.AddSeconds(30);
    private static readonly DateTime T4 = T0.AddSeconds(40);
    private static readonly DateTime Deadline = T0.AddMinutes(5);

    private static StepInput Input(params (string key, object value)[] values) =>
        new(values.ToDictionary(x => x.key, x => x.value));

    private static StepOutput Output(params (string key, object value)[] values) =>
        new(values.ToDictionary(x => x.key, x => x.value));

    private static ActionStepDefinitionSnapshot Snapshot(
        FailureStrategy failureStrategy = FailureStrategy.Stop,
        int maxRetries = 0) =>
        new(SId(), IId(), "doWork", failureStrategy, maxRetries);

    private static ActionExecution Build(
        FailureStrategy failureStrategy = FailureStrategy.Stop,
        int maxRetries = 0,
        DateTime? deadline = null) =>
        new(AeId(), WeId(), SeId(), SId(),
            Snapshot(failureStrategy, maxRetries),
            Input(("seed", "v1")),
            deadline ?? Deadline);

    /// <summary>Builds, executes, and returns a Running action.</summary>
    private static ActionExecution BuildRunning(
        FailureStrategy failureStrategy = FailureStrategy.Stop,
        int maxRetries = 0,
        DateTime? deadline = null)
    {
        var ae = Build(failureStrategy, maxRetries, deadline);
        ae.Execute(T0);
        return ae;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Construction
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var ae = Build();
        Assert.Equal(ActionExecutionStatus.Pending, ae.Status);
        Assert.Equal(0, ae.AttemptCount);
        Assert.Equal(0, ae.RetryCount);
        Assert.Null(ae.Output);
        Assert.Null(ae.LastError);
        Assert.Null(ae.StartedAtUtc);
        Assert.Null(ae.CompletedAtUtc);
        Assert.Null(ae.NextRetryAtUtc);
        Assert.Empty(ae.Attempts);
        Assert.Equal(Deadline, ae.DeadlineUtc);
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ActionExecution(AeId(), WeId(), SeId(), SId(),
                null!, Input(("k", "v")), Deadline));
    }

    [Fact]
    public void Constructor_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ActionExecution(AeId(), WeId(), SeId(), SId(),
                Snapshot(), null!, Deadline));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Execute
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Execute_FromPending_TransitionsToRunning()
    {
        var ae = Build();
        ae.Execute(T0);

        Assert.Equal(ActionExecutionStatus.Running, ae.Status);
        Assert.Equal(1, ae.AttemptCount);
        Assert.Equal(0, ae.RetryCount);
        Assert.Equal(T0, ae.StartedAtUtc);
        Assert.Equal(T0, ae.LastAttemptStartedAtUtc);
        Assert.Null(ae.NextRetryAtUtc);
    }

    [Fact]
    public void Execute_FromPending_EmitsIntegrationRequestedEvent()
    {
        var aeId = AeId();
        var weId = WeId();
        var seId = SeId();
        var stepId = SId();
        var snapshot = Snapshot();

        var ae = new ActionExecution(aeId, weId, seId, stepId, snapshot,
            Input(("k", "v")), Deadline);
        ae.Execute(T0);

        var evt = ae.DomainEvents.OfType<IntegrationRequestedEvent>().Single();
        Assert.Equal(aeId, evt.ActionExecutionId);
        Assert.Equal(weId, evt.WorkflowExecutionId);
        Assert.Equal(seId, evt.StepExecutionId);
        Assert.Equal(stepId, evt.StepId);
        Assert.Equal(snapshot.IntegrationId, evt.IntegrationId);
        Assert.Equal("doWork", evt.CommandName);
        Assert.Equal(1, evt.AttemptNumber);
        Assert.Equal(Deadline, evt.DeadlineUtc);
    }

    [Fact]
    public void Execute_CreatesActionAttempt()
    {
        var ae = Build();
        ae.Execute(T0);

        Assert.Single(ae.Attempts);
        var attempt = ae.Attempts[0];
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(T0, attempt.StartedAtUtc);
        Assert.Null(attempt.CompletedAtUtc);
        Assert.False(attempt.Succeeded);
    }

    [Fact]
    public void Execute_FromRunning_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<InvalidOperationException>(() => ae.Execute(T1));
    }

    [Fact]
    public void Execute_FromCompleted_Throws()
    {
        var ae = BuildRunning();
        ae.RecordIntegrationSucceeded(1, Output(("r", "v")), T1);
        Assert.Throws<InvalidOperationException>(() => ae.Execute(T2));
    }

    [Fact]
    public void Execute_FromFailed_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Stop);
        ae.RecordIntegrationFailed(1, "err", T1, null);
        Assert.Throws<InvalidOperationException>(() => ae.Execute(T2));
    }

    [Fact]
    public void Execute_FromSkipped_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Skip);
        ae.RecordIntegrationFailed(1, "err", T1, null);
        Assert.Equal(ActionExecutionStatus.Skipped, ae.Status);
        Assert.Throws<InvalidOperationException>(() => ae.Execute(T2));
    }

    [Fact]
    public void Execute_FromCancelled_Throws()
    {
        var ae = Build();
        ae.Cancel(T0);
        Assert.Throws<InvalidOperationException>(() => ae.Execute(T1));
    }

    [Fact]
    public void Execute_FromWaitingForRetry_WhenDue_TransitionsToRunning()
    {
        var retryAt = T2;
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "transient", T1, retryAt);
        Assert.Equal(ActionExecutionStatus.WaitingForRetry, ae.Status);

        ae.Execute(retryAt); // exactly at retry time

        Assert.Equal(ActionExecutionStatus.Running, ae.Status);
        Assert.Equal(2, ae.AttemptCount);
        Assert.Equal(1, ae.RetryCount);
        Assert.Null(ae.NextRetryAtUtc);
    }

    [Fact]
    public void Execute_FromWaitingForRetry_BeforeDue_Throws()
    {
        var retryAt = T2;
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "transient", T1, retryAt);

        Assert.Throws<InvalidOperationException>(() => ae.Execute(T1)); // before retryAt
    }

    [Fact]
    public void Execute_FromWaitingForRetry_IncrementsAttemptCount()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 3);
        ae.RecordIntegrationFailed(1, "err", T1, T2);

        ae.Execute(T2);
        Assert.Equal(2, ae.AttemptCount);
        Assert.Equal(2, ae.Attempts.Count);
        Assert.Equal(2, ae.Attempts[1].AttemptNumber);
    }

    [Fact]
    public void Execute_AfterDeadline_Throws()
    {
        var deadline = T0.AddSeconds(5);
        var ae = Build(deadline: deadline);

        Assert.Throws<InvalidOperationException>(() => ae.Execute(T1));
    }

    [Fact]
    public void Execute_PreservesStartedAtUtc_OnRetry()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        Assert.Equal(T0, ae.StartedAtUtc);

        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2);

        Assert.Equal(T0, ae.StartedAtUtc); // unchanged
        Assert.Equal(T2, ae.LastAttemptStartedAtUtc); // updated
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. RecordIntegrationSucceeded
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationSucceeded_CompletesAction()
    {
        var ae = BuildRunning();
        var output = Output(("result", "42"));

        ae.RecordIntegrationSucceeded(1, output, T1);

        Assert.Equal(ActionExecutionStatus.Completed, ae.Status);
        Assert.Equal(output, ae.Output);
        Assert.Equal(T1, ae.CompletedAtUtc);
        Assert.Equal(T1, ae.LastAttemptCompletedAtUtc);
    }

    [Fact]
    public void RecordIntegrationSucceeded_EmitsActionCompletedEvent()
    {
        var ae = BuildRunning();
        var output = Output(("result", "42"));
        ae.RecordIntegrationSucceeded(1, output, T1);

        var evt = ae.DomainEvents.OfType<ActionCompletedEvent>().Single();
        Assert.Equal(ae.Id, evt.ActionExecutionId);
        Assert.Equal(ae.WorkflowExecutionId, evt.WorkflowExecutionId);
        Assert.Equal(ae.StepExecutionId, evt.StepExecutionId);
        Assert.Equal(output, evt.Output);
    }

    [Fact]
    public void RecordIntegrationSucceeded_RecordsAttemptSuccess()
    {
        var ae = BuildRunning();
        ae.RecordIntegrationSucceeded(1, Output(("r", "v")), T1);

        var attempt = ae.Attempts.Single();
        Assert.True(attempt.Succeeded);
        Assert.Equal(T1, attempt.CompletedAtUtc);
        Assert.Null(attempt.Error);
    }

    [Fact]
    public void RecordIntegrationSucceeded_FromPending_Throws()
    {
        var ae = Build();
        Assert.Throws<InvalidOperationException>(
            () => ae.RecordIntegrationSucceeded(1, Output(("r", "v")), T0));
    }

    [Fact]
    public void RecordIntegrationSucceeded_NullOutput_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<ArgumentNullException>(
            () => ae.RecordIntegrationSucceeded(1, null!, T1));
    }

    [Fact]
    public void RecordIntegrationSucceeded_StaleAttemptNumber_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2);

        // Attempt 1 response arrives late — should be rejected
        Assert.Throws<InvalidOperationException>(
            () => ae.RecordIntegrationSucceeded(1, Output(("r", "v")), T3));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. RecordIntegrationFailed — Stop Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationFailed_StopStrategy_FailsImmediately()
    {
        var ae = BuildRunning(FailureStrategy.Stop);
        ae.RecordIntegrationFailed(1, "boom", T1, null);

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal("boom", ae.LastError);
        Assert.Equal(T1, ae.CompletedAtUtc);
        Assert.False(ae.CanRetry);
    }

    [Fact]
    public void RecordIntegrationFailed_StopStrategy_EmitsActionFailedEvent()
    {
        var ae = BuildRunning(FailureStrategy.Stop);
        ae.RecordIntegrationFailed(1, "boom", T1, null);

        var evt = ae.DomainEvents.OfType<ActionFailedEvent>().Single();
        Assert.Equal(ae.Id, evt.ActionExecutionId);
        Assert.Equal("boom", evt.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. RecordIntegrationFailed — Skip Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationFailed_SkipStrategy_SkipsAction()
    {
        var ae = BuildRunning(FailureStrategy.Skip);
        ae.RecordIntegrationFailed(1, "not critical", T1, null);

        Assert.Equal(ActionExecutionStatus.Skipped, ae.Status);
        Assert.Equal(T1, ae.CompletedAtUtc);
        Assert.False(ae.CanRetry);
    }

    [Fact]
    public void RecordIntegrationFailed_SkipStrategy_EmitsActionSkippedEvent()
    {
        var ae = BuildRunning(FailureStrategy.Skip);
        ae.RecordIntegrationFailed(1, "not critical", T1, null);

        var evt = ae.DomainEvents.OfType<ActionSkippedEvent>().Single();
        Assert.Equal(ae.Id, evt.ActionExecutionId);
        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionFailedEvent);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. RecordIntegrationFailed — Retry Strategy with Budget
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationFailed_RetryWithBudget_TransitionsToWaitingForRetry()
    {
        var retryAt = T2;
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "transient", T1, retryAt);

        Assert.Equal(ActionExecutionStatus.WaitingForRetry, ae.Status);
        Assert.Equal(retryAt, ae.NextRetryAtUtc);
        Assert.True(ae.CanRetry);
        Assert.Null(ae.CompletedAtUtc); // not terminal
    }

    [Fact]
    public void RecordIntegrationFailed_RetryWithBudget_NoTerminalEvent()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "transient", T1, T2);

        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionFailedEvent);
        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionSkippedEvent);
    }

    [Fact]
    public void RecordIntegrationFailed_RetryWithBudget_RecordsAttemptFailure()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "transient", T1, T2);

        var attempt = ae.Attempts.Single();
        Assert.False(attempt.Succeeded);
        Assert.Equal(T1, attempt.CompletedAtUtc);
        Assert.Equal("transient", attempt.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. RecordIntegrationFailed — Retry Exhausted
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationFailed_RetryExhausted_FailsAction()
    {
        // MaxRetries=1 means 1 initial + 1 retry = 2 total attempts
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 1);
        ae.RecordIntegrationFailed(1, "fail-1", T1, T2);
        ae.Execute(T2);
        ae.RecordIntegrationFailed(2, "fail-2", T3, T4);

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.False(ae.CanRetry);
        Assert.Equal("fail-2", ae.LastError);

        var evt = ae.DomainEvents.OfType<ActionFailedEvent>().Single();
        Assert.Equal("fail-2", evt.Error);
    }

    [Fact]
    public void RecordIntegrationFailed_RetryExhausted_AttemptCountMatchesExpected()
    {
        // Retry(2) = 1 initial + 2 retries = 3 total attempts
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);

        // Attempt 1 fails
        ae.RecordIntegrationFailed(1, "fail-1", T1, T2);
        Assert.True(ae.CanRetry);

        // Attempt 2 fails
        ae.Execute(T2);
        ae.RecordIntegrationFailed(2, "fail-2", T3, T4);
        Assert.True(ae.CanRetry);

        // Attempt 3 fails — exhausted (RetryCount=2 == MaxRetries=2)
        ae.Execute(T4);
        var t5 = T4.AddSeconds(10);
        ae.RecordIntegrationFailed(3, "fail-3", t5, t5.AddSeconds(10));

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal(3, ae.AttemptCount);
        Assert.Equal(2, ae.RetryCount);
        Assert.False(ae.CanRetry);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. RecordIntegrationFailed — Retry Past Deadline
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationFailed_RetryPastDeadline_FailsAction()
    {
        var shortDeadline = T0.AddSeconds(15);
        var ae = Build(FailureStrategy.Retry, maxRetries: 5, deadline: shortDeadline);
        ae.Execute(T0);

        // retryAtUtc is past the deadline
        var retryAt = shortDeadline.AddSeconds(1);
        ae.RecordIntegrationFailed(1, "transient", T1, retryAt);

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Contains(ae.DomainEvents, e => e is ActionFailedEvent);
    }

    [Fact]
    public void RecordIntegrationFailed_RetryWithNullRetryAt_FailsAction()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 5);
        ae.RecordIntegrationFailed(1, "no retry time", T1, retryAtUtc: null);

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. RecordDeadlineExceeded
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordDeadlineExceeded_FromRunning_FailsAction()
    {
        var ae = BuildRunning(FailureStrategy.Skip, deadline: T1);
        ae.RecordDeadlineExceeded(T1, "deadline exceeded");

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal("deadline exceeded", ae.LastError);
        Assert.Contains(ae.DomainEvents, e => e is ActionFailedEvent);
        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionSkippedEvent);
    }

    [Fact]
    public void RecordDeadlineExceeded_FromPending_FailsAction()
    {
        var ae = Build(deadline: T1);
        ae.RecordDeadlineExceeded(T1, "deadline exceeded before dispatch");

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal("deadline exceeded before dispatch", ae.LastError);
        Assert.Contains(ae.DomainEvents, e => e is ActionFailedEvent);
    }

    [Fact]
    public void RecordDeadlineExceeded_FromWaitingForRetry_FailsActionAndClearsRetrySchedule()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 3, deadline: T3);
        ae.RecordIntegrationFailed(1, "transient", T1, T2);

        ae.RecordDeadlineExceeded(T3, "deadline exceeded while waiting");

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Null(ae.NextRetryAtUtc);
        Assert.Equal("deadline exceeded while waiting", ae.LastError);
    }

    [Fact]
    public void RecordDeadlineExceeded_RecordsRunningAttemptFailure()
    {
        var ae = BuildRunning(FailureStrategy.Stop, deadline: T1);
        ae.RecordDeadlineExceeded(T1, "deadline exceeded");

        var attempt = ae.Attempts.Single();
        Assert.False(attempt.Succeeded);
        Assert.Equal("deadline exceeded", attempt.Error);
        Assert.Equal(T1, attempt.CompletedAtUtc);
    }

    [Fact]
    public void RecordDeadlineExceeded_BeforeDeadline_Throws()
    {
        var ae = BuildRunning(deadline: T2);

        Assert.Throws<InvalidOperationException>(
            () => ae.RecordDeadlineExceeded(T1, "too early"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. Cancel
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cancel_FromPending_EmitsActionCancelledEvent()
    {
        var ae = Build();
        ae.Cancel(T0);

        Assert.Equal(ActionExecutionStatus.Cancelled, ae.Status);
        Assert.Equal(T0, ae.CompletedAtUtc);
        var evt = ae.DomainEvents.OfType<ActionCancelledEvent>().Single();
        Assert.Equal(ae.Id, evt.ActionExecutionId);
    }

    [Fact]
    public void Cancel_FromRunning_EmitsActionCancelledEvent()
    {
        var ae = BuildRunning();
        ae.Cancel(T1);

        Assert.Equal(ActionExecutionStatus.Cancelled, ae.Status);
        Assert.Contains(ae.DomainEvents, e => e is ActionCancelledEvent);
    }

    [Fact]
    public void Cancel_FromWaitingForRetry_EmitsActionCancelledEvent()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 2);
        ae.RecordIntegrationFailed(1, "err", T1, T2);
        Assert.Equal(ActionExecutionStatus.WaitingForRetry, ae.Status);

        ae.Cancel(T2);

        Assert.Equal(ActionExecutionStatus.Cancelled, ae.Status);
        Assert.Contains(ae.DomainEvents, e => e is ActionCancelledEvent);
    }

    [Fact]
    public void Cancel_FromCompleted_Throws()
    {
        var ae = BuildRunning();
        ae.RecordIntegrationSucceeded(1, Output(("r", "v")), T1);

        Assert.Throws<InvalidOperationException>(() => ae.Cancel(T2));
    }

    [Fact]
    public void Cancel_FromSkipped_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Skip);
        ae.RecordIntegrationFailed(1, "err", T1, null);

        Assert.Throws<InvalidOperationException>(() => ae.Cancel(T2));
    }

    [Fact]
    public void Cancel_FromFailed_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Stop);
        ae.RecordIntegrationFailed(1, "err", T1, null);

        Assert.Throws<InvalidOperationException>(() => ae.Cancel(T2));
    }

    [Fact]
    public void Cancel_FromCancelled_Throws()
    {
        var ae = Build();
        ae.Cancel(T0);

        Assert.Throws<InvalidOperationException>(() => ae.Cancel(T1));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 11. Full Retry Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FullLifecycle_Pending_Execute_Fail_WaitForRetry_Execute_Succeed()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 3);

        // Attempt 1
        ae.Execute(T0);
        Assert.Equal(ActionExecutionStatus.Running, ae.Status);
        Assert.Equal(1, ae.AttemptCount);

        // Attempt 1 fails → WaitingForRetry
        ae.RecordIntegrationFailed(1, "attempt 1 fail", T1, T2);
        Assert.Equal(ActionExecutionStatus.WaitingForRetry, ae.Status);
        Assert.True(ae.CanRetry);

        // Attempt 2
        ae.Execute(T2);
        Assert.Equal(ActionExecutionStatus.Running, ae.Status);
        Assert.Equal(2, ae.AttemptCount);

        // Attempt 2 fails → WaitingForRetry
        ae.RecordIntegrationFailed(2, "attempt 2 fail", T3, T4);
        Assert.Equal(ActionExecutionStatus.WaitingForRetry, ae.Status);

        // Attempt 3 succeeds
        ae.Execute(T4);
        var output = Output(("result", "success"));
        var t5 = T4.AddSeconds(10);
        ae.RecordIntegrationSucceeded(3, output, t5);

        Assert.Equal(ActionExecutionStatus.Completed, ae.Status);
        Assert.Equal(output, ae.Output);
        Assert.Equal(3, ae.AttemptCount);
        Assert.Equal(2, ae.RetryCount);
        Assert.Equal(T0, ae.StartedAtUtc);
        Assert.Equal(t5, ae.CompletedAtUtc);
    }

    [Fact]
    public void FullLifecycle_AllRetriesExhausted()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);

        // 3 attempts total (1 initial + 2 retries)
        ae.Execute(T0);
        ae.RecordIntegrationFailed(1, "fail-1", T1, T2);

        ae.Execute(T2);
        ae.RecordIntegrationFailed(2, "fail-2", T3, T4);

        ae.Execute(T4);
        var t5 = T4.AddSeconds(10);
        var t6 = t5.AddSeconds(10);
        ae.RecordIntegrationFailed(3, "fail-3", t5, t6);

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal(3, ae.AttemptCount);
        Assert.Equal(2, ae.RetryCount);
        Assert.False(ae.CanRetry);
        Assert.False(ae.HasRemainingRetries);

        var evt = ae.DomainEvents.OfType<ActionFailedEvent>().Single();
        Assert.Equal("fail-3", evt.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 12. Stale Response Rejection
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void StaleSuccessResponse_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 3);
        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2); // now on attempt 2

        Assert.Throws<InvalidOperationException>(
            () => ae.RecordIntegrationSucceeded(1, Output(("r", "v")), T3));
    }

    [Fact]
    public void StaleFailureResponse_Throws()
    {
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 3);
        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2);

        Assert.Throws<InvalidOperationException>(
            () => ae.RecordIntegrationFailed(1, "stale err", T3, T4));
    }

    [Fact]
    public void FutureAttemptNumber_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<InvalidOperationException>(
            () => ae.RecordIntegrationSucceeded(99, Output(("r", "v")), T1));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 13. ActionAttempt History
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AttemptHistory_GrowsWithEachExecute()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 3);

        ae.Execute(T0);
        Assert.Single(ae.Attempts);

        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2);
        Assert.Equal(2, ae.Attempts.Count);

        ae.RecordIntegrationFailed(2, "err", T3, T4);
        ae.Execute(T4);
        Assert.Equal(3, ae.Attempts.Count);
    }

    [Fact]
    public void AttemptHistory_EachAttemptHasCorrectData()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);

        ae.Execute(T0);
        ae.RecordIntegrationFailed(1, "first-err", T1, T2);

        ae.Execute(T2);
        ae.RecordIntegrationSucceeded(2, Output(("r", "v")), T3);

        Assert.Equal(2, ae.Attempts.Count);

        var attempt1 = ae.Attempts[0];
        Assert.Equal(1, attempt1.AttemptNumber);
        Assert.Equal(T0, attempt1.StartedAtUtc);
        Assert.Equal(T1, attempt1.CompletedAtUtc);
        Assert.Equal("first-err", attempt1.Error);
        Assert.False(attempt1.Succeeded);

        var attempt2 = ae.Attempts[1];
        Assert.Equal(2, attempt2.AttemptNumber);
        Assert.Equal(T2, attempt2.StartedAtUtc);
        Assert.Equal(T3, attempt2.CompletedAtUtc);
        Assert.Null(attempt2.Error);
        Assert.True(attempt2.Succeeded);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 14. Event Correlation — All Events Carry ActionExecutionId
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AllTerminalEvents_CarryActionExecutionId()
    {
        // Completed
        var ae1 = BuildRunning();
        ae1.RecordIntegrationSucceeded(1, Output(("r", "v")), T1);
        Assert.Equal(ae1.Id, ae1.DomainEvents.OfType<ActionCompletedEvent>().Single().ActionExecutionId);

        // Failed
        var ae2 = BuildRunning(FailureStrategy.Stop);
        ae2.RecordIntegrationFailed(1, "err", T1, null);
        Assert.Equal(ae2.Id, ae2.DomainEvents.OfType<ActionFailedEvent>().Single().ActionExecutionId);

        // Skipped
        var ae3 = BuildRunning(FailureStrategy.Skip);
        ae3.RecordIntegrationFailed(1, "err", T1, null);
        Assert.Equal(ae3.Id, ae3.DomainEvents.OfType<ActionSkippedEvent>().Single().ActionExecutionId);

        // Cancelled
        var ae4 = Build();
        ae4.Cancel(T0);
        Assert.Equal(ae4.Id, ae4.DomainEvents.OfType<ActionCancelledEvent>().Single().ActionExecutionId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 15. Argument Validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIntegrationFailed_NullError_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<ArgumentNullException>(
            () => ae.RecordIntegrationFailed(1, null!, T1, null));
    }

    [Fact]
    public void RecordIntegrationFailed_EmptyError_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<ArgumentException>(
            () => ae.RecordIntegrationFailed(1, "", T1, null));
    }

    [Fact]
        public void RecordDeadlineExceeded_NullReason_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<ArgumentNullException>(
            () => ae.RecordDeadlineExceeded(T1, null!));
    }

    [Fact]
        public void RecordDeadlineExceeded_EmptyReason_Throws()
    {
        var ae = BuildRunning();
        Assert.Throws<ArgumentException>(
            () => ae.RecordDeadlineExceeded(T1, ""));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 16. HasRemainingRetries and RetryCount Derived Properties
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RetryCount_IsDerivedFromAttemptCount()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 3);
        Assert.Equal(0, ae.AttemptCount);
        Assert.Equal(0, ae.RetryCount);

        ae.Execute(T0);
        Assert.Equal(1, ae.AttemptCount);
        Assert.Equal(0, ae.RetryCount); // first attempt is not a retry

        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2);
        Assert.Equal(2, ae.AttemptCount);
        Assert.Equal(1, ae.RetryCount); // second attempt = 1 retry
    }

    [Fact]
    public void HasRemainingRetries_TrueUntilExhausted()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 1);
        Assert.True(ae.HasRemainingRetries); // 0 retries < 1

        ae.Execute(T0);
        Assert.True(ae.HasRemainingRetries); // RetryCount=0 < 1

        ae.RecordIntegrationFailed(1, "err", T1, T2);
        ae.Execute(T2);
        Assert.False(ae.HasRemainingRetries); // RetryCount=1 == MaxRetries=1
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 17. Invariant: Output Only Non-Null When Completed
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Output_NullUnlessCompleted()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);
        Assert.Null(ae.Output); // Pending

        ae.Execute(T0);
        Assert.Null(ae.Output); // Running

        ae.RecordIntegrationFailed(1, "err", T1, T2);
        Assert.Null(ae.Output); // WaitingForRetry

        ae.Execute(T2);
        ae.RecordIntegrationSucceeded(2, Output(("r", "done")), T3);
        Assert.NotNull(ae.Output); // Completed
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 18. Invariant: NextRetryAtUtc Only Non-Null When WaitingForRetry
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NextRetryAtUtc_OnlyNonNullWhenWaitingForRetry()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);
        Assert.Null(ae.NextRetryAtUtc); // Pending

        ae.Execute(T0);
        Assert.Null(ae.NextRetryAtUtc); // Running

        ae.RecordIntegrationFailed(1, "err", T1, T2);
        Assert.Equal(T2, ae.NextRetryAtUtc); // WaitingForRetry

        ae.Execute(T2);
        Assert.Null(ae.NextRetryAtUtc); // Running again — cleared
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 19. Edge Case: MaxRetries=0 with Retry Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RetryStrategyWithMaxRetries0_FailsOnFirstFailure()
    {
        // Retry(0) should allow only 1 attempt — no retries
        var ae = BuildRunning(FailureStrategy.Retry, maxRetries: 0);
        ae.RecordIntegrationFailed(1, "err", T1, T2);

        // RetryCount=0, MaxRetries=0, HasRemainingRetries=false
        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Contains(ae.DomainEvents, e => e is ActionFailedEvent);
    }
}
