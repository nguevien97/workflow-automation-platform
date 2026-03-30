using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Aggregates;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

using WorkflowExecutionAggregate = WorkflowAutomation.WorkflowExecution.Domain.Aggregates.WorkflowExecution;

namespace WorkflowAutomation.WorkflowExecution.Domain.Tests;

public partial class WorkflowExecutionOwnerBarrierTests
{
    [Fact]
    public void ReviewStep_Approve_AdvancesToNextStep()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, nextStepId: after),
                Action("After", after)),
            trigger);

        execution.Start();

        var actionExecution = execution.GetRunningSteps().Single(step => step.StepId == action);
        execution.RecordStepCompleted(actionExecution.Id, Output(("draftId", "123")));

        var reviewExecution = execution.GetRunningSteps().Single(step => step.StepId == review);
        Assert.Contains(execution.DomainEvents, domainEvent => domainEvent.GetType().Name == "ReviewStepReachedEvent");

        ApproveReviewStep(execution, reviewExecution.Id);

        var afterExecution = execution.GetRunningSteps().Single(step => step.StepId == after);
        Assert.Equal(after, afterExecution.StepId);
    }

    [Fact]
    public void ReviewStep_Reject_RewindsToTarget()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action)),
            trigger);

        execution.Start();

        var firstActionExecution = execution.GetRunningSteps().Single(step => step.StepId == action);
        execution.RecordStepCompleted(firstActionExecution.Id, Output(("draftId", "123")));

        var reviewExecution = execution.GetRunningSteps().Single(step => step.StepId == review);
        RejectReviewStep(execution, reviewExecution.Id, "needs changes");

        var rerunActionExecution = execution.GetRunningSteps().Single(step => step.StepId == action);
        Assert.NotEqual(firstActionExecution.Id, rerunActionExecution.Id);
        Assert.DoesNotContain(execution.StepExecutions, step => step.Id == firstActionExecution.Id);
        Assert.DoesNotContain(execution.StepExecutions, step => step.Id == reviewExecution.Id);
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        Assert.Contains(execution.DomainEvents, domainEvent => domainEvent.GetType().Name == "ReviewStepRejectedEvent");
    }

    [Fact]
    public void ReviewStep_MaxRejections_BoundaryCheck_FailsOnRecordedLimit()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, maxRejections: 2)),
            trigger);

        execution.Start();

        var firstActionExecution = execution.GetRunningSteps().Single(step => step.StepId == action);
        execution.RecordStepCompleted(firstActionExecution.Id, Output(("draftId", "one")));

        var firstReviewExecution = execution.GetRunningSteps().Single(step => step.StepId == review);
        RejectReviewStep(execution, firstReviewExecution.Id, "first rejection");

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        var secondActionExecution = execution.GetRunningSteps().Single(step => step.StepId == action);
        execution.RecordStepCompleted(secondActionExecution.Id, Output(("draftId", "two")));

        var secondReviewExecution = execution.GetRunningSteps().Single(step => step.StepId == review);
        RejectReviewStep(execution, secondReviewExecution.Id, "second rejection");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Contains(execution.DomainEvents, domainEvent => domainEvent is WorkflowFailedEvent);
    }

    [Fact]
    public void ReviewStep_InsideParallelBranch_ExhaustsMaxRejections_FailsWorkflow()
    {
        var trigger = Id();
        var parallel = Id();
        var branchAction = Id();
        var branchReview = Id();
        var sibling = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [branchAction, sibling]),
                Action("BranchAction", branchAction, nextStepId: branchReview),
                Review("BranchReview", branchReview, branchAction, maxRejections: 2),
                Action("Sibling", sibling)),
            trigger);

        execution.Start();

        var siblingExecution = execution.GetRunningSteps().Single(step => step.StepId == sibling);
        var firstBranchActionExecution = execution.GetRunningSteps().Single(step => step.StepId == branchAction);
        execution.RecordStepCompleted(firstBranchActionExecution.Id, Output(("draftId", "one")));

        var firstReviewExecution = execution.GetRunningSteps().Single(step => step.StepId == branchReview);
        RejectReviewStep(execution, firstReviewExecution.Id, "first rejection");

        Assert.Equal(ExecutionStatus.Running, siblingExecution.Status);

        var secondBranchActionExecution = execution.GetRunningSteps().Single(step => step.StepId == branchAction);
        execution.RecordStepCompleted(secondBranchActionExecution.Id, Output(("draftId", "two")));

        var secondReviewExecution = execution.GetRunningSteps().Single(step => step.StepId == branchReview);
        RejectReviewStep(execution, secondReviewExecution.Id, "second rejection");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(ExecutionStatus.Cancelled, siblingExecution.Status);
    }

    [Fact]
    public void ReviewStep_InsideLoopIteration_ExhaustsMaxRejections_FailsChildExecution()
    {
        var loop = Id();
        var bodyAction = Id();
        var review = Id();

        var parentContext = new ParentExecutionContext(
            ExecutionId(),
            loop,
            new Dictionary<string, StepOutput>());

        var execution = BuildExecution(
            Snapshot(
                Action("BodyAction", bodyAction, nextStepId: review),
                Review("Review", review, bodyAction, maxRejections: 2)),
            bodyAction,
            parentContext: parentContext);

        execution.Start();

        var firstBodyExecution = execution.GetRunningSteps().Single(step => step.StepId == bodyAction);
        execution.RecordStepCompleted(firstBodyExecution.Id, Output(("body", "one")));

        var firstReviewExecution = execution.GetRunningSteps().Single(step => step.StepId == review);
        RejectReviewStep(execution, firstReviewExecution.Id, "first rejection");

        var secondBodyExecution = execution.GetRunningSteps().Single(step => step.StepId == bodyAction);
        execution.RecordStepCompleted(secondBodyExecution.Id, Output(("body", "two")));

        var secondReviewExecution = execution.GetRunningSteps().Single(step => step.StepId == review);
        RejectReviewStep(execution, secondReviewExecution.Id, "second rejection");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Contains(execution.DomainEvents, domainEvent => domainEvent is WorkflowFailedEvent);
    }

    private static StepDefinitionInfo Review(
        string name,
        StepId id,
        StepId rejectionTargetStepId,
        int maxRejections = 3,
        StepId? nextStepId = null) =>
        new ReviewStepInfo(id, name, rejectionTargetStepId, maxRejections, nextStepId);

    private static void ApproveReviewStep(WorkflowExecutionAggregate execution, StepExecutionId stepExecutionId) =>
        execution.ApproveReviewStep(stepExecutionId);

    private static void RejectReviewStep(
        WorkflowExecutionAggregate execution,
        StepExecutionId stepExecutionId,
        string reason) =>
        execution.RejectReviewStep(stepExecutionId, reason);
}