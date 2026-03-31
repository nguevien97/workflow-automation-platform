using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Aggregates;

public sealed class LoopExecution : AggregateRoot<LoopExecutionId>
{
    private readonly List<LoopIteration> _iterations = [];

    public WorkflowExecutionId WorkflowExecutionId { get; }
    public StepExecutionId StepExecutionId { get; }
    public StepId LoopStepId { get; }
    public StepId LoopEntryStepId { get; }
    public ConcurrencyMode ConcurrencyMode { get; }
    public int? MaxConcurrency { get; }
    public IterationFailureStrategy IterationFailureStrategy { get; }
    public IReadOnlyDictionary<string, StepOutput> UpstreamStepOutputs { get; }
    public LoopExecutionStatus Status { get; private set; }
    public IReadOnlyList<LoopIteration> Iterations => _iterations.AsReadOnly();

    public LoopExecution(
        LoopExecutionId id,
        WorkflowExecutionId workflowExecutionId,
        StepExecutionId stepExecutionId,
        StepId loopStepId,
        StepId loopEntryStepId,
        LoopSourceItems sourceItems,
        ConcurrencyMode concurrencyMode,
        int? maxConcurrency,
        IterationFailureStrategy iterationFailureStrategy,
        IReadOnlyDictionary<string, StepOutput> upstreamStepOutputs)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(sourceItems);
        ArgumentNullException.ThrowIfNull(upstreamStepOutputs);

        if (concurrencyMode == ConcurrencyMode.Parallel && maxConcurrency.HasValue)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency.Value, 1);

        WorkflowExecutionId = workflowExecutionId;
        StepExecutionId = stepExecutionId;
        LoopStepId = loopStepId;
        LoopEntryStepId = loopEntryStepId;
        ConcurrencyMode = concurrencyMode;
        MaxConcurrency = maxConcurrency;
        IterationFailureStrategy = iterationFailureStrategy;
        UpstreamStepOutputs = upstreamStepOutputs;
        Status = LoopExecutionStatus.Pending;

        for (var i = 0; i < sourceItems.Count; i++)
            _iterations.Add(new LoopIteration(LoopIterationId.New(), i, sourceItems.Items[i]));
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public void Start()
    {
        GuardStatus(LoopExecutionStatus.Pending, nameof(Start));

        if (_iterations.Count == 0)
        {
            Status = LoopExecutionStatus.Completed;
            AddDomainEvent(new LoopCompletedEvent(
                Id, WorkflowExecutionId, StepExecutionId, AggregateResults()));
            return;
        }

        Status = LoopExecutionStatus.Running;
        StartPendingIterations();
    }

    public void RecordIterationCompleted(LoopIterationId iterationId, StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(LoopExecutionStatus.Running, nameof(RecordIterationCompleted));

        var iteration = GetIterationOrThrow(iterationId);
        iteration.MarkCompleted(output);

        AddDomainEvent(new LoopIterationCompletedEvent(
            Id, iteration.Id, iteration.Index, output));

        TryAdvance();
    }

    public void RecordIterationFailed(LoopIterationId iterationId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardStatus(LoopExecutionStatus.Running, nameof(RecordIterationFailed));

        var iteration = GetIterationOrThrow(iterationId);
        iteration.MarkFailed(error);

        AddDomainEvent(new LoopIterationFailedEvent(
            Id, iteration.Id, iteration.Index, error));

        if (IterationFailureStrategy == IterationFailureStrategy.Skip)
        {
            iteration.MarkSkipped();
            AddDomainEvent(new LoopIterationSkippedEvent(
                Id, iteration.Id, iteration.Index));
            TryAdvance();
        }
        else // Stop
        {
            CancelNonTerminalIterations();
            Status = LoopExecutionStatus.Failed;
            AddDomainEvent(new LoopFailedEvent(
                Id, WorkflowExecutionId, StepExecutionId,
                $"Iteration {iteration.Index} failed: {error}"));
        }
    }

    public void Cancel()
    {
        if (Status is LoopExecutionStatus.Completed
                   or LoopExecutionStatus.Failed
                   or LoopExecutionStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot cancel a loop execution in '{Status}' status.");

        CancelNonTerminalIterations();
        Status = LoopExecutionStatus.Cancelled;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void TryAdvance()
    {
        if (_iterations.All(i => i.Status is LoopIterationStatus.Completed
                                          or LoopIterationStatus.Skipped
                                          or LoopIterationStatus.Failed
                                          or LoopIterationStatus.Cancelled))
        {
            Status = LoopExecutionStatus.Completed;
            AddDomainEvent(new LoopCompletedEvent(
                Id, WorkflowExecutionId, StepExecutionId, AggregateResults()));
            return;
        }

        StartPendingIterations();
    }

    private void StartPendingIterations()
    {
        var slotsAvailable = ConcurrencyMode == ConcurrencyMode.Sequential
            ? 1 - _iterations.Count(i => i.Status == LoopIterationStatus.Running)
            : (MaxConcurrency ?? int.MaxValue) - _iterations.Count(i => i.Status == LoopIterationStatus.Running);

        foreach (var iteration in _iterations.Where(i => i.Status == LoopIterationStatus.Pending))
        {
            if (slotsAvailable <= 0) break;

            iteration.MarkRunning();
            AddDomainEvent(new LoopIterationStartedEvent(
                Id, iteration.Id,
                WorkflowExecutionId, StepExecutionId,
                LoopStepId, LoopEntryStepId,
                iteration.Index, iteration.IterationItem,
                UpstreamStepOutputs));

            slotsAvailable--;
        }
    }

    private StepOutput AggregateResults()
    {
        var items = _iterations
            .OrderBy(i => i.Index)
            .Select(i => i.Status == LoopIterationStatus.Completed ? (object?)i.Output : null)
            .ToList();

        return new StepOutput(new Dictionary<string, object> { ["items"] = items });
    }

    private void CancelNonTerminalIterations()
    {
        foreach (var iteration in _iterations.Where(i =>
                     i.Status is LoopIterationStatus.Pending or LoopIterationStatus.Running))
        {
            iteration.Cancel();
        }
    }

    private LoopIteration GetIterationOrThrow(LoopIterationId id) =>
        _iterations.Find(i => i.Id == id)
        ?? throw new InvalidOperationException(
            $"Loop iteration '{id}' not found.");

    private void GuardStatus(LoopExecutionStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} — status is '{Status}', expected '{expected}'.");
    }
}
