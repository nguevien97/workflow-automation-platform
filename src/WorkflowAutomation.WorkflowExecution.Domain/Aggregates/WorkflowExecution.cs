using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;
using WorkflowAutomation.WorkflowLanguage.Domain.Conditions;
using WorkflowAutomation.WorkflowLanguage.Domain.Templates;

namespace WorkflowAutomation.WorkflowExecution.Domain.Aggregates;

public sealed class WorkflowExecution : AggregateRoot<WorkflowExecutionId>
{
    private readonly List<StepExecution> _stepExecutions = [];

    public WorkflowVersionId WorkflowVersionId { get; }
    public WorkflowDefinitionSnapshot Definition { get; }
    public StepId EntryStepId { get; }
    public StepOutput InitialTriggerOutput { get; }
    public ParentExecutionContext? ParentContext { get; }
    public WorkflowExecutionStatus Status { get; private set; }
    public IReadOnlyList<StepExecution> StepExecutions => _stepExecutions.AsReadOnly();
    public DateTime CreatedAt { get; private init; }
    public DateTime? CompletedAt { get; private set; }

    public WorkflowExecution(
        WorkflowExecutionId id,
        WorkflowVersionId workflowVersionId,
        WorkflowDefinitionSnapshot definition,
        StepId entryStepId,
        StepOutput initialTriggerOutput,
        ParentExecutionContext? parentContext = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(initialTriggerOutput);

        WorkflowVersionId = workflowVersionId;
        Definition = definition;
        EntryStepId = entryStepId;
        InitialTriggerOutput = initialTriggerOutput;
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
        if (ParentContext is null && firstStep.StepType != StepType.Trigger)
            throw new InvalidOperationException(
                "The first step must be a Trigger when there is no parent context.");

        ExecuteStep(firstStep.StepId);
        AddDomainEvent(new WorkflowStartedEvent(Id));
    }

