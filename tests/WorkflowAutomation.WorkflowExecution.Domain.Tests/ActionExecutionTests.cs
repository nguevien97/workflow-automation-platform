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
    private static ActionExecutionId AeId() => ActionExecutionId.New();
    private static WorkflowExecutionId WeId() => WorkflowExecutionId.New();
    private static StepExecutionId SeId() => StepExecutionId.New();
    private static StepId SId() => StepId.New();
    private static IntegrationId IId() => IntegrationId.New();

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
        int maxRetries = 0) =>
        new(AeId(), WeId(), SeId(), Snapshot(failureStrategy, maxRetries), Input(("seed", "v1")));

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Construction
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_SetsStatusToPending()
    {
        var ae = Build();
        Assert.Equal(ActionExecutionStatus.Pending, ae.Status);
        Assert.Equal(0, ae.RetryCount);
        Assert.Null(ae.Output);
        Assert.Null(ae.Error);
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ActionExecution(AeId(), WeId(), SeId(), null!, Input(("k", "v"))));
    }

    [Fact]
    public void Constructor_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ActionExecution(AeId(), WeId(), SeId(), Snapshot(), null!));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. MarkRunning
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MarkRunning_FromPending_TransitionsToRunning()
    {
        var ae = Build();
        ae.MarkRunning();
        Assert.Equal(ActionExecutionStatus.Running, ae.Status);
    }

    [Fact]
    public void MarkRunning_FromFailed_TransitionsToRunning_ForRetry()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);
        ae.MarkRunning();
        ae.MarkFailed("transient error");
        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);

        ae.MarkRunning(); // retry
        Assert.Equal(ActionExecutionStatus.Running, ae.Status);
        Assert.Null(ae.Error); // error cleared on retry
    }

    [Fact]
    public void MarkRunning_FromCompleted_Throws()
    {
        var ae = Build();
        ae.MarkRunning();
        ae.MarkCompleted(Output(("r", "done")));

        Assert.Throws<InvalidOperationException>(() => ae.MarkRunning());
    }

    [Fact]
    public void MarkRunning_FromCancelled_Throws()
    {
        var ae = Build();
        ae.Cancel();
        Assert.Throws<InvalidOperationException>(() => ae.MarkRunning());
    }

    [Fact]
    public void MarkRunning_FromRunning_Throws()
    {
        var ae = Build();
        ae.MarkRunning();
        Assert.Throws<InvalidOperationException>(() => ae.MarkRunning());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. MarkCompleted
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MarkCompleted_EmitsActionCompletedEvent()
    {
        var ae = Build();
        ae.MarkRunning();

        var output = Output(("result", "42"));
        ae.MarkCompleted(output);

        Assert.Equal(ActionExecutionStatus.Completed, ae.Status);
        Assert.Equal(output, ae.Output);

        var evt = ae.DomainEvents.OfType<ActionCompletedEvent>().Single();
        Assert.Equal(ae.WorkflowExecutionId, evt.WorkflowExecutionId);
        Assert.Equal(ae.StepExecutionId, evt.StepExecutionId);
        Assert.Equal(output, evt.Output);
    }

    [Fact]
    public void MarkCompleted_FromPending_Throws()
    {
        var ae = Build();
        Assert.Throws<InvalidOperationException>(
            () => ae.MarkCompleted(Output(("r", "v"))));
    }

    [Fact]
    public void MarkCompleted_NullOutput_Throws()
    {
        var ae = Build();
        ae.MarkRunning();
        Assert.Throws<ArgumentNullException>(() => ae.MarkCompleted(null!));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. MarkFailed — Stop Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MarkFailed_StopStrategy_EmitsActionFailedEvent_Immediately()
    {
        var ae = Build(FailureStrategy.Stop);
        ae.MarkRunning();
        ae.MarkFailed("boom");

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal("boom", ae.Error);
        Assert.Equal(1, ae.RetryCount);
        Assert.False(ae.CanRetry);

        var evt = ae.DomainEvents.OfType<ActionFailedEvent>().Single();
        Assert.Equal("boom", evt.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. MarkFailed — Skip Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MarkFailed_SkipStrategy_EmitsActionSkippedEvent_Immediately()
    {
        var ae = Build(FailureStrategy.Skip);
        ae.MarkRunning();
        ae.MarkFailed("not critical");

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.False(ae.CanRetry);

        var evt = ae.DomainEvents.OfType<ActionSkippedEvent>().Single();
        Assert.Equal(ae.StepExecutionId, evt.StepExecutionId);
        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionFailedEvent);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. MarkFailed — Retry Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MarkFailed_RetryStrategy_WithRetriesRemaining_NoTerminalEvent()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);
        ae.MarkRunning();
        ae.MarkFailed("transient");

        Assert.Equal(ActionExecutionStatus.Failed, ae.Status);
        Assert.Equal(1, ae.RetryCount);
        Assert.True(ae.CanRetry);

        // No terminal event emitted — caller should check CanRetry
        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionFailedEvent);
        Assert.DoesNotContain(ae.DomainEvents, e => e is ActionSkippedEvent);
    }

    [Fact]
    public void MarkFailed_RetryStrategy_RetriesExhausted_EmitsActionFailedEvent()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 1);
        ae.MarkRunning();
        ae.MarkFailed("first fail");
        Assert.True(ae.CanRetry);

        // Retry
        ae.MarkRunning();
        ae.MarkFailed("second fail");

        Assert.False(ae.CanRetry);
        Assert.Equal(2, ae.RetryCount);

        var evt = ae.DomainEvents.OfType<ActionFailedEvent>().Single();
        Assert.Equal("second fail", evt.Error);
    }

    [Fact]
    public void MarkFailed_FromPending_Throws()
    {
        var ae = Build();
        Assert.Throws<InvalidOperationException>(() => ae.MarkFailed("oops"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. Full Retry Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FullRetryLifecycle_Pending_Running_Failed_Running_Completed()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 3);

        ae.MarkRunning();
        ae.MarkFailed("attempt 1");
        Assert.True(ae.CanRetry);

        ae.MarkRunning();
        ae.MarkFailed("attempt 2");
        Assert.True(ae.CanRetry);

        ae.MarkRunning();
        ae.MarkCompleted(Output(("result", "success")));

        Assert.Equal(ActionExecutionStatus.Completed, ae.Status);
        Assert.Equal(2, ae.RetryCount);
        Assert.Contains(ae.DomainEvents, e => e is ActionCompletedEvent);
    }

    [Fact]
    public void FullRetryLifecycle_ExhaustsAllRetries()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 2);

        for (var i = 0; i < 2; i++)
        {
            ae.MarkRunning();
            ae.MarkFailed($"fail-{i}");
        }

        Assert.True(ae.CanRetry); // retryCount=2, maxRetries=2, 2 <= 2 is true

        ae.MarkRunning();
        ae.MarkFailed("final fail");

        Assert.False(ae.CanRetry); // retryCount=3, 3 > 2
        Assert.Contains(ae.DomainEvents, e => e is ActionFailedEvent);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. Cancel
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cancel_FromPending_EmitsActionCancelledEvent()
    {
        var ae = Build();
        ae.Cancel();

        Assert.Equal(ActionExecutionStatus.Cancelled, ae.Status);
        var evt = ae.DomainEvents.OfType<ActionCancelledEvent>().Single();
        Assert.Equal(ae.StepExecutionId, evt.StepExecutionId);
    }

    [Fact]
    public void Cancel_FromRunning_EmitsActionCancelledEvent()
    {
        var ae = Build();
        ae.MarkRunning();
        ae.Cancel();

        Assert.Equal(ActionExecutionStatus.Cancelled, ae.Status);
        Assert.Contains(ae.DomainEvents, e => e is ActionCancelledEvent);
    }

    [Fact]
    public void Cancel_FromFailed_EmitsActionCancelledEvent()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 1);
        ae.MarkRunning();
        ae.MarkFailed("oops");
        ae.Cancel();

        Assert.Equal(ActionExecutionStatus.Cancelled, ae.Status);
    }

    [Fact]
    public void Cancel_FromCompleted_Throws()
    {
        var ae = Build();
        ae.MarkRunning();
        ae.MarkCompleted(Output(("r", "v")));

        Assert.Throws<InvalidOperationException>(() => ae.Cancel());
    }

    [Fact]
    public void Cancel_FromCancelled_Throws()
    {
        var ae = Build();
        ae.Cancel();

        Assert.Throws<InvalidOperationException>(() => ae.Cancel());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. CanRetry Property
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CanRetry_FalseWhenNotFailed()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 3);
        Assert.False(ae.CanRetry); // Pending

        ae.MarkRunning();
        Assert.False(ae.CanRetry); // Running
    }

    [Fact]
    public void CanRetry_FalseForStopStrategy()
    {
        var ae = Build(FailureStrategy.Stop);
        ae.MarkRunning();
        ae.MarkFailed("err");
        Assert.False(ae.CanRetry);
    }

    [Fact]
    public void CanRetry_FalseForSkipStrategy()
    {
        var ae = Build(FailureStrategy.Skip);
        ae.MarkRunning();
        ae.MarkFailed("err");
        Assert.False(ae.CanRetry);
    }

    [Fact]
    public void CanRetry_MaxRetriesZero_FalseAfterFirstFail()
    {
        var ae = Build(FailureStrategy.Retry, maxRetries: 0);
        ae.MarkRunning();
        ae.MarkFailed("err");
        Assert.False(ae.CanRetry); // retryCount=1 > maxRetries=0
    }
}
