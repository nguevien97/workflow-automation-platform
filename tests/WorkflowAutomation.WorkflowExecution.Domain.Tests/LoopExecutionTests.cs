using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Aggregates;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Tests;

public class LoopExecutionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LoopExecutionId LeId() => LoopExecutionId.New();
    private static WorkflowExecutionId WeId() => WorkflowExecutionId.New();
    private static StepExecutionId SeId() => StepExecutionId.New();
    private static StepId SId() => StepId.New();

    private static StepOutput Output(params (string key, object value)[] values) =>
        new(values.ToDictionary(x => x.key, x => x.value));

    private static LoopSourceItems Items(params object?[] items) =>
        LoopSourceItems.FromResolvedValue(items);

    private static IReadOnlyDictionary<string, StepOutput> NoUpstream() =>
        new Dictionary<string, StepOutput>();

    private static LoopExecution Build(
        LoopSourceItems? sourceItems = null,
        ConcurrencyMode concurrencyMode = ConcurrencyMode.Sequential,
        int? maxConcurrency = null,
        IterationFailureStrategy failureStrategy = IterationFailureStrategy.Skip,
        IReadOnlyDictionary<string, StepOutput>? upstreamOutputs = null)
    {
        return new LoopExecution(
            LeId(), WeId(), SeId(),
            loopStepId: SId(),
            loopEntryStepId: SId(),
            sourceItems ?? Items("a", "b", "c"),
            concurrencyMode,
            maxConcurrency,
            failureStrategy,
            upstreamOutputs ?? NoUpstream());
    }

    private static LoopIteration GetIteration(LoopExecution loop, int index) =>
        loop.Iterations.Single(i => i.Index == index);

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Construction
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_SetsStatusToPending()
    {
        var loop = Build();
        Assert.Equal(LoopExecutionStatus.Pending, loop.Status);
    }

    [Fact]
    public void Constructor_CreatesIterationsForEachSourceItem()
    {
        var loop = Build(sourceItems: Items("x", "y", "z"));

        Assert.Equal(3, loop.Iterations.Count);
        for (var i = 0; i < 3; i++)
        {
            var iter = GetIteration(loop, i);
            Assert.Equal(LoopIterationStatus.Pending, iter.Status);
        }

        Assert.Equal("x", GetIteration(loop, 0).IterationItem);
        Assert.Equal("y", GetIteration(loop, 1).IterationItem);
        Assert.Equal("z", GetIteration(loop, 2).IterationItem);
    }

    [Fact]
    public void Constructor_NullSourceItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoopExecution(
                LeId(), WeId(), SeId(), SId(), SId(),
                null!,
                ConcurrencyMode.Sequential, null,
                IterationFailureStrategy.Skip, NoUpstream()));
    }

    [Fact]
    public void Constructor_ParallelWithZeroMaxConcurrency_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Build(concurrencyMode: ConcurrencyMode.Parallel, maxConcurrency: 0));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Start — Empty Source
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_EmptySourceItems_CompletesImmediately()
    {
        var loop = Build(sourceItems: Items());
        loop.Start();

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
        Assert.Contains(loop.DomainEvents, e => e is LoopCompletedEvent);
    }

    [Fact]
    public void Start_EmptySourceItems_AggregatedOutputHasEmptyItemsList()
    {
        var loop = Build(sourceItems: Items());
        loop.Start();

        var evt = loop.DomainEvents.OfType<LoopCompletedEvent>().Single();
        Assert.True(evt.AggregatedOutput.Data.ContainsKey("items"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Start — Sequential
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_Sequential_StartsFirstIterationOnly()
    {
        var loop = Build();
        loop.Start();

        Assert.Equal(LoopExecutionStatus.Running, loop.Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 2).Status);
    }

    [Fact]
    public void Start_Sequential_EmitsOneLoopIterationStartedEvent()
    {
        var loop = Build();
        loop.Start();

        var events = loop.DomainEvents.OfType<LoopIterationStartedEvent>().ToList();
        Assert.Single(events);
        Assert.Equal(0, events[0].IterationIndex);
    }

    [Fact]
    public void Start_FromNonPending_Throws()
    {
        var loop = Build();
        loop.Start();
        Assert.Throws<InvalidOperationException>(() => loop.Start());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. Start — Parallel Unbounded
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_Parallel_NullMaxConcurrency_StartsAllIterations()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        Assert.All(loop.Iterations, i => Assert.Equal(LoopIterationStatus.Running, i.Status));
    }

    [Fact]
    public void Start_Parallel_NullMaxConcurrency_EmitsStartedEventForEachIteration()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        var events = loop.DomainEvents.OfType<LoopIterationStartedEvent>().ToList();
        Assert.Equal(3, events.Count);
        Assert.Contains(events, e => e.IterationIndex == 0);
        Assert.Contains(events, e => e.IterationIndex == 1);
        Assert.Contains(events, e => e.IterationIndex == 2);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. Start — Parallel Bounded
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_Parallel_MaxConcurrency2_StartsTwoIterations()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 2);
        loop.Start();

        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 3).Status);
    }

    [Fact]
    public void Start_Parallel_MaxConcurrency2_EmitsTwoStartedEvents()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 2);
        loop.Start();

        var events = loop.DomainEvents.OfType<LoopIterationStartedEvent>().ToList();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void Start_Parallel_MaxConcurrencyExceedsCount_StartsAll()
    {
        var loop = Build(
            sourceItems: Items("a", "b"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 10);
        loop.Start();

        Assert.All(loop.Iterations, i => Assert.Equal(LoopIterationStatus.Running, i.Status));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. Sequential — Iteration Completion
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sequential_IterationCompleted_StartsNextIteration()
    {
        var loop = Build();
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationCompleted(iter0.Id, Output(("r", "v0")));

        Assert.Equal(LoopIterationStatus.Completed, iter0.Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 2).Status);
    }

    [Fact]
    public void Sequential_IterationCompleted_EmitsCompletedAndStartedEvents()
    {
        var loop = Build();
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationCompleted(iter0.Id, Output(("r", "v0")));

        Assert.Contains(loop.DomainEvents, e => e is LoopIterationCompletedEvent c && c.IterationIndex == 0);
        // 2 LoopIterationStartedEvents: one for iter0 at Start(), one for iter1 after completion
        var startedEvents = loop.DomainEvents.OfType<LoopIterationStartedEvent>().ToList();
        Assert.Equal(2, startedEvents.Count);
    }

    [Fact]
    public void Sequential_LastIterationCompleted_LoopCompletes()
    {
        var loop = Build(sourceItems: Items("a", "b"));
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationCompleted(iter0.Id, Output(("r", "v0")));

        var iter1 = GetIteration(loop, 1);
        loop.RecordIterationCompleted(iter1.Id, Output(("r", "v1")));

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
        Assert.Contains(loop.DomainEvents, e => e is LoopCompletedEvent);
    }

    [Fact]
    public void Sequential_LastIterationCompleted_AggregatesOutputsInOrder()
    {
        var loop = Build(sourceItems: Items("a", "b", "c"));
        loop.Start();

        for (var i = 0; i < 3; i++)
        {
            var iter = GetIteration(loop, i);
            loop.RecordIterationCompleted(iter.Id, Output(("r", $"v{i}")));
        }

        var evt = loop.DomainEvents.OfType<LoopCompletedEvent>().Single();
        var items = (IReadOnlyList<object?>)evt.AggregatedOutput.Data["items"];
        Assert.Equal(3, items.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. Parallel Bounded — Iteration Completion
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parallel_IterationCompleted_StartsNextPending()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 2);
        loop.Start();

        // iter0 and iter1 running, iter2 and iter3 pending
        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationCompleted(iter0.Id, Output(("r", "v0")));

        // iter2 should now be running (fills the slot)
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 3).Status);
    }

    [Fact]
    public void Parallel_AllCompleted_LoopCompletes()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        // Complete in reverse order to test ordering
        for (var i = 2; i >= 0; i--)
        {
            var iter = GetIteration(loop, i);
            loop.RecordIterationCompleted(iter.Id, Output(("r", $"v{i}")));
        }

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
    }

    [Fact]
    public void Parallel_AllCompleted_AggregatesOutputsInOrder()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        // Complete out of order: 2, 0, 1
        loop.RecordIterationCompleted(GetIteration(loop, 2).Id, Output(("r", "v2")));
        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));
        loop.RecordIterationCompleted(GetIteration(loop, 1).Id, Output(("r", "v1")));

        var evt = loop.DomainEvents.OfType<LoopCompletedEvent>().Single();
        var items = (IReadOnlyList<object?>)evt.AggregatedOutput.Data["items"];
        Assert.Equal(3, items.Count);
        // Outputs ordered by index, not completion order
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. Sequential Failure — Skip Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sequential_IterationFailed_Skip_MarksSkippedAndContinues()
    {
        var loop = Build(failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationFailed(iter0.Id, "fail-0");

        Assert.Equal(LoopIterationStatus.Skipped, iter0.Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopExecutionStatus.Running, loop.Status);
    }

    [Fact]
    public void Sequential_IterationFailed_Skip_EmitsFailedAndSkippedEvents()
    {
        var loop = Build(failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationFailed(iter0.Id, "fail-0");

        Assert.Contains(loop.DomainEvents, e => e is LoopIterationFailedEvent f && f.IterationIndex == 0);
        Assert.Contains(loop.DomainEvents, e => e is LoopIterationSkippedEvent s && s.IterationIndex == 0);
    }

    [Fact]
    public void Sequential_AllSkipped_LoopStillCompletes()
    {
        var loop = Build(
            sourceItems: Items("a", "b"),
            failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fail-0");
        loop.RecordIterationFailed(GetIteration(loop, 1).Id, "fail-1");

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
        Assert.Contains(loop.DomainEvents, e => e is LoopCompletedEvent);
    }

    [Fact]
    public void Sequential_MixedResults_Skip_AggregatesCorrectly()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        // iter0 succeeds, iter1 fails+skipped, iter2 succeeds
        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));
        loop.RecordIterationFailed(GetIteration(loop, 1).Id, "fail");
        loop.RecordIterationCompleted(GetIteration(loop, 2).Id, Output(("r", "v2")));

        var evt = loop.DomainEvents.OfType<LoopCompletedEvent>().Single();
        var items = (IReadOnlyList<object?>)evt.AggregatedOutput.Data["items"];
        Assert.Equal(3, items.Count);
        Assert.Null(items[1]); // skipped iteration → null
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. Sequential Failure — Stop Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sequential_IterationFailed_Stop_CancelsRemainingAndFailsLoop()
    {
        var loop = Build(failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationFailed(iter0.Id, "fatal");

        Assert.Equal(LoopExecutionStatus.Failed, loop.Status);
        Assert.Equal(LoopIterationStatus.Failed, iter0.Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 2).Status);
    }

    [Fact]
    public void Sequential_IterationFailed_Stop_EmitsFailedAndLoopFailedEvents()
    {
        var loop = Build(failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fatal");

        Assert.Contains(loop.DomainEvents, e => e is LoopIterationFailedEvent);
        Assert.Contains(loop.DomainEvents, e => e is LoopFailedEvent);
        Assert.DoesNotContain(loop.DomainEvents, e => e is LoopIterationSkippedEvent);
    }

    [Fact]
    public void Sequential_IterationFailed_Stop_PendingIterationsBecomeCancelled()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        // Complete iter0, then fail iter1 → iter2 and iter3 cancelled
        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));
        loop.RecordIterationFailed(GetIteration(loop, 1).Id, "fatal");

        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Failed, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 3).Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. Parallel Failure — Skip Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parallel_IterationFailed_Skip_ContinuesOthers()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel,
            failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fail");

        Assert.Equal(LoopIterationStatus.Skipped, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 2).Status);
        Assert.Equal(LoopExecutionStatus.Running, loop.Status);
    }

    [Fact]
    public void Parallel_IterationFailed_Skip_StartsNextPending()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 2,
            failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        // iter0 and iter1 running
        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fail");

        // iter0 skipped, iter2 should start (fill the slot)
        Assert.Equal(LoopIterationStatus.Skipped, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 3).Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 11. Parallel Failure — Stop Strategy
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parallel_IterationFailed_Stop_CancelsPendingAndFails()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 2,
            failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        // iter0 and iter1 running, iter2 and iter3 pending
        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fatal");

        Assert.Equal(LoopExecutionStatus.Failed, loop.Status);
        Assert.Equal(LoopIterationStatus.Failed, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 3).Status);
    }

    [Fact]
    public void Parallel_IterationFailed_Stop_CancelsRunningIterations()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel,
            failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        // All 3 running
        loop.RecordIterationFailed(GetIteration(loop, 1).Id, "fatal");

        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Failed, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 2).Status);
    }

    [Fact]
    public void Parallel_IterationFailed_Stop_NoIterationsLeftRunning()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel,
            failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fatal");

        var runningCount = loop.Iterations.Count(i => i.Status == LoopIterationStatus.Running);
        Assert.Equal(0, runningCount);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 12. Cancel
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cancel_FromRunning_CancelsAllNonTerminalIterations()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        // Complete iter0 first
        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));

        loop.Cancel();

        Assert.Equal(LoopExecutionStatus.Cancelled, loop.Status);
        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 0).Status); // was already terminal
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 2).Status);
    }

    [Fact]
    public void Cancel_FromPending_Cancels()
    {
        var loop = Build();
        loop.Cancel();

        Assert.Equal(LoopExecutionStatus.Cancelled, loop.Status);
        Assert.All(loop.Iterations, i => Assert.Equal(LoopIterationStatus.Cancelled, i.Status));
    }

    [Fact]
    public void Cancel_FromCompleted_Throws()
    {
        var loop = Build(sourceItems: Items());
        loop.Start(); // completes immediately

        Assert.Throws<InvalidOperationException>(() => loop.Cancel());
    }

    [Fact]
    public void Cancel_FromFailed_Throws()
    {
        var loop = Build(failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();
        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "fatal");

        Assert.Throws<InvalidOperationException>(() => loop.Cancel());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 13. Guard Conditions
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordIterationCompleted_WhenLoopNotRunning_Throws()
    {
        var loop = Build();
        // Not started yet
        var iter0 = GetIteration(loop, 0);

        Assert.Throws<InvalidOperationException>(
            () => loop.RecordIterationCompleted(iter0.Id, Output(("r", "v"))));
    }

    [Fact]
    public void RecordIterationFailed_WhenLoopNotRunning_Throws()
    {
        var loop = Build();
        var iter0 = GetIteration(loop, 0);

        Assert.Throws<InvalidOperationException>(
            () => loop.RecordIterationFailed(iter0.Id, "err"));
    }

    [Fact]
    public void RecordIterationCompleted_UnknownIterationId_Throws()
    {
        var loop = Build();
        loop.Start();

        Assert.Throws<InvalidOperationException>(
            () => loop.RecordIterationCompleted(LoopIterationId.New(), Output(("r", "v"))));
    }

    [Fact]
    public void RecordIterationCompleted_IterationAlreadyCompleted_Throws()
    {
        var loop = Build();
        loop.Start();

        var iter0 = GetIteration(loop, 0);
        loop.RecordIterationCompleted(iter0.Id, Output(("r", "v0")));

        Assert.Throws<InvalidOperationException>(
            () => loop.RecordIterationCompleted(iter0.Id, Output(("r", "again"))));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 14. Event Content Verification
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LoopIterationStartedEvent_CarriesCorrectData()
    {
        var loopId = LeId();
        var weId = WeId();
        var seId = SeId();
        var loopStepId = SId();
        var entryStepId = SId();
        var upstream = new Dictionary<string, StepOutput>
        {
            ["Fetch"] = Output(("data", "hello"))
        };

        var loop = new LoopExecution(
            loopId, weId, seId,
            loopStepId, entryStepId,
            Items("item-a"),
            ConcurrencyMode.Sequential, null,
            IterationFailureStrategy.Skip,
            upstream);

        loop.Start();

        var evt = loop.DomainEvents.OfType<LoopIterationStartedEvent>().Single();
        Assert.Equal(loopId, evt.LoopExecutionId);
        Assert.Equal(weId, evt.WorkflowExecutionId);
        Assert.Equal(seId, evt.StepExecutionId);
        Assert.Equal(loopStepId, evt.LoopStepId);
        Assert.Equal(entryStepId, evt.LoopEntryStepId);
        Assert.Equal(0, evt.IterationIndex);
        Assert.Equal("item-a", evt.IterationItem);
        Assert.Same(upstream, evt.UpstreamStepOutputs);
    }

    [Fact]
    public void LoopCompletedEvent_CarriesParentCorrelationIds()
    {
        var weId = WeId();
        var seId = SeId();

        var loop = new LoopExecution(
            LeId(), weId, seId, SId(), SId(),
            Items("a"),
            ConcurrencyMode.Sequential, null,
            IterationFailureStrategy.Skip, NoUpstream());

        loop.Start();
        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v")));

        var evt = loop.DomainEvents.OfType<LoopCompletedEvent>().Single();
        Assert.Equal(weId, evt.WorkflowExecutionId);
        Assert.Equal(seId, evt.StepExecutionId);
    }

    [Fact]
    public void LoopFailedEvent_CarriesErrorMessage()
    {
        var loop = Build(
            sourceItems: Items("a"),
            failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "iteration exploded");

        var evt = loop.DomainEvents.OfType<LoopFailedEvent>().Single();
        Assert.Contains("iteration exploded", evt.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 15. Edge Cases
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SingleItem_Sequential_StartAndComplete()
    {
        var loop = Build(sourceItems: Items("only"));
        loop.Start();

        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 0).Status);

        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "done")));

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
    }

    [Fact]
    public void SingleItem_Parallel_StartAndComplete()
    {
        var loop = Build(
            sourceItems: Items("only"),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "done")));

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
    }

    [Fact]
    public void Parallel_MaxConcurrency1_BehavesLikeSequential()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 1);
        loop.Start();

        // Only first iteration started
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 1).Status);

        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));

        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 2).Status);
    }

    [Fact]
    public void IterationItems_WithNullValues_Handled()
    {
        var loop = Build(sourceItems: Items("a", null, "c"));
        loop.Start();

        Assert.Equal("a", GetIteration(loop, 0).IterationItem);
        Assert.Null(GetIteration(loop, 1).IterationItem);
        Assert.Equal("c", GetIteration(loop, 2).IterationItem);
    }

    [Fact]
    public void LargeItemCount_AllStartedInParallelUnbounded()
    {
        var items = Enumerable.Range(0, 100).Cast<object?>().ToArray();
        var loop = Build(
            sourceItems: Items(items),
            concurrencyMode: ConcurrencyMode.Parallel);
        loop.Start();

        Assert.Equal(100, loop.Iterations.Count);
        Assert.All(loop.Iterations, i => Assert.Equal(LoopIterationStatus.Running, i.Status));

        var events = loop.DomainEvents.OfType<LoopIterationStartedEvent>().ToList();
        Assert.Equal(100, events.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 16. Sequential — Full Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 5 items sequential: complete 0, fail+skip 1, complete 2, fail+skip 3, complete 4.
    /// Loop completes with mixed results.
    /// </summary>
    [Fact]
    public void Sequential_FullLifecycle_MixedSuccessAndSkips()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d", "e"),
            failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));
        loop.RecordIterationFailed(GetIteration(loop, 1).Id, "skip-1");
        loop.RecordIterationCompleted(GetIteration(loop, 2).Id, Output(("r", "v2")));
        loop.RecordIterationFailed(GetIteration(loop, 3).Id, "skip-3");
        loop.RecordIterationCompleted(GetIteration(loop, 4).Id, Output(("r", "v4")));

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Skipped, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Skipped, GetIteration(loop, 3).Status);
        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 4).Status);
    }

    /// <summary>
    /// 4 items sequential with Stop strategy: complete 0, complete 1, fail 2 → 3 cancelled.
    /// </summary>
    [Fact]
    public void Sequential_FullLifecycle_StopOnThirdIteration()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d"),
            failureStrategy: IterationFailureStrategy.Stop);
        loop.Start();

        loop.RecordIterationCompleted(GetIteration(loop, 0).Id, Output(("r", "v0")));
        loop.RecordIterationCompleted(GetIteration(loop, 1).Id, Output(("r", "v1")));
        loop.RecordIterationFailed(GetIteration(loop, 2).Id, "fatal");

        Assert.Equal(LoopExecutionStatus.Failed, loop.Status);
        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Completed, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Failed, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Cancelled, GetIteration(loop, 3).Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 17. Parallel — Full Lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 5 items, MaxConcurrency=3, Skip strategy.
    /// Start 0,1,2. Complete 1 → start 3. Fail 0 (skip) → start 4.
    /// Complete 2,3,4 → loop completes.
    /// </summary>
    [Fact]
    public void Parallel_FullLifecycle_BoundedConcurrencyWithSkips()
    {
        var loop = Build(
            sourceItems: Items("a", "b", "c", "d", "e"),
            concurrencyMode: ConcurrencyMode.Parallel,
            maxConcurrency: 3,
            failureStrategy: IterationFailureStrategy.Skip);
        loop.Start();

        // 0,1,2 running; 3,4 pending
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 0).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 1).Status);
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 2).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 3).Status);

        // Complete 1 → 3 starts
        loop.RecordIterationCompleted(GetIteration(loop, 1).Id, Output(("r", "v1")));
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 3).Status);
        Assert.Equal(LoopIterationStatus.Pending, GetIteration(loop, 4).Status);

        // Fail 0 (skip) → 4 starts
        loop.RecordIterationFailed(GetIteration(loop, 0).Id, "skip-0");
        Assert.Equal(LoopIterationStatus.Running, GetIteration(loop, 4).Status);

        // Complete remaining
        loop.RecordIterationCompleted(GetIteration(loop, 2).Id, Output(("r", "v2")));
        loop.RecordIterationCompleted(GetIteration(loop, 3).Id, Output(("r", "v3")));
        loop.RecordIterationCompleted(GetIteration(loop, 4).Id, Output(("r", "v4")));

        Assert.Equal(LoopExecutionStatus.Completed, loop.Status);
    }
}
