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
    public WorkflowVersionId WorkflowVersionId { get; }
    private readonly Dictionary<string, object>? ParentContext;
    private readonly StepOutput InitialTriggerOutput;
    private readonly StepId EntryStepId;
    public WorkflowDefinitionSnapshot Definition { get; }
    public WorkflowExecutionStatus Status { get; private set; }
    public IReadOnlyList<StepExecution> StepExecutions => _stepExecutions.AsReadOnly();
    public DateTime CreatedAt { get; private init; }
    public DateTime? CompletedAt { get; private set; }

    public WorkflowExecution(
        WorkflowExecutionId id,
        WorkflowDefinitionSnapshot definition,
        StepOutput initialTriggerOutput,
        StepId entryStepId,
        Dictionary<string, object>? parentContext = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(initialTriggerOutput);
        Definition = definition;
        InitialTriggerOutput = initialTriggerOutput;
        EntryStepId = entryStepId;
        ParentContext = parentContext;
        Status = WorkflowExecutionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public void Start()
    {
        GuardStatus(WorkflowExecutionStatus.Pending, nameof(Start));

        Status = WorkflowExecutionStatus.Running;

        var firstStep = Definition.GetStepInfo(EntryStepId);
        if (ParentContext is null)
        {
            if (firstStep.StepType != StepType.Trigger)
                throw new InvalidOperationException(
                    "The first step of the workflow must be a Trigger when there is no parent context.");
        }
        
        ExecuteStep(firstStep.StepId);
        AddDomainEvent(new WorkflowStartedEvent(Id));
    }

    
    public void RecordStepCompleted(StepExecutionId stepExecutionId, StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepCompleted));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Complete(output);

        AdvanceOrComplete(step.Id);
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

        AdvanceOrComplete(step.Id);
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
    private void ExecuteStep(StepId stepId)
    {
        if (_stepExecutions.Any(s => s.StepId == stepId))
            throw new InvalidOperationException(
                $"A step execution for step ID '{stepId}' already exists. Duplicate step IDs are not allowed in the same workflow execution.");
        
        var input = GetStepExecutionInput(stepId);
        var stepInfo = Definition.GetStepInfo(stepId);
        var step = new StepExecution(StepExecutionId.New(), stepId);
        _stepExecutions.Add(step);
        step.Start(input);
        
        switch (stepInfo.StepType)
        {
            case StepType.Trigger:
                step.Complete(InitialTriggerOutput);
                ExecuteStep(stepInfo.NextStepId!.Value);
                break;
            case StepType.Action:
                // fire ActionTriggered domain event, which will be handled by ActionExecution aggregate
                break;
            case StepType.Condition:
                var conditionStepInfo = (ConditionStepInfo)stepInfo;
                var matchingRule = conditionStepInfo.Rules.FirstOrDefault(r => EvaluateCondition(r));
                var nextStepId = matchingRule != default
                    ? matchingRule.TargetStepId
                    : conditionStepInfo.FallbackStepId;
                step.Complete(null);
                if (nextStepId.HasValue)
                {
                    ExecuteStep(nextStepId.Value);
                }
                else
                {
                    // no next step means the workflow fails, which follows the requirements
                    // fire WorkflowFailedEvent with appropriate error message
                    FailWorkflow($"No condition rules matched and no fallback step defined for condition step '{stepInfo.StepId}'.");
                }
                break;
            case StepType.Loop:
                var loopStepInfo = (LoopStepInfo)stepInfo;
                // evaluate input and fire WorkflowExecutionSpawned here
                break;
            case StepType.Parallel:
                var parallelStepInfo = (ParallelStepInfo)stepInfo;
                // traverse branches until first action step
                foreach (var branchEntryStepId in parallelStepInfo.BranchEntryStepIds)
                {
                    ExecuteStep(branchEntryStepId);
                }
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported step type '{stepInfo.StepType}' for execution.");
        }
    }
    
    private void AdvanceOrComplete(StepExecutionId completedStepExecutionId)
    {
        var completedStep = GetStepExecutionOrThrow(completedStepExecutionId);
        var completedStepInfo = Definition.GetStepInfo(completedStep.StepId);

        if (completedStepInfo.NextStepId.HasValue)
        {
            ExecuteStep(completedStepInfo.NextStepId.Value);
            return;
        }

        // if there is no next step, it is either end of the workflow or end of a parallel branch.
        // TODO: find the parallel step that faned out this branch (if any) and check if all its branches are completed. If there is no such a parallel step, check if all steps are completed. If so, complete the workflow.
    }

    private void CompleteWorkflow()
    {
        Status = WorkflowExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new WorkflowCompletedEvent(Id));
    }

    private void FailWorkflow(string error)
    {
        Status = WorkflowExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new WorkflowFailedEvent(Id, error));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private StepExecution GetStepExecutionOrThrow(StepExecutionId stepExecutionId) =>
        _stepExecutions.Find(s => s.Id == stepExecutionId)
        ?? throw new InvalidOperationException(
            $"Step execution '{stepExecutionId}' not found in this workflow execution.");

    private StepInput? GetStepExecutionInput(StepId stepId)
    {
        var stepInfo = Definition.GetStepInfo(stepId);
        if (stepInfo.StepType == StepType.Trigger || stepInfo.StepType == StepType.Parallel || stepInfo.StepType == StepType.Condition)
        {
            return null;
        }

        if (stepInfo.StepType == StepType.Action)
        {
            var actionStepInfo = (ActionStepInfo)stepInfo;
            // resolve input from previous step's output
            return null; // placeholder for actual input resolution logic
        }

        if (stepInfo.StepType == StepType.Loop)
        {
            var loopStepInfo = (LoopStepInfo)stepInfo;
            // resolve input from loop collection or previous step's output
            return null; // placeholder for actual input resolution logic
        }

        throw new InvalidOperationException(
            $"Unsupported step type '{stepInfo.StepType}' for input resolution.");
    }

    private bool EvaluateCondition(ConditionRuleInfo rule)
    {
        // placeholder for actual condition evaluation logic
        return false;
    }

    private void GuardStatus(WorkflowExecutionStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} a workflow execution in '{Status}' status. Expected '{expected}'.");
    }
}