    public void RecordStepCompleted(
        StepExecutionId stepExecutionId,
        StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepCompleted));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.CompleteWithOutput(output);

        AddDomainEvent(new StepCompletedEvent(Id, step.StepId, stepExecutionId));
        AdvanceOrComplete(step.Id);
    }

    public void RecordStepSkipped(StepExecutionId stepExecutionId)
    {
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepSkipped));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Skip();

        AddDomainEvent(new StepSkippedEvent(Id, step.StepId, stepExecutionId));
        AdvanceOrComplete(step.Id);
    }

    public void RecordStepFailed(StepExecutionId stepExecutionId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardStatus(WorkflowExecutionStatus.Running, nameof(RecordStepFailed));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Fail(error);

        AddDomainEvent(new StepFailedEvent(Id, step.StepId, stepExecutionId, error));
        FailWorkflow($"Step '{Definition.GetStepInfo(step.StepId).Name}' failed: {error}");
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
        GuardStatus(WorkflowExecutionStatus.Running, nameof(Suspend));
        Status = WorkflowExecutionStatus.Suspended;
    }

    public void Resume()
    {
        GuardStatus(WorkflowExecutionStatus.Suspended, nameof(Resume));
        Status = WorkflowExecutionStatus.Running;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public IReadOnlyList<StepExecution> GetRunningSteps() =>
        _stepExecutions.Where(s => s.Status == ExecutionStatus.Running)
            .ToList().AsReadOnly();

    public IReadOnlyList<StepExecution> GetPendingSteps() =>
        _stepExecutions.Where(s => s.Status == ExecutionStatus.Pending)
            .ToList().AsReadOnly();

    // ── Graph advance ────────────────────────────────────────────────────────

    private void ExecuteStep(StepId stepId)
    {
        if (_stepExecutions.Any(s => s.StepId == stepId))
            throw new InvalidOperationException(
                $"Step execution for step '{stepId}' already exists.");

        var stepInfo = Definition.GetStepInfo(stepId);
        var step = new StepExecution(StepExecutionId.New(), stepId);
        _stepExecutions.Add(step);

        switch (stepInfo.StepType)
        {
            case StepType.Trigger:
                step.Start(input: null);
                step.CompleteWithOutput(InitialTriggerOutput);
                ExecuteStep(stepInfo.NextStepId!.Value);
                break;

            case StepType.Action:
                var actionInfo = (ActionStepInfo)stepInfo;
                var resolvedInput = ResolveInputMappings(actionInfo.InputMappings);
                step.Start(resolvedInput);
                // Action step is now Running — an external handler will
                // call RecordStepCompleted/Skipped/Failed when done.
                AddDomainEvent(new ActionExecutionRequestedEvent(
                    Id, step.Id, step.StepId,
                    actionInfo.IntegrationId, actionInfo.CommandName,
                    resolvedInput, actionInfo.FailureStrategy, actionInfo.MaxRetries));
                break;

            case StepType.Condition:
                var condInfo = (ConditionStepInfo)stepInfo;
                step.Start(input: null);
                var outputsByName = BuildStepOutputsByName();
                StepId? selectedBranchId = null;

                foreach (var rule in condInfo.Rules)
                {
                    var resolvedExpr = TemplateResolver.ResolveText(rule.Expression, outputsByName);
                    if (ConditionEvaluator.Evaluate(resolvedExpr))
                    {
                        selectedBranchId = rule.TargetStepId;
                        break;
                    }
                }

                selectedBranchId ??= condInfo.FallbackStepId;

                step.CompleteWithoutOutput();

                if (selectedBranchId.HasValue)
                {
                    AddDomainEvent(new ConditionBranchSelectedEvent(
                        Id, step.StepId, selectedBranchId.Value));
                    ExecuteStep(selectedBranchId.Value);
                }
                else
                {
                    FailWorkflow(
                        $"No condition rules matched and no fallback defined for step '{stepInfo.Name}'.");
                }
                break;

            case StepType.Parallel:
                var parallelInfo = (ParallelStepInfo)stepInfo;
                step.Start(input: null);
                // Don't complete — stays Running until all branches merge.
                AddDomainEvent(new ParallelBranchesForkedEvent(
                    Id, step.StepId, parallelInfo.BranchEntryStepIds.ToList()));
                foreach (var branchEntryId in parallelInfo.BranchEntryStepIds)
                {
                    ExecuteStep(branchEntryId);
                }
                break;

            case StepType.Loop:
                var loopInfo = (LoopStepInfo)stepInfo;
                var loopOutputsByName = BuildStepOutputsByName();
                var resolvedSource = TemplateResolver.ResolveValue(
                    loopInfo.SourceArrayExpression, loopOutputsByName);
                step.Start(input: null);
                // Loop step stays Running — ActionExecution aggregate
                // manages iteration spawning and result aggregation.
                // Raise event with everything the handler needs.
                AddDomainEvent(new LoopExecutionStartedEvent(
                    Id, step.Id, step.StepId,
                    loopInfo.LoopEntryStepId,
                    resolvedSource,
                    loopInfo.ConcurrencyMode,
                    loopInfo.MaxConcurrency,
                    loopInfo.IterationFailureStrategy,
                    BuildUpstreamOutputsForParentContext()));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported step type '{stepInfo.StepType}'.");
        }
    }

    private void AdvanceOrComplete(StepExecutionId completedStepExecutionId)
    {
        var completedStep = GetStepExecutionOrThrow(completedStepExecutionId);
        var completedStepInfo = Definition.GetStepInfo(completedStep.StepId);

        // If the step has a NextStepId, advance to it.
        if (completedStepInfo.NextStepId.HasValue)
        {
            ExecuteStep(completedStepInfo.NextStepId.Value);
            return;
        }

        // No NextStepId — either end of a parallel branch, end of a
        // condition branch, or end of the entire workflow.

        // Check if this step is inside a parallel branch.
        var owningParallel = Definition.FindOwningParallelStep(completedStep.StepId);
        if (owningParallel is not null)
        {
            var completedStepIds = _stepExecutions
                .Where(s => s.Status is ExecutionStatus.Completed or ExecutionStatus.Skipped)
                .Select(s => s.StepId)
                .ToHashSet();

            if (Definition.AreAllParallelBranchesCompleted(owningParallel, completedStepIds))
            {
                // Mark the parallel step itself as completed.
                var parallelExec = _stepExecutions.First(s => s.StepId == owningParallel.StepId);
                parallelExec.CompleteWithoutOutput();

                AddDomainEvent(new ParallelBranchesMergedEvent(Id, owningParallel.StepId));

                // Advance from the parallel step.
                AdvanceOrComplete(parallelExec.Id);
            }
            // else: other branches still running — do nothing, wait.
            return;
        }

        // Check if this step is the last step of a condition branch.
        // Condition branches have NextStepId = null; the condition step's
        // own NextStepId is the continuation. Find the owning condition.
        var owningCondition = Definition.FindOwningConditionStep(completedStep.StepId);
        if (owningCondition is not null)
        {
            // Condition step was already completed when the branch was selected.
            // Advance from the condition step's NextStepId.
            if (owningCondition.NextStepId.HasValue)
            {
                ExecuteStep(owningCondition.NextStepId.Value);
            }
            else
            {
                // Condition itself has no NextStepId — it might be nested.
                var condExec = _stepExecutions.First(s => s.StepId == owningCondition.StepId);
                AdvanceOrComplete(condExec.Id);
            }
            return;
        }

        // Not inside a parallel or condition branch — this is the end of
        // the workflow.
        CompleteWorkflow();
    }

    // ── Template resolution ──────────────────────────────────────────────────

    /// <summary>
    /// Builds a dictionary of step name → output value for all completed
    /// steps, used for template resolution. "trigger" maps to the initial
    /// trigger output. Parent context outputs are included if present.
    /// </summary>
    private Dictionary<string, IReadOnlyDictionary<string, object>> BuildStepOutputsByName()
    {
        var outputs = new Dictionary<string, IReadOnlyDictionary<string, object>>();

        // Add parent context outputs (for child/loop executions).
        if (ParentContext is not null)
        {
            foreach (var kvp in ParentContext.UpstreamStepOutputs)
                outputs[kvp.Key] = kvp.Value.Data;
        }

        // Add completed step outputs from this execution.
        foreach (var stepExec in _stepExecutions)
        {
            if (stepExec.Output is null) continue;
            var info = Definition.GetStepInfo(stepExec.StepId);

            // For child executions, the trigger output is the iteration item.
            // For root executions, it's the real trigger output.
            // Both are accessible as "trigger".
            if (info.StepType == StepType.Trigger)
                outputs["trigger"] = stepExec.Output.Data;
            else
                outputs[info.Name] = stepExec.Output.Data;
        }

        return outputs;
    }

    private StepInput ResolveInputMappings(IReadOnlyDictionary<string, string> inputMappings)
    {
        var outputsByName = BuildStepOutputsByName();
        var resolved = new Dictionary<string, object>();
        foreach (var kvp in inputMappings)
        {
            resolved[kvp.Key] = TemplateResolver.ResolveValue(kvp.Value, outputsByName);
        }
        return new StepInput(resolved);
    }

    /// <summary>
    /// Builds the upstream step outputs dictionary that will be passed
    /// into child executions via <see cref="ParentExecutionContext"/>.
    /// </summary>
    private Dictionary<string, StepOutput> BuildUpstreamOutputsForParentContext()
    {
        var outputs = new Dictionary<string, StepOutput>();
        foreach (var stepExec in _stepExecutions)
        {
            if (stepExec.Output is null) continue;
            var info = Definition.GetStepInfo(stepExec.StepId);
            if (info.StepType == StepType.Trigger)
                outputs["trigger"] = stepExec.Output;
            else
                outputs[info.Name] = stepExec.Output;
        }
        return outputs;
    }

    // ── Graph queries ────────────────────────────────────────────────────────

    // ── Terminal transitions ─────────────────────────────────────────────────

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

    private StepExecution GetStepExecutionOrThrow(StepExecutionId id) =>
        _stepExecutions.Find(s => s.Id == id)
        ?? throw new InvalidOperationException(
            $"Step execution '{id}' not found.");

    private void GuardStatus(WorkflowExecutionStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} — status is '{Status}', expected '{expected}'.");
    }
}