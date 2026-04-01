using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Aggregates;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;
using WorkflowAutomation.WorkflowLanguage.Domain.Conditions;
using WorkflowAutomation.WorkflowLanguage.Domain.Templates;

namespace WorkflowAutomation.WorkflowExecution.Domain.Tests;

public partial class WorkflowExecutionOwnerBarrierTests
{
    private static WorkflowVersionId VersionId() => WorkflowVersionId.New();
    private static WorkflowExecutionId ExecutionId() => WorkflowExecutionId.New();
    private static StepId Id() => StepId.New();
    private static IntegrationId NewIntegrationId() => IntegrationId.New();

    private static StepOutput Output(params (string key, object value)[] values) =>
        new(values.ToDictionary(x => x.key, x => x.value));

    private static WorkflowDefinitionSnapshot Snapshot(params StepDefinitionInfo[] steps) =>
        new(steps.ToList());

    private static TriggerStepInfo Trigger(string name, StepId id, StepId nextStepId) =>
        new(id, name, NewIntegrationId(), "trigger", new Dictionary<string, string>(), nextStepId);

    private static ActionStepInfo Action(
        string name,
        StepId id,
        StepId? nextStepId = null,
        IReadOnlyDictionary<string, string>? inputMappings = null) =>
        new(id, name, NewIntegrationId(), "action", inputMappings ?? new Dictionary<string, string>(), FailureStrategy.Stop, 0, nextStepId);

    private static ConditionStepInfo Condition(
        string name,
        StepId id,
        IReadOnlyList<ConditionRuleInfo> rules,
        StepId? nextStepId = null,
        StepId? fallbackStepId = null) =>
        new(id, name, rules, nextStepId, fallbackStepId);

    private static ParallelStepInfo Parallel(
        string name,
        StepId id,
        IReadOnlyList<StepId> branchEntryStepIds,
        StepId? nextStepId = null) =>
        new(id, name, branchEntryStepIds, nextStepId);

    private static LoopStepInfo Loop(
        string name,
        StepId id,
        string sourceArrayExpression,
        StepId loopEntryStepId,
        StepId? nextStepId = null) =>
        new(id, name, sourceArrayExpression, loopEntryStepId, ConcurrencyMode.Sequential, IterationFailureStrategy.Skip, nextStepId);

    private static ConditionRuleInfo Rule(string expression, StepId targetStepId) =>
        new(expression, targetStepId);

    private static WorkflowAutomation.WorkflowExecution.Domain.Aggregates.WorkflowExecution BuildExecution(
        WorkflowDefinitionSnapshot snapshot,
        StepId entryStepId,
        StepOutput? triggerOutput = null,
        ParentExecutionContext? parentContext = null) =>
        new(ExecutionId(), VersionId(), snapshot, entryStepId, triggerOutput ?? Output(("seed", "alpha"), ("items", new[] { 1, 2 })), parentContext);

    private static void CompleteStep(
        WorkflowAutomation.WorkflowExecution.Domain.Aggregates.WorkflowExecution execution,
        StepExecutionId stepExecutionId,
        StepOutput output)
    {
        var stepExecution = execution.StepExecutions.Single(step => step.Id == stepExecutionId);
        var stepInfo = execution.Definition.GetStepInfo(stepExecution.StepId);

        switch (stepInfo.StepType)
        {
            case StepType.Action:
                execution.RecordActionCompleted(stepExecutionId, output);
                break;

            case StepType.Loop:
                execution.RecordLoopCompleted(stepExecutionId, output);
                break;

            default:
                throw new InvalidOperationException(
                    $"Test helper cannot externally complete a '{stepInfo.StepType}' step.");
        }
    }

    private static void FailStep(
        WorkflowAutomation.WorkflowExecution.Domain.Aggregates.WorkflowExecution execution,
        StepExecutionId stepExecutionId,
        string error)
    {
        var stepExecution = execution.StepExecutions.Single(step => step.Id == stepExecutionId);
        var stepInfo = execution.Definition.GetStepInfo(stepExecution.StepId);

        switch (stepInfo.StepType)
        {
            case StepType.Action:
                execution.RecordActionFailed(stepExecutionId, error);
                break;

            case StepType.Loop:
                execution.RecordLoopFailed(stepExecutionId, error);
                break;

            default:
                throw new InvalidOperationException(
                    $"Test helper cannot externally fail a '{stepInfo.StepType}' step.");
        }
    }

