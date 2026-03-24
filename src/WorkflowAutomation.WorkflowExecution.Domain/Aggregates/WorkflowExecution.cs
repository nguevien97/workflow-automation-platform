using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Aggregates;

public sealed class WorkflowExecution : AggregateRoot<WorkflowExecutionId>
{
    private readonly List<StepExecution> _stepExecutions = [];

    public WorkflowDefinitionSnapshot Definition { get; }
    public WorkflowExecutionStatus Status { get; private set; }
    public IReadOnlyList<StepExecution> StepExecutions => _stepExecutions.AsReadOnly();
    public DateTime CreatedAt { get; private init; }
    public DateTime? CompletedAt { get; private set; }

    public WorkflowExecution(
        WorkflowExecutionId id,
        WorkflowDefinitionSnapshot definition)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        Status = WorkflowExecutionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to Running and creates the first step execution (Pending).
    /// The application service must subsequently call <see cref="ExecuteStep"/>
    /// with the resolved input to start that step.
    /// </summary>
    public void Start()
    {
        GuardStatus(WorkflowExecutionStatus.Pending, nameof(Start));

        Status = WorkflowExecutionStatus.Running;

        var firstStepId = Definition.GetFirstStepId();
        _stepExecutions.Add(new StepExecution(StepExecutionId.New(), firstStepId));

        AddDomainEvent(new WorkflowStartedEvent(Id));
    }

    /// <summary>
    /// Called by the application service once it has resolved the step's
    /// input from previous outputs.  Transitions the step Pending → Running.
    /// Emits <see cref="ActionStartedEvent"/> for action-type steps so the
    /// application service can create and dispatch an <see cref="ActionExecution"/>.
    /// </summary>
    public void ExecuteStep(StepExecutionId stepExecutionId, StepInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        GuardStatus(WorkflowExecutionStatus.Running, nameof(ExecuteStep));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Start(input);

        if (Definition.GetStepType(step.StepId) == StepType.Action)
            AddDomainEvent(new ActionStartedEvent(Id, stepExecutionId, input));
    }

    /// <summary>
    /// Records a successful step outcome and advances the DAG.
    /// For condition steps the application service passes the branch-value
    /// output so we can fan out into the correct parallel branches.
    /// </summary>
    public void RecordStepCompleted(StepExecutionId stepExecutionId, StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepCompleted));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Complete(output);

        AdvanceOrComplete(step.StepId);
    }

    /// <summary>
    /// Records that a step was skipped (FailureStrategy.Skip).
    /// Treats the step as having completed for graph-advance purposes.
    /// </summary>
    public void RecordStepSkipped(StepExecutionId stepExecutionId)
    {
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepSkipped));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Skip();

        AdvanceOrComplete(step.StepId);
    }

    /// <summary>
    /// Records that a step has definitively failed (stop strategy, retries
    /// exhausted). Transitions the workflow to Failed.
    /// </summary>
    public void RecordStepFailed(StepExecutionId stepExecutionId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepFailed));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Fail(error);

        Status = WorkflowExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new WorkflowFailedEvent(Id, error));
    }

    public void Cancel()
    {
        if (Status is WorkflowExecutionStatus.Completed
                   or WorkflowExecutionStatus.Failed
                   or WorkflowExecutionStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot cancel a workflow execution in '{Status}' status.");

        Status = WorkflowExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        if (Status != WorkflowExecutionStatus.Running)
            throw new InvalidOperationException(
                $"Cannot suspend a workflow execution in '{Status}' status.");

        Status = WorkflowExecutionStatus.Suspended;
    }

    public void Resume()
    {
        if (Status != WorkflowExecutionStatus.Suspended)
            throw new InvalidOperationException(
                $"Cannot resume a workflow execution in '{Status}' status.");

        Status = WorkflowExecutionStatus.Running;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all currently running step executions.
    /// There can be more than one when condition branches execute in parallel.
    /// </summary>
    public IReadOnlyList<StepExecution> GetRunningSteps() =>
        _stepExecutions.Where(s => s.Status == ExecutionStatus.Running).ToList().AsReadOnly();

    /// <summary>Returns all pending step executions awaiting <see cref="ExecuteStep"/>.</summary>
    public IReadOnlyList<StepExecution> GetPendingSteps() =>
        _stepExecutions.Where(s => s.Status == ExecutionStatus.Pending).ToList().AsReadOnly();

    // ── Graph advance ────────────────────────────────────────────────────────

    private void AdvanceOrComplete(StepId completedStepId)
    {
        var stepType = Definition.GetStepType(completedStepId);

        if (stepType == StepType.Condition)
        {
            // Fan out: create one pending StepExecution for the first step
            // of every branch.  Each branch then runs in parallel.
            var branchEntries = Definition.GetConditionBranchEntries(completedStepId);
            foreach (var firstBranchStepId in branchEntries.Values)
                _stepExecutions.Add(new StepExecution(StepExecutionId.New(), firstBranchStepId));

            return; // workflow is not done yet; branches will drive further progress
        }

        // Determine what comes next in the same context (top-level or branch).
        var nextInContext = Definition.GetNextStepIdInContext(completedStepId);

        if (nextInContext is not null)
        {
            // Simple linear advance within the same context.
            _stepExecutions.Add(new StepExecution(StepExecutionId.New(), nextInContext.Value));
            return;
        }

        // The completed step was the last in its context.
        var owningCondition = Definition.GetOwningConditionStepId(completedStepId);

        if (owningCondition is not null)
        {
            // We're inside a branch.  Check if ALL branches of the condition are done.
            if (!AllBranchesDone(owningCondition.Value))
                return; // other branches are still running

            // All branches complete — advance past the condition (merge).
            var afterCondition = Definition.GetStepAfterCondition(owningCondition.Value);
            if (afterCondition is null)
                CompleteWorkflow();
            else
                _stepExecutions.Add(new StepExecution(StepExecutionId.New(), afterCondition.Value));

            return;
        }

        // Top-level step and it was the last — workflow is done.
        CompleteWorkflow();
    }

    private bool AllBranchesDone(StepId conditionStepId)
    {
        var branches = Definition.GetStepInfo(conditionStepId).ConditionBranches!;
        foreach (var branchSteps in branches.Values)
        {
            var lastStepId = branchSteps[^1];
            var lastExecution = _stepExecutions
                .LastOrDefault(s => s.StepId == lastStepId);

            if (lastExecution is null
                || lastExecution.Status is ExecutionStatus.Pending
                                        or ExecutionStatus.Running)
                return false;
        }
        return true;
    }

    private void CompleteWorkflow()
    {
        Status = WorkflowExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new WorkflowCompletedEvent(Id));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private StepExecution GetStepExecutionOrThrow(StepExecutionId stepExecutionId) =>
        _stepExecutions.Find(s => s.Id == stepExecutionId)
        ?? throw new InvalidOperationException(
            $"Step execution '{stepExecutionId}' not found in this workflow execution.");

    private void GuardStatus(WorkflowExecutionStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} a workflow execution in '{Status}' status. Expected '{expected}'.");
    }
}
