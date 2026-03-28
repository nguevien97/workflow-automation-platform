using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;

namespace WorkflowAutomation.WorkflowExecution.Domain.Tests;

public partial class WorkflowExecutionOwnerBarrierTests
{
    [Fact]
    public void ActionInputMappings_PreserveTypedValues()
    {
        var trigger = Id();
        var action = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action(
                    "Send",
                    action,
                    inputMappings: new Dictionary<string, string>
                    {
                        ["items"] = "{{trigger.items}}",
                        ["count"] = "{{trigger.count}}",
                        ["message"] = "Count {{trigger.count}}"
                    })),
            trigger,
            Output(("items", new[] { 1, 2, 3 }), ("count", 3)));

        execution.Start();

        var requested = Assert.Single(execution.DomainEvents.OfType<ActionExecutionRequestedEvent>());

        var items = Assert.IsType<int[]>(requested.Input.Data["items"]);
        Assert.Equal(new[] { 1, 2, 3 }, items);
        Assert.IsType<int>(requested.Input.Data["count"]);
        Assert.Equal(3, requested.Input.Data["count"]);
        Assert.Equal("Count 3", requested.Input.Data["message"]);
    }

    [Fact]
    public void LoopSource_PreservesCollectionValue()
    {
        var trigger = Id();
        var loop = Id();
        var body = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, loop),
                Loop("EachItem", loop, "{{trigger.items}}", body),
                Action("Body", body)),
            trigger,
            Output(("items", new[] { 1, 2, 3 })));

        execution.Start();

        var loopStarted = Assert.Single(execution.DomainEvents.OfType<LoopExecutionStartedEvent>());
        Assert.Equal(3, loopStarted.SourceItems.Count);
        Assert.Equal(new object?[] { 1, 2, 3 }, loopStarted.SourceItems.Items);
    }

    [Fact]
    public void LoopSource_MustResolveToEnumerableCollection()
    {
        var trigger = Id();
        var loop = Id();
        var body = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, loop),
                Loop("EachItem", loop, "{{trigger.items}}", body),
                Action("Body", body)),
            trigger,
            Output(("items", "not-a-collection")));

        execution.Start();

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        var loopExecution = Assert.Single(execution.StepExecutions, step => step.StepId == loop);
        Assert.Equal(ExecutionStatus.Failed, loopExecution.Status);
        Assert.Contains(execution.DomainEvents, e => e is WorkflowFailedEvent failed && failed.Error.Contains("enumerable collection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(execution.DomainEvents, e => e is LoopExecutionStartedEvent);
    }
}