    [Fact]
    public void RecordActionCompleted_ForLoopStep_Throws()
    {
        var trigger = Id();
        var loop = Id();
        var body = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, loop),
                Loop("Loop", loop, "{{trigger.items}}", body),
                Action("Body", body)),
            trigger,
            Output(("items", new[] { 1, 2 })));

        execution.Start();

        var loopExecution = execution.GetRunningSteps().Single(step => step.StepId == loop);

        var ex = Assert.Throws<InvalidOperationException>(
            () => execution.RecordActionCompleted(loopExecution.Id, Output(("result", "wrong"))));

        Assert.Contains("expected 'Action'", ex.Message);
    }

    [Fact]
    public void RecordLoopCompleted_ForActionStep_Throws()
    {
        var trigger = Id();
        var action = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("A", action)),
            trigger);

        execution.Start();

        var actionExecution = execution.GetRunningSteps().Single(step => step.StepId == action);

        var ex = Assert.Throws<InvalidOperationException>(
            () => execution.RecordLoopCompleted(actionExecution.Id, Output(("result", "wrong"))));

        Assert.Contains("expected 'Loop'", ex.Message);
    }

    [Fact]
    public void ConditionBranchCompletion_AdvancesToOwnerContinuation()
    {
        var trigger = Id();
        var route = Id();
        var branch = Id();
        var fallback = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, route),
                Condition("Route", route, [Rule("'go' == 'go'", branch)], nextStepId: after, fallbackStepId: fallback),
                Action("Branch", branch),
                Action("Fallback", fallback),
                Action("After", after)),
            trigger,
            Output(("seed", "alpha")));

        execution.Start();

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        var runningSteps = execution.GetRunningSteps();
        Assert.Equal(2, runningSteps.Count); // Condition stays Running + branch action
        Assert.Contains(runningSteps, s => s.StepId == route);
        var branchExecution = runningSteps.Single(s => s.StepId == branch);
        Assert.Contains(execution.DomainEvents, e => e is ConditionBranchSelectedEvent branchSelected && branchSelected.SelectedBranchEntryStepId == branch);

        CompleteStep(execution, branchExecution.Id, Output(("branchOut", "done")));

        var afterExecution = Assert.Single(execution.GetRunningSteps());
        Assert.Equal(after, afterExecution.StepId);
        Assert.Contains(execution.DomainEvents, e => e is ActionExecutionRequestedEvent requested && requested.StepId == after);
    }

    [Fact]
    public void ConditionWithoutMatchingRuleOrFallback_FailsConditionStepAndWorkflow()
    {
        var trigger = Id();
        var route = Id();
        var branch = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, route),
                Condition("Route", route, [Rule("'go' == 'stop'", branch)]),
                Action("Branch", branch)),
            trigger,
            Output(("seed", "alpha")));

        execution.Start();

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Empty(execution.GetRunningSteps());

        var conditionExecution = Assert.Single(execution.StepExecutions, step => step.StepId == route);
        Assert.Equal(ExecutionStatus.Failed, conditionExecution.Status);
        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == branch);
        Assert.Contains(execution.DomainEvents,
            e => e is StepFailedEvent failed
                && failed.StepId == route
                && failed.Error.Contains("no condition rules matched", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(execution.DomainEvents,
            e => e is WorkflowFailedEvent failed
                && failed.Error.Contains("no condition rules matched", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(execution.DomainEvents, e => e is ConditionBranchSelectedEvent);
    }

    [Fact]
    public void Snapshot_FindsDirectOwner_ForNestedBranchStep()
    {
        var trigger = Id();
        var outerCondition = Id();
        var parallel = Id();
        var innerBranch = Id();
        var siblingBranch = Id();
        var afterParallel = Id();
        var fallback = Id();
        var afterCondition = Id();

        var snapshot = Snapshot(
            Trigger("T", trigger, outerCondition),
            Condition("Route", outerCondition, [Rule("'go' == 'go'", parallel)], nextStepId: afterCondition, fallbackStepId: fallback),
            Parallel("Fork", parallel, [innerBranch, siblingBranch], nextStepId: afterParallel),
            Action("InnerBranch", innerBranch),
            Action("SiblingBranch", siblingBranch),
            Action("AfterParallel", afterParallel),
            Action("Fallback", fallback),
            Action("AfterCondition", afterCondition));

        // InnerBranch's direct owner is the Parallel, not the outer Condition.
        var parallelOwner = snapshot.FindOwningParallelStep(innerBranch);
        Assert.NotNull(parallelOwner);
        Assert.Equal(parallel, parallelOwner!.StepId);
        Assert.Null(snapshot.FindOwningConditionStep(innerBranch));

        // The Parallel's direct owner is the outer Condition.
        var conditionOwner = snapshot.FindOwningConditionStep(parallel);
        Assert.NotNull(conditionOwner);
        Assert.Equal(outerCondition, conditionOwner!.StepId);
    }

    [Fact]
    public void ParallelOwner_WaitsForAllBranchesBeforeAdvancing()
    {
        var trigger = Id();
        var parallel = Id();
        var left = Id();
        var right = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [left, right], nextStepId: after),
                Action("Left", left),
                Action("Right", right),
                Action("After", after)),
            trigger);

        execution.Start();

        var runningStepIds = execution.GetRunningSteps().Select(step => step.StepId).ToHashSet();
        Assert.Equal(3, runningStepIds.Count);
        Assert.Contains(parallel, runningStepIds);
        Assert.Contains(left, runningStepIds);
        Assert.Contains(right, runningStepIds);

        var leftExecution = execution.GetRunningSteps().Single(step => step.StepId == left);
        CompleteStep(execution, leftExecution.Id, Output(("left", "done")));

        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == after);
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        var rightExecution = execution.GetRunningSteps().Single(step => step.StepId == right);
        CompleteStep(execution, rightExecution.Id, Output(("right", "done")));

        var afterExecution = execution.GetRunningSteps().Single(step => step.StepId == after);
        Assert.Equal(after, afterExecution.StepId);
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent merged && merged.ParallelStepId == parallel);
    }

    [Fact]
    public void LoopOwnerCompletion_AdvancesToOwnerContinuation()
    {
        var trigger = Id();
        var fetch = Id();
        var loop = Id();
        var body = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, fetch),
                Action("Fetch", fetch, nextStepId: loop, inputMappings: new Dictionary<string, string> { ["seed"] = "{{trigger.seed}}" }),
                Loop("MyLoop", loop, "{{Fetch.rows}}", body, nextStepId: after),
                Action("Body", body),
                Action("After", after)),
            trigger,
            Output(("seed", "alpha"), ("rows", new[] { 1, 2, 3 })));

        execution.Start();

        var fetchExecution = execution.GetRunningSteps().Single(step => step.StepId == fetch);
        CompleteStep(execution, fetchExecution.Id, Output(("rows", new[] { 1, 2, 3 })));

        var loopExecution = execution.GetRunningSteps().Single(step => step.StepId == loop);
        Assert.Contains(execution.DomainEvents, e => e is LoopExecutionStartedEvent loopStarted && loopStarted.LoopStepId == loop);

        CompleteStep(execution, loopExecution.Id, Output(("processed", new[] { "a", "b" })));

        var afterExecution = execution.GetRunningSteps().Single(step => step.StepId == after);
        Assert.Equal(after, afterExecution.StepId);
    }

    [Fact]
    public void LoopInsideParallelBranch_ReturnsToBranchThenToParallelContinuation()
    {
        var trigger = Id();
        var parallel = Id();
        var loop = Id();
        var loopBody = Id();
        var branchAfterLoop = Id();
        var sibling = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("OuterParallel", parallel, [loop, sibling], nextStepId: after),
                Loop("LoopBranch", loop, "{{trigger.items}}", loopBody, nextStepId: branchAfterLoop),
                Action("LoopBody", loopBody),
                Action("BranchAfterLoop", branchAfterLoop),
                Action("Sibling", sibling),
                Action("AfterParallel", after)),
            trigger,
            Output(("items", new[] { 1, 2 })));

        execution.Start();

        var siblingExecution = execution.GetRunningSteps().Single(step => step.StepId == sibling);
        var loopExecution = execution.GetRunningSteps().Single(step => step.StepId == loop);

        CompleteStep(execution, loopExecution.Id, Output(("processed", new[] { "x" })));

        var branchAfterLoopExecution = execution.GetRunningSteps().Single(step => step.StepId == branchAfterLoop);
        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == after);

        CompleteStep(execution, siblingExecution.Id, Output(("siblingOut", "done")));
        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == after);

        CompleteStep(execution, branchAfterLoopExecution.Id, Output(("branchSummary", "ok")));

        var afterExecution = execution.GetRunningSteps().Single(step => step.StepId == after);
        Assert.Equal(after, afterExecution.StepId);
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent merged && merged.ParallelStepId == parallel);
    }

    /// <summary>
    /// Condition at the end of a parallel branch. The condition dispatches
    /// an action step, but the parallel merge must NOT happen until that
    /// action completes. After the workflow finishes, no steps should be
    /// left Running.
    ///
    ///   Parallel
    ///     ├─ Branch A: ActionLeft (null)
    ///     └─ Branch B: Condition (null)          ← terminal of branch B
    ///                      └─ rule → ActionInBranch (null)
    ///   After
    /// </summary>
    [Fact]
    public void ConditionInParallelBranch_ParallelMustWaitForConditionBranchToComplete()
    {
        var trigger = Id();
        var parallel = Id();
        var actionLeft = Id();
        var condition = Id();
        var actionInBranch = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [actionLeft, condition], nextStepId: after),
                Action("ActionLeft", actionLeft),
                Condition("Route", condition, [Rule("'go' == 'go'", actionInBranch)]),
                Action("ActionInBranch", actionInBranch),
                Action("After", after)),
            trigger);

        execution.Start();

        // ActionLeft and ActionInBranch should both be running.
        // The condition dispatched ActionInBranch via the selected rule.
        var actionLeftExec = execution.StepExecutions.Single(s => s.StepId == actionLeft);
        var actionInBranchExec = execution.StepExecutions.Single(s => s.StepId == actionInBranch);
        Assert.Equal(ExecutionStatus.Running, actionLeftExec.Status);
        Assert.Equal(ExecutionStatus.Running, actionInBranchExec.Status);

        // Complete ActionLeft — parallel must NOT merge yet because
        // ActionInBranch (inside the condition's branch) is still running.
        CompleteStep(execution, actionLeftExec.Id, Output(("left", "done")));

        Assert.DoesNotContain(execution.DomainEvents,
            e => e is ParallelBranchesMergedEvent);
        Assert.DoesNotContain(execution.StepExecutions,
            s => s.StepId == after);
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // Now complete ActionInBranch — parallel should merge and advance.
        CompleteStep(execution, actionInBranchExec.Id, Output(("branch", "done")));

        Assert.Contains(execution.DomainEvents,
            e => e is ParallelBranchesMergedEvent merged && merged.ParallelStepId == parallel);

        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    /// <summary>
    /// When the workflow completes, there must be zero steps still in
    /// Running status. This scenario uses a condition inside a parallel
    /// branch to expose orphaned running steps.
    ///
    ///   Parallel
    ///     ├─ Branch A: ActionLeft (null)
    ///     └─ Branch B: Condition (null)
    ///                      └─ rule → ActionInBranch (null)
    /// </summary>
    [Fact]
    public void NoRunningStepsAfterWorkflowCompleted_ConditionInParallelBranch()
    {
        var trigger = Id();
        var parallel = Id();
        var actionLeft = Id();
        var condition = Id();
        var actionInBranch = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [actionLeft, condition]),
                Action("ActionLeft", actionLeft),
                Condition("Route", condition, [Rule("'go' == 'go'", actionInBranch)]),
                Action("ActionInBranch", actionInBranch)),
            trigger);

        execution.Start();

        var actionLeftExec = execution.StepExecutions.Single(s => s.StepId == actionLeft);
        var actionInBranchExec = execution.StepExecutions.Single(s => s.StepId == actionInBranch);

        CompleteStep(execution, actionLeftExec.Id, Output(("left", "done")));
        CompleteStep(execution, actionInBranchExec.Id, Output(("branch", "done")));

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);

        // The invariant: no running steps after completion.
        var runningSteps = execution.StepExecutions
            .Where(s => s.Status == ExecutionStatus.Running)
            .Select(s => s.StepId)
            .ToList();
        Assert.Empty(runningSteps);
    }

    [Fact]
    public void NoRunningStepsAfterWorkflowFailed_ActionFailureInParallelBranch()
    {
        var trigger = Id();
        var parallel = Id();
        var left = Id();
        var right = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [left, right]),
                Action("Left", left),
                Action("Right", right)),
            trigger);

        execution.Start();

        var leftExecution = Assert.Single(execution.GetRunningSteps(), step => step.StepId == left);
        FailStep(execution, leftExecution.Id, "boom");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);

        var runningSteps = execution.StepExecutions
            .Where(s => s.Status == ExecutionStatus.Running)
            .Select(s => s.StepId)
            .ToList();
        Assert.Empty(runningSteps);
    }

    [Fact]
    public void NoRunningStepsAfterWorkflowCancelled()
    {
        var trigger = Id();
        var action = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Send", action)),
            trigger);

        execution.Start();
        execution.Cancel();

        Assert.Equal(WorkflowExecutionStatus.Cancelled, execution.Status);

        var runningSteps = execution.StepExecutions
            .Where(s => s.Status == ExecutionStatus.Running)
            .Select(s => s.StepId)
            .ToList();
        Assert.Empty(runningSteps);
    }

    /// <summary>
    /// Condition with a multi-step branch inside a parallel branch.
    /// The parallel must wait for the entire chain, not just the condition.
    ///
    ///   Parallel
    ///     ├─ Branch A: ActionLeft (null)
    ///     └─ Branch B: Condition (null)
    ///                      └─ rule → Action1 → Action2 (null)
    /// </summary>
    [Fact]
    public void ConditionWithMultiStepBranch_InParallel_WaitsForEntireChain()
    {
        var trigger = Id();
        var parallel = Id();
        var actionLeft = Id();
        var condition = Id();
        var action1 = Id();
        var action2 = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [actionLeft, condition], nextStepId: after),
                Action("ActionLeft", actionLeft),
                Condition("Route", condition, [Rule("'go' == 'go'", action1)]),
                Action("Action1", action1, nextStepId: action2),
                Action("Action2", action2),
                Action("After", after)),
            trigger);

        execution.Start();

        var actionLeftExec = execution.StepExecutions.Single(s => s.StepId == actionLeft);
        var action1Exec = execution.StepExecutions.Single(s => s.StepId == action1);

        // Complete left branch and first step of condition branch.
        CompleteStep(execution, actionLeftExec.Id, Output(("left", "done")));
        CompleteStep(execution, action1Exec.Id, Output(("a1", "done")));

        // Parallel must NOT merge yet — Action2 is still running.
        Assert.DoesNotContain(execution.DomainEvents,
            e => e is ParallelBranchesMergedEvent);
        Assert.DoesNotContain(execution.StepExecutions,
            s => s.StepId == after);

        // Complete the chain.
        var action2Exec = execution.GetRunningSteps().Single(s => s.StepId == action2);
        CompleteStep(execution, action2Exec.Id, Output(("a2", "done")));

        Assert.Contains(execution.DomainEvents,
            e => e is ParallelBranchesMergedEvent merged && merged.ParallelStepId == parallel);
        Assert.Single(execution.GetRunningSteps(), s => s.StepId == after);
    }
}
