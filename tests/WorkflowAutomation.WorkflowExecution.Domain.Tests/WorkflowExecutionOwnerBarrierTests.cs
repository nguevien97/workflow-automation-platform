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
        var branchExecution = Assert.Single(execution.GetRunningSteps());
        Assert.Equal(branch, branchExecution.StepId);
        Assert.Contains(execution.DomainEvents, e => e is ConditionBranchSelectedEvent branchSelected && branchSelected.SelectedBranchEntryStepId == branch);

        execution.RecordStepCompleted(branchExecution.Id, Output(("branchOut", "done")));

        var afterExecution = Assert.Single(execution.GetRunningSteps());
        Assert.Equal(after, afterExecution.StepId);
        Assert.Contains(execution.DomainEvents, e => e is ActionExecutionRequestedEvent requested && requested.StepId == after);
    }

    [Fact]
    public void Snapshot_FindsOwningConditionStep_ForNestedBranchStep()
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

        var owner = snapshot.FindOwningConditionStep(innerBranch);

        Assert.NotNull(owner);
        Assert.Equal(outerCondition, owner!.StepId);
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
        execution.RecordStepCompleted(leftExecution.Id, Output(("left", "done")));

        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == after);
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        var rightExecution = execution.GetRunningSteps().Single(step => step.StepId == right);
        execution.RecordStepCompleted(rightExecution.Id, Output(("right", "done")));

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
        execution.RecordStepCompleted(fetchExecution.Id, Output(("rows", new[] { 1, 2, 3 })));

        var loopExecution = execution.GetRunningSteps().Single(step => step.StepId == loop);
        Assert.Contains(execution.DomainEvents, e => e is LoopExecutionStartedEvent loopStarted && loopStarted.LoopStepId == loop);

        execution.RecordStepCompleted(loopExecution.Id, Output(("processed", new[] { "a", "b" })));

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

        execution.RecordStepCompleted(loopExecution.Id, Output(("processed", new[] { "x" })));

        var branchAfterLoopExecution = execution.GetRunningSteps().Single(step => step.StepId == branchAfterLoop);
        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == after);

        execution.RecordStepCompleted(siblingExecution.Id, Output(("siblingOut", "done")));
        Assert.DoesNotContain(execution.StepExecutions, step => step.StepId == after);

        execution.RecordStepCompleted(branchAfterLoopExecution.Id, Output(("branchSummary", "ok")));

        var afterExecution = execution.GetRunningSteps().Single(step => step.StepId == after);
        Assert.Equal(after, afterExecution.StepId);
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent merged && merged.ParallelStepId == parallel);
    }
}