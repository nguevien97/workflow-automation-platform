using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Aggregates;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;

using WorkflowExecutionAggregate = WorkflowAutomation.WorkflowExecution.Domain.Aggregates.WorkflowExecution;

namespace WorkflowAutomation.WorkflowExecution.Domain.Tests;

public partial class WorkflowExecutionOwnerBarrierTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Basic Approve / Reject
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Review(target:A) → B
    /// Approve the review step; execution advances to B.
    /// </summary>
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

    /// <summary>
    /// T → A → Review(target:A). Terminal review step.
    /// Approve completes the workflow.
    /// </summary>
    [Fact]
    public void ReviewStep_Approve_TerminalReview_CompletesWorkflow()
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

        var actionExecution = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExecution.Id, Output(("draftId", "123")));

        var reviewExecution = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExecution.Id);

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        Assert.Contains(execution.DomainEvents, e => e is WorkflowCompletedEvent);
    }

    /// <summary>
    /// T → A → Review(target:A).
    /// Reject invalidates A and review; re-executes from A with a new StepExecution.
    /// </summary>
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

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Multi-Step Invalidation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → B → C → Review(target:A).
    /// Reject invalidates A, B, C, and Review — four steps in the range.
    /// Re-executes from A.
    /// </summary>
    [Fact]
    public void ReviewStep_RejectBackMultipleSteps_InvalidatesEntireRange()
    {
        var trigger = Id();
        var a = Id();
        var b = Id();
        var c = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: b),
                Action("B", b, nextStepId: c),
                Action("C", c, nextStepId: review),
                Review("Review", review, a)),
            trigger);

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "2")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "3")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "rework all");

        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == aExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == bExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == cExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == reviewExec.Id);

        var newA = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, newA.Id);
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Max Rejections & Boundary Checks
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// maxRejections=2. First rejection rewinds; second rejection fails workflow.
    /// Verifies the count fires exactly at the limit, not before or after.
    /// </summary>
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

    /// <summary>
    /// maxRejections=3. Two rejections keep running; third fails.
    /// Full approve-after-reject cycle in between.
    /// </summary>
    [Fact]
    public void ReviewStep_MaxRejections_ThreeLimit_FailsOnThirdRejection()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, maxRejections: 3)),
            trigger);

        execution.Start();

        for (var i = 0; i < 2; i++)
        {
            var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
            execution.RecordStepCompleted(actionExec.Id, Output(("draftId", $"attempt-{i}")));
            var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
            RejectReviewStep(execution, reviewExec.Id, $"rejection-{i}");
            Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        }

        var finalAction = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(finalAction.Id, Output(("draftId", "attempt-2")));
        var finalReview = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, finalReview.Id, "third rejection");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
    }

    /// <summary>
    /// maxRejections=1. The very first rejection fails the workflow.
    /// </summary>
    [Fact]
    public void ReviewStep_MaxRejectionsOne_FirstRejectionFails()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, maxRejections: 1)),
            trigger);

        execution.Start();

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExec.Id, Output(("draftId", "only")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "fail immediately");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Contains(execution.DomainEvents, e => e is WorkflowFailedEvent);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. Rejection History & Snapshots
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Review(target:A) → After.
    /// Reject once (invalidates A + Review), then re-execute A and approve.
    /// Workflow continues to After. Verifies the full reject-then-approve lifecycle.
    /// (Rejection history snapshot assertions deferred until _rejectionHistory is exposed.)
    /// </summary>
    [Fact]
    public void ReviewStep_RejectThenApprove_WorkflowContinuesToNextStep()
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

        var firstAction = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(firstAction.Id, Output(("draftId", "v1")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "wrong format");

        var secondAction = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(secondAction.Id, Output(("draftId", "v2")));

        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. Downstream Rejection Resets Upstream Counter (Supersede)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → ReviewA(target:A, max:3) → B → ReviewB(target:A, max:3) → C
    ///
    /// Cycle 1: A rejects twice (active count=2), then approves. B reaches.
    /// Cycle 2: B rejects → rewinds to A. A's two prior records become superseded.
    /// Cycle 3: A can now reject 3 more times (fresh counter) before failing.
    /// </summary>
    [Fact]
    public void ReviewStep_DownstreamRejection_SupersedesUpstreamCounter()
    {
        var trigger = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var reviewB = Id();
        var c = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: reviewA),
                Review("ReviewA", reviewA, a, maxRejections: 3, nextStepId: b),
                Action("B", b, nextStepId: reviewB),
                Review("ReviewB", reviewB, a, maxRejections: 3, nextStepId: c),
                Action("C", c)),
            trigger);

        execution.Start();

        // A rejects twice at ReviewA
        for (var i = 0; i < 2; i++)
        {
            var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
            execution.RecordStepCompleted(aExec.Id, Output(("a", $"v{i}")));
            var rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
            RejectReviewStep(execution, rA.Id, $"reviewA-reject-{i}");
            Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        }

        // A passes ReviewA this time
        var aPass = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aPass.Id, Output(("a", "v2-pass")));
        var rAPass = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        ApproveReviewStep(execution, rAPass.Id);

        // B completes, reaches ReviewB
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "done")));

        // ReviewB rejects back to A — this supersedes ReviewA's prior records
        var rB = execution.GetRunningSteps().Single(s => s.StepId == reviewB);
        RejectReviewStep(execution, rB.Id, "reviewB rejects back to A");
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // Now A's counter is reset. It can reject 3 more times before failing.
        for (var i = 0; i < 2; i++)
        {
            var aNew = execution.GetRunningSteps().Single(s => s.StepId == a);
            execution.RecordStepCompleted(aNew.Id, Output(("a", $"cycle2-{i}")));
            var rANew = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
            RejectReviewStep(execution, rANew.Id, $"cycle2-reviewA-reject-{i}");
            Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        }

        // Third rejection in the new cycle should fail
        var aFinal = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aFinal.Id, Output(("a", "cycle2-2")));
        var rAFinal = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        RejectReviewStep(execution, rAFinal.Id, "cycle2-reviewA-reject-2");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
    }

    /// <summary>
    /// Three sequential review gates: A → ReviewA → B → ReviewB → C → ReviewC → D.
    /// ReviewC rejects to A; this supersedes both ReviewA and ReviewB counters.
    /// Then ReviewA rejects again — its counter starts at 0.
    /// </summary>
    [Fact]
    public void ReviewStep_DeepChain_DownstreamRejectionSupersedes_AllUpstream()
    {
        var trigger = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var reviewB = Id();
        var c = Id();
        var reviewC = Id();
        var d = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: reviewA),
                Review("ReviewA", reviewA, a, maxRejections: 2, nextStepId: b),
                Action("B", b, nextStepId: reviewB),
                Review("ReviewB", reviewB, b, maxRejections: 2, nextStepId: c),
                Action("C", c, nextStepId: reviewC),
                Review("ReviewC", reviewC, a, maxRejections: 2, nextStepId: d),
                Action("D", d)),
            trigger);

        execution.Start();

        // ReviewA rejects once
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));
        var rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        RejectReviewStep(execution, rA.Id, "reviewA-reject-1");
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // ReviewA approves, move through B, ReviewB approves, move through C
        aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "2")));
        rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        ApproveReviewStep(execution, rA.Id);

        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));
        var rB = execution.GetRunningSteps().Single(s => s.StepId == reviewB);
        ApproveReviewStep(execution, rB.Id);

        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "1")));

        // ReviewC rejects all the way back to A — supersedes ReviewA and ReviewB
        var rC = execution.GetRunningSteps().Single(s => s.StepId == reviewC);
        RejectReviewStep(execution, rC.Id, "reviewC rejects to A");
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // ReviewA's counter is now 0 (prior rejection superseded). Can reject again.
        aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "3")));
        rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        RejectReviewStep(execution, rA.Id, "reviewA-reject-post-supersede");
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // Second rejection at ReviewA now fails (maxRejections=2, count=2)
        aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "4")));
        rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        RejectReviewStep(execution, rA.Id, "reviewA-reject-final");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. Error Handling — Invalid Operations
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calling RejectReviewStep on an Action step throws.
    /// </summary>
    [Fact]
    public void ReviewStep_RejectNonReviewStep_Throws()
    {
        var trigger = Id();
        var action = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Work", action)),
            trigger);

        execution.Start();

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        Assert.Throws<InvalidOperationException>(
            () => RejectReviewStep(execution, actionExec.Id, "not a review"));
    }

    /// <summary>
    /// Calling ApproveReviewStep on an Action step throws.
    /// </summary>
    [Fact]
    public void ApproveReviewStep_OnActionStep_Throws()
    {
        var trigger = Id();
        var action = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Work", action)),
            trigger);

        execution.Start();

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        Assert.Throws<InvalidOperationException>(
            () => ApproveReviewStep(execution, actionExec.Id));
    }

    /// <summary>
    /// Calling ApproveReviewStep on an already-completed review step throws.
    /// </summary>
    [Fact]
    public void ReviewStep_ApproveAlreadyCompleted_Throws()
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

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExec.Id, Output(("draftId", "123")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec.Id);

        // Review is now completed. Approving again should throw.
        Assert.Throws<InvalidOperationException>(
            () => ApproveReviewStep(execution, reviewExec.Id));
    }

    /// <summary>
    /// Cannot approve/reject on a cancelled workflow.
    /// </summary>
    [Fact]
    public void ReviewStep_ActionOnCancelledWorkflow_Throws()
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
        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExec.Id, Output(("draftId", "123")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        execution.Cancel();

        Assert.Throws<InvalidOperationException>(
            () => ApproveReviewStep(execution, reviewExec.Id));
        Assert.Throws<InvalidOperationException>(
            () => RejectReviewStep(execution, reviewExec.Id, "too late"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. Review Inside Condition Branches
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → Condition(rule → A → Review(target:A)) → After.
    /// Review inside a condition branch targets A (same branch), rejects,
    /// re-executes A within the branch context. Then approve, workflow advances.
    /// </summary>
    [Fact]
    public void ReviewStep_InsideConditionBranch_RejectAndApprove()
    {
        var trigger = Id();
        var condition = Id();
        var branchAction = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, condition),
                Condition("Route", condition, [Rule("'go' == 'go'", branchAction)], nextStepId: after),
                Action("BranchWork", branchAction, nextStepId: review),
                Review("BranchReview", review, branchAction),
                Action("After", after)),
            trigger);

        execution.Start();

        // Complete the branch action
        var branchExec = execution.GetRunningSteps().Single(s => s.StepId == branchAction);
        execution.RecordStepCompleted(branchExec.Id, Output(("branch", "v1")));

        // Reject at the review step
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "rework branch");

        // A new branch action execution starts
        var newBranchExec = execution.GetRunningSteps().Single(s => s.StepId == branchAction);
        Assert.NotEqual(branchExec.Id, newBranchExec.Id);

        execution.RecordStepCompleted(newBranchExec.Id, Output(("branch", "v2")));

        // Now approve
        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        // Condition branch completes → condition completes → After step runs
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    /// <summary>
    /// Condition with two branches each having their own review step.
    /// Branch selection via fallback. Only selected branch's review executes.
    /// </summary>
    [Fact]
    public void ReviewStep_ConditionWithFallback_OnlySelectedBranchReviewExecutes()
    {
        var trigger = Id();
        var condition = Id();
        var branchA = Id();
        var reviewA = Id();
        var branchB = Id();
        var reviewB = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, condition),
                Condition("Route", condition,
                    [Rule("'go' == 'stop'", branchA)],
                    nextStepId: after,
                    fallbackStepId: branchB),
                Action("BranchA", branchA, nextStepId: reviewA),
                Review("ReviewA", reviewA, branchA),
                Action("BranchB", branchB, nextStepId: reviewB),
                Review("ReviewB", reviewB, branchB),
                Action("After", after)),
            trigger);

        execution.Start();

        // Fallback branch selected (rule doesn't match)
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == branchA);
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == branchB);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "done")));

        var reviewBExec = execution.GetRunningSteps().Single(s => s.StepId == reviewB);
        ApproveReviewStep(execution, reviewBExec.Id);

        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == reviewA);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. Review Inside Parallel Branches
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallel(Branch1: A → Review(target:A), Branch2: B).
    /// Review rejects in Branch1 → Branch1 rewinds locally,
    /// Branch2 remains untouched and the parallel waits.
    /// </summary>
    [Fact]
    public void ReviewStep_InsideParallelBranch_RejectRewindsOnlyThatBranch()
    {
        var trigger = Id();
        var parallel = Id();
        var branchAction = Id();
        var branchReview = Id();
        var sibling = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [branchAction, sibling], nextStepId: after),
                Action("BranchAction", branchAction, nextStepId: branchReview),
                Review("BranchReview", branchReview, branchAction),
                Action("Sibling", sibling),
                Action("After", after)),
            trigger);

        execution.Start();

        var siblingExec = execution.GetRunningSteps().Single(s => s.StepId == sibling);
        var branchExec = execution.GetRunningSteps().Single(s => s.StepId == branchAction);
        execution.RecordStepCompleted(branchExec.Id, Output(("branch", "v1")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == branchReview);
        RejectReviewStep(execution, reviewExec.Id, "rework branch");

        // Sibling untouched
        Assert.Equal(ExecutionStatus.Running, siblingExec.Status);

        // Branch1 re-executes from branchAction
        var newBranch = execution.GetRunningSteps().Single(s => s.StepId == branchAction);
        Assert.NotEqual(branchExec.Id, newBranch.Id);

        // Parallel still waiting
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == after);
    }

    /// <summary>
    /// Parallel(Branch1: A → Review(target:A, max:2), Branch2: B).
    /// Branch1 exhausts max rejections → entire workflow fails.
    /// Branch2 is cancelled.
    /// </summary>
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

    /// <summary>
    /// Parallel with 3 branches, each having a review step.
    /// Branch1 rejects once, Branch2 approves, Branch3 rejects once.
    /// Then Branch1 and Branch3 approve → parallel merges → After runs.
    /// </summary>
    [Fact]
    public void ReviewStep_MultipleParallelBranches_IndependentRejectApprove_ThenMerge()
    {
        var trigger = Id();
        var parallel = Id();
        var a1 = Id();
        var r1 = Id();
        var a2 = Id();
        var r2 = Id();
        var a3 = Id();
        var r3 = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [a1, a2, a3], nextStepId: after),
                Action("A1", a1, nextStepId: r1),
                Review("R1", r1, a1),
                Action("A2", a2, nextStepId: r2),
                Review("R2", r2, a2),
                Action("A3", a3, nextStepId: r3),
                Review("R3", r3, a3),
                Action("After", after)),
            trigger);

        execution.Start();

        // Branch 1: complete A1, reject at R1
        var a1Exec = execution.GetRunningSteps().Single(s => s.StepId == a1);
        execution.RecordStepCompleted(a1Exec.Id, Output(("a1", "v1")));
        var r1Exec = execution.GetRunningSteps().Single(s => s.StepId == r1);
        RejectReviewStep(execution, r1Exec.Id, "rework branch1");

        // Branch 2: complete A2, approve R2
        var a2Exec = execution.GetRunningSteps().Single(s => s.StepId == a2);
        execution.RecordStepCompleted(a2Exec.Id, Output(("a2", "v1")));
        var r2Exec = execution.GetRunningSteps().Single(s => s.StepId == r2);
        ApproveReviewStep(execution, r2Exec.Id);

        // Branch 3: complete A3, reject at R3
        var a3Exec = execution.GetRunningSteps().Single(s => s.StepId == a3);
        execution.RecordStepCompleted(a3Exec.Id, Output(("a3", "v1")));
        var r3Exec = execution.GetRunningSteps().Single(s => s.StepId == r3);
        RejectReviewStep(execution, r3Exec.Id, "rework branch3");

        // Parallel not merged yet — branches 1 and 3 still in progress
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == after);

        // Branch 1: re-execute, approve
        var a1Redo = execution.GetRunningSteps().Single(s => s.StepId == a1);
        execution.RecordStepCompleted(a1Redo.Id, Output(("a1", "v2")));
        var r1Redo = execution.GetRunningSteps().Single(s => s.StepId == r1);
        ApproveReviewStep(execution, r1Redo.Id);

        // Still not merged — branch 3 pending
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == after);

        // Branch 3: re-execute, approve
        var a3Redo = execution.GetRunningSteps().Single(s => s.StepId == a3);
        execution.RecordStepCompleted(a3Redo.Id, Output(("a3", "v2")));
        var r3Redo = execution.GetRunningSteps().Single(s => s.StepId == r3);
        ApproveReviewStep(execution, r3Redo.Id);

        // Now all branches done → merge → After
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent m && m.ParallelStepId == parallel);
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. Review Inside Loop Bodies
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loop body: A → Review(target:A, max:2).
    /// Iteration 1 exhausts max rejections → child execution fails.
    /// </summary>
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

    /// <summary>
    /// Two independent loop iteration child executions.
    /// Iteration 1 rejects twice and approves. Iteration 2 starts at count=0.
    /// Proves per-WorkflowExecutionId scoping.
    /// </summary>
    [Fact]
    public void ReviewStep_InsideLoop_EachIterationHasOwnCounter()
    {
        var loop = Id();
        var bodyAction = Id();
        var review = Id();
        var afterReview = Id();

        // Iteration 1
        var parentContext1 = new ParentExecutionContext(
            ExecutionId(),
            loop,
            new Dictionary<string, StepOutput>());

        var iter1 = BuildExecution(
            Snapshot(
                Action("BodyAction", bodyAction, nextStepId: review),
                Review("Review", review, bodyAction, maxRejections: 3, nextStepId: afterReview),
                Action("AfterReview", afterReview)),
            bodyAction,
            parentContext: parentContext1);

        iter1.Start();

        // Iteration 1 rejects twice
        for (var i = 0; i < 2; i++)
        {
            var body = iter1.GetRunningSteps().Single(s => s.StepId == bodyAction);
            iter1.RecordStepCompleted(body.Id, Output(("body", $"iter1-{i}")));
            var rev = iter1.GetRunningSteps().Single(s => s.StepId == review);
            RejectReviewStep(iter1, rev.Id, $"iter1-reject-{i}");
            Assert.Equal(WorkflowExecutionStatus.Running, iter1.Status);
        }

        // Iteration 1 approves
        var body1Pass = iter1.GetRunningSteps().Single(s => s.StepId == bodyAction);
        iter1.RecordStepCompleted(body1Pass.Id, Output(("body", "iter1-pass")));
        var rev1Pass = iter1.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(iter1, rev1Pass.Id);

        var afterExec1 = iter1.GetRunningSteps().Single(s => s.StepId == afterReview);
        Assert.Equal(afterReview, afterExec1.StepId);

        // Iteration 2 — fresh execution, counter starts at 0
        var parentContext2 = new ParentExecutionContext(
            ExecutionId(),
            loop,
            new Dictionary<string, StepOutput>());

        var iter2 = BuildExecution(
            Snapshot(
                Action("BodyAction", bodyAction, nextStepId: review),
                Review("Review", review, bodyAction, maxRejections: 3, nextStepId: afterReview),
                Action("AfterReview", afterReview)),
            bodyAction,
            parentContext: parentContext2);

        iter2.Start();

        // Iteration 2 can reject (proves counter=0)
        var body2 = iter2.GetRunningSteps().Single(s => s.StepId == bodyAction);
        iter2.RecordStepCompleted(body2.Id, Output(("body", "iter2-0")));
        var rev2 = iter2.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(iter2, rev2.Id, "iter2-reject-0");
        Assert.Equal(WorkflowExecutionStatus.Running, iter2.Status);
    }

    /// <summary>
    /// Loop iteration with IterationFailureStrategy=Skip and review that fails.
    /// The child execution fails but the parent should interpret it as skip.
    /// We only verify the child execution fails here (parent loop is out of
    /// scope for the execution aggregate).
    /// </summary>
    [Fact]
    public void ReviewStep_InsideLoop_ExhaustsMaxRejections_ChildExecutionFails()
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
                Review("Review", review, bodyAction, maxRejections: 1)),
            bodyAction,
            parentContext: parentContext);

        execution.Start();

        var body = execution.GetRunningSteps().Single(s => s.StepId == bodyAction);
        execution.RecordStepCompleted(body.Id, Output(("body", "only")));
        var rev = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, rev.Id, "immediate fail");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. Nested Scope Invalidation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Condition(rule → B → C) → Review(target: A).
    /// Rejection invalidates A, the Condition, B, C, and the Review.
    /// Steps inside the condition scope are collected recursively.
    /// </summary>
    [Fact]
    public void ReviewStep_RejectWithNestedCondition_InvalidatesConditionScopeSteps()
    {
        var trigger = Id();
        var a = Id();
        var condition = Id();
        var b = Id();
        var c = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: condition),
                Condition("Route", condition, [Rule("'go' == 'go'", b)], nextStepId: review),
                Action("B", b, nextStepId: c),
                Action("C", c),
                Review("Review", review, a)),
            trigger);

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));

        // Condition selects branch B → C
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "1")));

        // Review reached after condition completes
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo everything");

        // All steps from A to Review (including condition scope) are invalidated
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == aExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == bExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == cExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == reviewExec.Id);

        // Re-executes from A
        var newA = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, newA.Id);
    }

    /// <summary>
    /// T → A → Parallel(Branch1: B, Branch2: C → D) → Review(target: A).
    /// Rejection invalidates A, the Parallel, B, C, D, and the Review.
    /// All nested parallel branch steps are collected via recursive scope expansion.
    /// </summary>
    [Fact]
    public void ReviewStep_RejectWithNestedParallel_InvalidatesAllBranchSteps()
    {
        var trigger = Id();
        var a = Id();
        var parallel = Id();
        var b = Id();
        var c = Id();
        var d = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: parallel),
                Parallel("Fork", parallel, [b, c], nextStepId: review),
                Action("B", b),
                Action("C", c, nextStepId: d),
                Action("D", d),
                Review("Review", review, a)),
            trigger);

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));

        // Parallel branches run
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));
        execution.RecordStepCompleted(cExec.Id, Output(("c", "1")));

        var dExec = execution.GetRunningSteps().Single(s => s.StepId == d);
        execution.RecordStepCompleted(dExec.Id, Output(("d", "1")));

        // Parallel merges, review reached
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo");

        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == aExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == bExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == cExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == dExec.Id);

        var newA = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, newA.Id);
    }

    /// <summary>
    /// T → A → Loop(body: X → Y) → Review(target: A).
    /// Rejection invalidates A, the Loop, and the Review.
    /// Loop body (X, Y) runs as child execution, so the parent only sees
    /// the Loop step itself — not individual body steps.
    /// </summary>
    [Fact]
    public void ReviewStep_RejectWithNestedLoop_InvalidatesLoopStep()
    {
        var trigger = Id();
        var a = Id();
        var loop = Id();
        var x = Id();
        var y = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: loop),
                Loop("MyLoop", loop, "{{A.items}}", x, nextStepId: review),
                Action("X", x, nextStepId: y),
                Action("Y", y),
                Review("Review", review, a)),
            trigger,
            Output(("seed", "alpha"), ("items", new[] { 1, 2 })));

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("items", new[] { 1, 2 })));

        // Loop starts — stays running, emits LoopExecutionStartedEvent
        var loopExec = execution.GetRunningSteps().Single(s => s.StepId == loop);
        Assert.Contains(execution.DomainEvents, e => e is LoopExecutionStartedEvent);

        // External handler completes the loop
        execution.RecordStepCompleted(loopExec.Id, Output(("loopResult", "done")));

        // Review reached
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo loop");

        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == aExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == loopExec.Id);

        var newA = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, newA.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 11. Complex: Review After Parallel (Reject Invalidates Parallel)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Parallel(B1: X → Y, B2: Z) → Review(target: A) → After.
    ///
    /// Complete entire parallel, reach review, reject back to A.
    /// All of A, Parallel (with X, Y, Z), and Review are invalidated.
    /// Re-execute from A, complete parallel again, approve review → After runs.
    /// </summary>
    [Fact]
    public void ReviewStep_AfterParallel_RejectInvalidatesParallelAndReRuns()
    {
        var trigger = Id();
        var a = Id();
        var parallel = Id();
        var x = Id();
        var y = Id();
        var z = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: parallel),
                Parallel("Fork", parallel, [x, z], nextStepId: review),
                Action("X", x, nextStepId: y),
                Action("Y", y),
                Action("Z", z),
                Review("Review", review, a, nextStepId: after),
                Action("After", after)),
            trigger);

        execution.Start();

        // Cycle 1: A → Parallel(X→Y, Z) → Review → Reject
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "c1")));

        var xExec = execution.GetRunningSteps().Single(s => s.StepId == x);
        var zExec = execution.GetRunningSteps().Single(s => s.StepId == z);
        execution.RecordStepCompleted(xExec.Id, Output(("x", "c1")));
        var yExec = execution.GetRunningSteps().Single(s => s.StepId == y);
        execution.RecordStepCompleted(zExec.Id, Output(("z", "c1")));
        execution.RecordStepCompleted(yExec.Id, Output(("y", "c1")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo everything");

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // Cycle 2: re-execute from A
        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, aExec2.Id);
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "c2")));

        var xExec2 = execution.GetRunningSteps().Single(s => s.StepId == x);
        var zExec2 = execution.GetRunningSteps().Single(s => s.StepId == z);
        execution.RecordStepCompleted(xExec2.Id, Output(("x", "c2")));
        var yExec2 = execution.GetRunningSteps().Single(s => s.StepId == y);
        execution.RecordStepCompleted(zExec2.Id, Output(("z", "c2")));
        execution.RecordStepCompleted(yExec2.Id, Output(("y", "c2")));

        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 12. Complex: Review After Condition (Reject Invalidates Condition)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Condition(rule → B → C, fallback → D) → Review(target: A) → After.
    ///
    /// First cycle: rule matches, B → C execute, review rejects.
    /// Second cycle: condition may select a different branch because A produces
    /// new output. Both cycles re-evaluate the condition.
    /// </summary>
    [Fact]
    public void ReviewStep_AfterCondition_RejectCausesConditionReEvaluation()
    {
        var trigger = Id();
        var a = Id();
        var condition = Id();
        var b = Id();
        var c = Id();
        var d = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: condition),
                Condition("Route", condition,
                    [Rule("'go' == 'go'", b)],
                    nextStepId: review,
                    fallbackStepId: d),
                Action("B", b, nextStepId: c),
                Action("C", c),
                Action("D", d),
                Review("Review", review, a, nextStepId: after),
                Action("After", after)),
            trigger);

        execution.Start();

        // Cycle 1
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));

        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "1")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo");

        // Cycle 2: A re-executes → condition re-evaluates
        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "2")));

        // Condition re-evaluates (same rule should match again)
        var bExec2 = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec2.Id, Output(("b", "2")));
        var cExec2 = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec2.Id, Output(("c", "2")));

        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 13. Complex: Review in Parallel Branch with Condition Inside
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallel(
    ///   Branch1: A → Condition(rule → B) → Review(target: A),
    ///   Branch2: C
    /// ) → After.
    ///
    /// Branch1 has a condition with nested action, followed by a review.
    /// Rejection in Branch1 invalidates A, Condition(+B), and Review.
    /// Branch2 is untouched.
    /// </summary>
    [Fact]
    public void ReviewStep_ParallelBranchWithCondition_RejectInvalidatesNestedScope()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var condition = Id();
        var b = Id();
        var review = Id();
        var c = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [a, c], nextStepId: after),
                Action("A", a, nextStepId: condition),
                Condition("Route", condition, [Rule("'go' == 'go'", b)], nextStepId: review),
                Action("B", b),
                Review("Review", review, a),
                Action("C", c),
                Action("After", after)),
            trigger);

        execution.Start();

        // Branch1: A → Condition(→B) → Review
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));

        // Review reached
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo");

        // Branch1 rewinds to A; Branch2 (C) untouched
        var cExec = execution.StepExecutions.Single(s => s.StepId == c);
        Assert.Equal(ExecutionStatus.Running, cExec.Status);

        var newA = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, newA.Id);

        // Complete everything to verify workflow can finish
        execution.RecordStepCompleted(newA.Id, Output(("a", "2")));
        var newB = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(newB.Id, Output(("b", "2")));

        var newReview = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, newReview.Id);

        execution.RecordStepCompleted(cExec.Id, Output(("c", "done")));

        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent);
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 14. Complex: Multi-Review Chain with Mixed Reject/Approve
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → ReviewA(target:A) → B → ReviewB(target:B) → C.
    ///
    /// ReviewA rejects once, approves on retry.
    /// ReviewB rejects once, approves on retry.
    /// Workflow completes at C.
    ///
    /// Verifies two independent review gates on the same path, each with
    /// their own rejection targets and counters.
    /// </summary>
    [Fact]
    public void ReviewStep_TwoSequentialReviews_IndependentRejectApprove()
    {
        var trigger = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var reviewB = Id();
        var c = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: reviewA),
                Review("ReviewA", reviewA, a, nextStepId: b),
                Action("B", b, nextStepId: reviewB),
                Review("ReviewB", reviewB, b, nextStepId: c),
                Action("C", c)),
            trigger);

        execution.Start();

        // A → ReviewA rejects
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "v1")));
        var rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        RejectReviewStep(execution, rA.Id, "rework A");

        // A → ReviewA approves
        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "v2")));
        var rA2 = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        ApproveReviewStep(execution, rA2.Id);

        // B → ReviewB rejects
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "v1")));
        var rB = execution.GetRunningSteps().Single(s => s.StepId == reviewB);
        RejectReviewStep(execution, rB.Id, "rework B");

        // B → ReviewB approves
        var bExec2 = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec2.Id, Output(("b", "v2")));
        var rB2 = execution.GetRunningSteps().Single(s => s.StepId == reviewB);
        ApproveReviewStep(execution, rB2.Id);

        // C runs
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        Assert.Equal(c, cExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 15. Stress: Repeated Reject-Rerun Cycles
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Review(target:A, max:10).
    /// Rejects 9 times and approves on the 10th attempt.
    /// Proves the system handles many reject-rerun cycles correctly.
    /// </summary>
    [Fact]
    public void ReviewStep_ManyRejectCycles_EventualApprove()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, maxRejections: 10)),
            trigger);

        execution.Start();

        for (var i = 0; i < 9; i++)
        {
            var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
            execution.RecordStepCompleted(actionExec.Id, Output(("draftId", $"attempt-{i}")));
            var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
            RejectReviewStep(execution, reviewExec.Id, $"rejection-{i}");
            Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        }

        // 10th attempt: approve
        var finalAction = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(finalAction.Id, Output(("draftId", "final")));
        var finalReview = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, finalReview.Id);

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 16. No Running Steps After Review-Triggered Failure
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When max rejections is reached and workflow fails, there must be
    /// zero running steps.
    /// </summary>
    [Fact]
    public void ReviewStep_MaxRejectionsFailure_NoRunningSteps()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, maxRejections: 1)),
            trigger);

        execution.Start();

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExec.Id, Output(("draftId", "only")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "fail");

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Empty(execution.GetRunningSteps());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 17. Events Verification
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verifies that ReviewStepReachedEvent is emitted exactly once per
    /// review step entry (including after rejection rewind).
    /// </summary>
    [Fact]
    public void ReviewStep_EmitsReachedEvent_OnEachEntry()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, action),
                Action("Draft", action, nextStepId: review),
                Review("Review", review, action, maxRejections: 3)),
            trigger);

        execution.Start();

        // First entry
        var a1 = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(a1.Id, Output(("draftId", "v1")));
        var reachedEvents1 = execution.DomainEvents.Count(e => e.GetType().Name == "ReviewStepReachedEvent");

        // Reject → second entry
        var r1 = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, r1.Id, "reject");

        var a2 = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(a2.Id, Output(("draftId", "v2")));
        var reachedEvents2 = execution.DomainEvents.Count(e => e.GetType().Name == "ReviewStepReachedEvent");

        Assert.Equal(reachedEvents1 + 1, reachedEvents2);
    }

    /// <summary>
    /// Verifies ReviewStepRejectedEvent includes correct reason.
    /// </summary>
    [Fact]
    public void ReviewStep_RejectedEvent_ContainsReason()
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

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExec.Id, Output(("draftId", "123")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "budget exceeds limit");

        var rejectedEvent = execution.DomainEvents
            .OfType<ReviewStepRejectedEvent>()
            .Single();
        Assert.Equal("budget exceeds limit", rejectedEvent.Reason);
        Assert.Equal(review, rejectedEvent.ReviewStepId);
        Assert.Equal(action, rejectedEvent.TargetStepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 18. Complex End-to-End: Full Workflow with All Step Types
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Condition(rule → B) → Parallel(X, Y) → Review(target: A, max:2) → Loop → After.
    ///
    /// Full workflow traversal with condition, parallel, review (reject once),
    /// then loop, then completion. Exercises all step types interacting with review.
    /// </summary>
    [Fact]
    public void ReviewStep_FullWorkflow_ConditionParallelReviewLoop_EndToEnd()
    {
        var trigger = Id();
        var a = Id();
        var condition = Id();
        var b = Id();
        var parallel = Id();
        var x = Id();
        var y = Id();
        var review = Id();
        var loop = Id();
        var loopBody = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: condition),
                Condition("Route", condition, [Rule("'go' == 'go'", b)], nextStepId: parallel),
                Action("B", b),
                Parallel("Fork", parallel, [x, y], nextStepId: review),
                Action("X", x),
                Action("Y", y),
                Review("Review", review, a, maxRejections: 2, nextStepId: loop),
                Loop("MyLoop", loop, "{{A.items}}", loopBody, nextStepId: after),
                Action("LoopBody", loopBody),
                Action("After", after)),
            trigger,
            Output(("seed", "alpha"), ("items", new[] { 1, 2 })));

        execution.Start();

        // === Cycle 1: Reject ===

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("items", new[] { 1, 2 })));

        // Condition selects B
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));

        // Parallel: X and Y
        var xExec = execution.GetRunningSteps().Single(s => s.StepId == x);
        var yExec = execution.GetRunningSteps().Single(s => s.StepId == y);
        execution.RecordStepCompleted(xExec.Id, Output(("x", "1")));
        execution.RecordStepCompleted(yExec.Id, Output(("y", "1")));

        // Review reached — reject back to A
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo everything");

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);

        // === Cycle 2: Approve ===

        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec2.Id, Output(("items", new[] { 1, 2 })));

        var bExec2 = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec2.Id, Output(("b", "2")));

        var xExec2 = execution.GetRunningSteps().Single(s => s.StepId == x);
        var yExec2 = execution.GetRunningSteps().Single(s => s.StepId == y);
        execution.RecordStepCompleted(xExec2.Id, Output(("x", "2")));
        execution.RecordStepCompleted(yExec2.Id, Output(("y", "2")));

        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        // Loop starts
        var loopExec = execution.GetRunningSteps().Single(s => s.StepId == loop);
        Assert.Contains(execution.DomainEvents, e => e is LoopExecutionStartedEvent);

        // External handler completes the loop
        execution.RecordStepCompleted(loopExec.Id, Output(("loopResult", "done")));

        // After step
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 19. Parallel with Review in One Branch + Condition in Other Branch
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallel(
    ///   Branch1: A → Review(target:A),
    ///   Branch2: Condition(rule → B → C, fallback → D)
    /// ) → After.
    ///
    /// Branch1 rejects and rewinds while Branch2 evaluates its condition.
    /// Both branches must complete before parallel merges.
    /// </summary>
    [Fact]
    public void ReviewStep_ParallelBranchReview_WithConditionInOtherBranch()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var review = Id();
        var condition = Id();
        var b = Id();
        var c = Id();
        var d = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [a, condition], nextStepId: after),
                Action("A", a, nextStepId: review),
                Review("Review", review, a),
                Condition("Route", condition, [Rule("'go' == 'go'", b)], fallbackStepId: d),
                Action("B", b, nextStepId: c),
                Action("C", c),
                Action("D", d),
                Action("After", after)),
            trigger);

        execution.Start();

        // Branch1: A → Review → Reject
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "v1")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "rework");

        // Branch2: Condition → B → C (completes)
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "done")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "done")));

        // Parallel not merged — Branch1 still in progress
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == after);

        // Branch1: A re-execute → Review → Approve
        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "v2")));
        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        // Now both branches complete → merge → After
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent);
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 20. Review Step as Entry to Parallel Branch
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallel(Branch1: B → Review(target:B), Branch2: C) → After.
    ///
    /// Review is the terminal step of a multi-step parallel branch.
    /// Approve Branch1's review, complete Branch2 → parallel merges → After runs.
    /// </summary>
    [Fact]
    public void ReviewStep_AtEndOfMultiStepParallelBranch_ApproveAdvances()
    {
        var trigger = Id();
        var parallel = Id();
        var b = Id();
        var review = Id();
        var c = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [b, c], nextStepId: after),
                Action("B", b, nextStepId: review),
                Review("Review", review, b),
                Action("C", c),
                Action("After", after)),
            trigger);

        execution.Start();

        // Branch1: B → Review
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "done")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec.Id);

        // Branch2: C
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "done")));

        // Merge → After
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent);
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 21. Deep Nesting: Condition → Parallel → Review
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → Condition(rule → Parallel(B1: A → Review(target:A), B2: X)) → After.
    ///
    /// Review inside parallel inside condition. Reject at review only rewinds
    /// A within the parallel branch. Condition and After are untouched.
    /// </summary>
    [Fact]
    public void ReviewStep_ConditionThenParallel_ReviewInBranch_RejectIsLocal()
    {
        var trigger = Id();
        var condition = Id();
        var parallel = Id();
        var a = Id();
        var review = Id();
        var x = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, condition),
                Condition("Route", condition, [Rule("'go' == 'go'", parallel)], nextStepId: after),
                Parallel("Fork", parallel, [a, x]),
                Action("A", a, nextStepId: review),
                Review("Review", review, a),
                Action("X", x),
                Action("After", after)),
            trigger);

        execution.Start();

        // A → Review → Reject
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "v1")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "rework");

        // X still running (other parallel branch)
        var xExec = execution.StepExecutions.Single(s => s.StepId == x);
        Assert.Equal(ExecutionStatus.Running, xExec.Status);

        // A re-executes
        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, aExec2.Id);

        // Complete everything
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "v2")));
        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);
        execution.RecordStepCompleted(xExec.Id, Output(("x", "done")));

        // Parallel merges → condition completes → After
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 22. Review Rejection Target is First Step After Trigger
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → B → C → D → Review(target:A, max:2) → After.
    ///
    /// Rejection targets the very first action after trigger, invalidating
    /// the largest possible range (A, B, C, D, Review).
    /// </summary>
    [Fact]
    public void ReviewStep_TargetsFirstAction_InvalidatesMaxRange()
    {
        var trigger = Id();
        var a = Id();
        var b = Id();
        var c = Id();
        var d = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: b),
                Action("B", b, nextStepId: c),
                Action("C", c, nextStepId: d),
                Action("D", d, nextStepId: review),
                Review("Review", review, a, nextStepId: after),
                Action("After", after)),
            trigger);

        execution.Start();

        // Complete the chain
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "1")));
        var dExec = execution.GetRunningSteps().Single(s => s.StepId == d);
        execution.RecordStepCompleted(dExec.Id, Output(("d", "1")));

        // Reject back to A
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "start over");

        // All five step executions invalidated
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == aExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == bExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == cExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == dExec.Id);

        // Re-executes from A
        var newA = execution.GetRunningSteps().Single(s => s.StepId == a);
        Assert.NotEqual(aExec.Id, newA.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 23. Review Targets Immediately Preceding Step (Minimal Range)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → B → Review(target:B) → After.
    /// Rejection invalidates only B and Review. A is untouched.
    /// </summary>
    [Fact]
    public void ReviewStep_TargetsImmediatelyPrecedingStep_MinimalInvalidation()
    {
        var trigger = Id();
        var a = Id();
        var b = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: b),
                Action("B", b, nextStepId: review),
                Review("Review", review, b, nextStepId: after),
                Action("After", after)),
            trigger);

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "1")));
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "redo B only");

        // A is still in step executions (not invalidated)
        Assert.Contains(execution.StepExecutions, s => s.Id == aExec.Id);
        Assert.Equal(ExecutionStatus.Completed, aExec.Status);

        // B and Review are invalidated
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == bExec.Id);
        Assert.DoesNotContain(execution.StepExecutions, s => s.Id == reviewExec.Id);

        // New B running
        var newB = execution.GetRunningSteps().Single(s => s.StepId == b);
        Assert.NotEqual(bExec.Id, newB.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 24. Loop Body with Review: Reject Then Approve, Child Completes
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loop body: A → B → Review(target:A, max:3) → C.
    /// Single iteration: reject once at review, re-execute A→B, approve, C runs.
    /// Child execution completes.
    /// </summary>
    [Fact]
    public void ReviewStep_InsideLoopBody_RejectThenApprove_ChildCompletes()
    {
        var loop = Id();
        var a = Id();
        var b = Id();
        var review = Id();
        var c = Id();

        var parentContext = new ParentExecutionContext(
            ExecutionId(),
            loop,
            new Dictionary<string, StepOutput>());

        var execution = BuildExecution(
            Snapshot(
                Action("A", a, nextStepId: b),
                Action("B", b, nextStepId: review),
                Review("Review", review, a, maxRejections: 3, nextStepId: c),
                Action("C", c)),
            a,
            parentContext: parentContext);

        execution.Start();

        // A → B → Review → Reject
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "v1")));
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "v1")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "rework");

        // Re-execute A → B → Review → Approve
        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "v2")));
        var bExec2 = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec2.Id, Output(("b", "v2")));
        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        // C runs
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "done")));

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 25. Supersede Mechanics: B Rejects to B's Target (Mid-Path)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → ReviewA(target:A, max:3) → B → C → ReviewC(target:B, max:3) → D.
    ///
    /// ReviewA rejects twice, approves. ReviewC rejects to B (not A).
    /// Only ReviewA records in the B→C→ReviewC range get superseded —
    /// but ReviewA's records are for steps A→ReviewA, which is NOT in B→ReviewC range.
    /// So ReviewA's counter should NOT be reset.
    /// </summary>
    [Fact]
    public void ReviewStep_DownstreamRejectToMidPath_DoesNotSupersedePriorUpstream()
    {
        var trigger = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var c = Id();
        var reviewC = Id();
        var d = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: reviewA),
                Review("ReviewA", reviewA, a, maxRejections: 3, nextStepId: b),
                Action("B", b, nextStepId: c),
                Action("C", c, nextStepId: reviewC),
                Review("ReviewC", reviewC, b, maxRejections: 3, nextStepId: d),
                Action("D", d)),
            trigger);

        execution.Start();

        // ReviewA rejects twice
        for (var i = 0; i < 2; i++)
        {
            var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
            execution.RecordStepCompleted(aExec.Id, Output(("a", $"v{i}")));
            var rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
            RejectReviewStep(execution, rA.Id, $"reject-{i}");
        }

        // ReviewA approves
        var aPass = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aPass.Id, Output(("a", "pass")));
        var rAPass = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        ApproveReviewStep(execution, rAPass.Id);

        // B → C → ReviewC → reject to B
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "1")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "1")));
        var rC = execution.GetRunningSteps().Single(s => s.StepId == reviewC);
        RejectReviewStep(execution, rC.Id, "rework B-C");

        // B re-executes. A and ReviewA are NOT re-executed.
        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        var newB = execution.GetRunningSteps().Single(s => s.StepId == b);
        Assert.DoesNotContain(execution.GetRunningSteps(), s => s.StepId == a);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 26. Review Step Reached Event Contains Correct IDs
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReviewStep_ReachedEvent_HasCorrectExecutionAndStepIds()
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

        var actionExec = execution.GetRunningSteps().Single(s => s.StepId == action);
        execution.RecordStepCompleted(actionExec.Id, Output(("draftId", "123")));

        var reachedEvent = execution.DomainEvents
            .OfType<ReviewStepReachedEvent>()
            .Single();

        Assert.Equal(execution.Id, reachedEvent.WorkflowExecutionId);
        Assert.Equal(review, reachedEvent.StepId);

        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        Assert.Equal(reviewExec.Id, reachedEvent.StepExecutionId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 27. Parallel: One Branch Approved While Other Still Running
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parallel(Branch1: A → Review(target:A), Branch2: B → C).
    /// Review in Branch1 approves while Branch2 is still running.
    /// Parallel must wait for Branch2 to complete.
    /// </summary>
    [Fact]
    public void ReviewStep_ParallelBranch_ApproveWhileOtherBranchRunning_Waits()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var review = Id();
        var b = Id();
        var c = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [a, b], nextStepId: after),
                Action("A", a, nextStepId: review),
                Review("Review", review, a),
                Action("B", b, nextStepId: c),
                Action("C", c),
                Action("After", after)),
            trigger);

        execution.Start();

        // Branch1: complete A, approve review
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "done")));
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec.Id);

        // Parallel not merged — Branch2 still in progress
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == after);

        // Branch2: B → C
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "done")));
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "done")));

        // Now merge
        Assert.Contains(execution.DomainEvents, e => e is ParallelBranchesMergedEvent);
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 28. Review + Condition Fallback Path
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Condition(rule: never matches, fallback → B → Review(target:B)) → After.
    /// Fallback branch is taken, review inside fallback rejects, re-executes B.
    /// </summary>
    [Fact]
    public void ReviewStep_InsideConditionFallbackBranch_RejectRewindsInFallback()
    {
        var trigger = Id();
        var a = Id();
        var condition = Id();
        var neverMatch = Id();
        var b = Id();
        var review = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: condition),
                Condition("Route", condition,
                    [Rule("'go' == 'stop'", neverMatch)],
                    nextStepId: after,
                    fallbackStepId: b),
                Action("NeverMatch", neverMatch),
                Action("B", b, nextStepId: review),
                Review("Review", review, b),
                Action("After", after)),
            trigger);

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "done")));

        // Fallback branch taken
        Assert.DoesNotContain(execution.StepExecutions, s => s.StepId == neverMatch);
        var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bExec.Id, Output(("b", "v1")));

        // Review rejects inside fallback
        var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
        RejectReviewStep(execution, reviewExec.Id, "rework fallback");

        var newB = execution.GetRunningSteps().Single(s => s.StepId == b);
        Assert.NotEqual(bExec.Id, newB.Id);

        // Approve this time
        execution.RecordStepCompleted(newB.Id, Output(("b", "v2")));
        var reviewExec2 = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewExec2.Id);

        // Condition completes → After
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 29. Review at Workflow End — No NextStepId
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → B → Review(target:B, no next).
    /// Reject twice (max:3), approve on third attempt → workflow completes.
    /// </summary>
    [Fact]
    public void ReviewStep_TerminalReview_RejectTwiceThenApprove_Completes()
    {
        var trigger = Id();
        var a = Id();
        var b = Id();
        var review = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, a),
                Action("A", a, nextStepId: b),
                Action("B", b, nextStepId: review),
                Review("Review", review, b, maxRejections: 3)),
            trigger);

        execution.Start();

        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "done")));

        for (var i = 0; i < 2; i++)
        {
            var bExec = execution.GetRunningSteps().Single(s => s.StepId == b);
            execution.RecordStepCompleted(bExec.Id, Output(("b", $"v{i}")));
            var reviewExec = execution.GetRunningSteps().Single(s => s.StepId == review);
            RejectReviewStep(execution, reviewExec.Id, $"reject-{i}");
            Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        }

        // Third attempt: approve
        var bFinal = execution.GetRunningSteps().Single(s => s.StepId == b);
        execution.RecordStepCompleted(bFinal.Id, Output(("b", "v-final")));
        var reviewFinal = execution.GetRunningSteps().Single(s => s.StepId == review);
        ApproveReviewStep(execution, reviewFinal.Id);

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        Assert.Contains(execution.DomainEvents, e => e is WorkflowCompletedEvent);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 30. Massive: Parallel(3 branches) × Review × Condition × Loop — Full E2E
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → Parallel(
    ///   B1: A → Review(target:A, max:2),
    ///   B2: Condition(rule → C → D),
    ///   B3: E
    /// ) → Loop(body: F) → Review(target:Loop, max:2) → After
    ///
    /// Tests: parallel with review in one branch + condition in another,
    /// followed by a loop and another review targeting the loop.
    /// Branch1 rejects once and then approves.
    /// After parallel merge, loop executes.
    /// Post-loop review rejects once, loop re-runs, then approves.
    /// </summary>
    [Fact]
    public void ReviewStep_Massive_ParallelConditionLoopReview_EndToEnd()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var reviewA = Id();
        var condition = Id();
        var c = Id();
        var d = Id();
        var e = Id();
        var loop = Id();
        var f = Id();
        var reviewLoop = Id();
        var after = Id();

        var execution = BuildExecution(
            Snapshot(
                Trigger("T", trigger, parallel),
                Parallel("Fork", parallel, [a, condition, e], nextStepId: loop),
                Action("A", a, nextStepId: reviewA),
                Review("ReviewA", reviewA, a, maxRejections: 2),
                Condition("Route", condition, [Rule("'go' == 'go'", c)]),
                Action("C", c, nextStepId: d),
                Action("D", d),
                Action("E", e),
                Loop("MyLoop", loop, "{{trigger.items}}", f, nextStepId: reviewLoop),
                Action("F", f),
                Review("ReviewLoop", reviewLoop, loop, maxRejections: 2, nextStepId: after),
                Action("After", after)),
            trigger,
            Output(("seed", "alpha"), ("items", new[] { 1, 2 })));

        execution.Start();

        // === Branch 1: A → ReviewA (reject once, then approve) ===
        var aExec = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec.Id, Output(("a", "v1")));
        var rA = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        RejectReviewStep(execution, rA.Id, "rework A");

        var aExec2 = execution.GetRunningSteps().Single(s => s.StepId == a);
        execution.RecordStepCompleted(aExec2.Id, Output(("a", "v2")));
        var rA2 = execution.GetRunningSteps().Single(s => s.StepId == reviewA);
        ApproveReviewStep(execution, rA2.Id);

        // === Branch 2: Condition → C → D ===
        var cExec = execution.GetRunningSteps().Single(s => s.StepId == c);
        execution.RecordStepCompleted(cExec.Id, Output(("c", "done")));
        var dExec = execution.GetRunningSteps().Single(s => s.StepId == d);
        execution.RecordStepCompleted(dExec.Id, Output(("d", "done")));

        // === Branch 3: E ===
        var eExec = execution.GetRunningSteps().Single(s => s.StepId == e);
        execution.RecordStepCompleted(eExec.Id, Output(("e", "done")));

        // Parallel merges
        Assert.Contains(execution.DomainEvents, ev => ev is ParallelBranchesMergedEvent);

        // === Loop starts ===
        var loopExec = execution.GetRunningSteps().Single(s => s.StepId == loop);
        Assert.Contains(execution.DomainEvents, ev => ev is LoopExecutionStartedEvent);

        // Complete loop (external handler)
        execution.RecordStepCompleted(loopExec.Id, Output(("loopResult", "batch1")));

        // === Review after loop: reject once ===
        var rLoop = execution.GetRunningSteps().Single(s => s.StepId == reviewLoop);
        RejectReviewStep(execution, rLoop.Id, "redo loop");

        // Loop re-executes
        var loopExec2 = execution.GetRunningSteps().Single(s => s.StepId == loop);
        Assert.NotEqual(loopExec.Id, loopExec2.Id);
        execution.RecordStepCompleted(loopExec2.Id, Output(("loopResult", "batch2")));

        // === Approve post-loop review ===
        var rLoop2 = execution.GetRunningSteps().Single(s => s.StepId == reviewLoop);
        ApproveReviewStep(execution, rLoop2.Id);

        // After step
        var afterExec = execution.GetRunningSteps().Single(s => s.StepId == after);
        Assert.Equal(after, afterExec.StepId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

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
