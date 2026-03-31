using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;

namespace WorkflowAutomation.WorkflowDefinition.Tests;

public class WorkflowDefinitionReviewStepTests
{
    private static StepId Id() => StepId.New();
    private static WorkflowVersionId VersionId() => WorkflowVersionId.New();
    private static WorkflowId WfId() => WorkflowId.New();
    private static IntegrationId IntId() => IntegrationId.New();

    private static StepOutputSchema Schema(params (string name, string type)[] fields) =>
        new(fields.ToDictionary(f => f.name, f => f.type));

    private static TriggerStepDefinition Trigger(
        string name,
        StepId id,
        StepId nextStepId,
        StepOutputSchema outputSchema) =>
        new(id, name, IntId(), "onEvent", new Dictionary<string, string>(), nextStepId, outputSchema);

    private static ActionStepDefinition Action(
        string name,
        StepId id,
        StepOutputSchema outputSchema,
        StepId? nextStepId = null,
        FailureStrategy failureStrategy = FailureStrategy.Stop,
        int retryCount = 0) =>
        new(id, name, IntId(), "execute", new Dictionary<string, TemplateOrLiteral>(), failureStrategy, retryCount, outputSchema, nextStepId);

    private static ParallelStepDefinition Parallel(
        string name,
        StepId id,
        IReadOnlyList<StepId> branchEntryStepIds,
        StepId? nextStepId = null) =>
        new(id, name, branchEntryStepIds, nextStepId);

    private static ConditionStepDefinition Condition(
        string name,
        StepId id,
        IReadOnlyList<ConditionRule> rules,
        StepId? nextStepId = null,
        StepId? fallbackStepId = null) =>
        new(id, name, rules, nextStepId, fallbackStepId);

    private static ConditionRule Rule(string expression, StepId targetStepId) =>
        new(expression, targetStepId);

    private static WorkflowAutomation.WorkflowDefinition.Domain.Aggregates.WorkflowDefinition Build(List<StepDefinition> steps) =>
        new(VersionId(), WfId(), steps);

    private static StepDefinition Review(
        string name,
        StepId id,
        StepId rejectionTargetStepId,
        int maxRejections = 3,
        StepId? nextStepId = null) =>
        new ReviewStepDefinition(id, name, rejectionTargetStepId, maxRejections, nextStepId);

    // ═══════════════════════════════════════════════════════════════════════════
    // Enum
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void StepType_Enum_ContainsReview()
    {
        Assert.Contains("Review", Enum.GetNames<StepType>());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Valid Scenarios
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// T → A → Review(target:A) → B. Basic valid case.
    /// </summary>
    [Fact]
    public void Valid_Review_TargetOnSameLocalPath()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();
        var after = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, action, Schema(("seed", "string"))),
            Action("Draft", action, Schema(("draftId", "string")), nextStepId: review),
            Review("Review", review, action, nextStepId: after),
            Action("AfterReview", after, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// T → A → Review(target:A). Review as terminal step (no NextStepId).
    /// </summary>
    [Fact]
    public void Valid_Review_TerminalStep()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, action, Schema(("seed", "string"))),
            Action("Draft", action, Schema(("draftId", "string")), nextStepId: review),
            Review("Review", review, action),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review inside a parallel branch targeting a step in the same branch.
    /// Parallel(Branch1: A → Review(target:A), Branch2: B).
    /// </summary>
    [Fact]
    public void Valid_Review_InsideParallelBranch()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var review = Id();
        var b = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, parallel, Schema(("seed", "string"))),
            Parallel("Fork", parallel, [a, b]),
            Action("A", a, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, a),
            Action("B", b, Schema(("out", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review inside a condition branch targeting a step in the same branch.
    /// Condition(rule → A → Review(target:A)) → After.
    /// </summary>
    [Fact]
    public void Valid_Review_InsideConditionBranch()
    {
        var trigger = Id();
        var condition = Id();
        var a = Id();
        var review = Id();
        var after = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, condition, Schema(("seed", "string"))),
            Condition("Route", condition, [Rule("'a' == 'a'", a)], nextStepId: after),
            Action("A", a, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, a),
            Action("After", after, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Separate review steps on different parallel branches.
    /// Parallel(Branch1: A → ReviewA(target:A), Branch2: B → ReviewB(target:B)).
    /// </summary>
    [Fact]
    public void Valid_Review_DifferentParallelBranches()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var reviewB = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, parallel, Schema(("seed", "string"))),
            Parallel("Fork", parallel, [a, b]),
            Action("A", a, Schema(("out", "string")), nextStepId: reviewA),
            Review("ReviewA", reviewA, a),
            Action("B", b, Schema(("out", "string")), nextStepId: reviewB),
            Review("ReviewB", reviewB, b),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// T → A → Review(target:A) → B → Review(target:B) → C.
    /// Multiple reviews with intervening action steps.
    /// </summary>
    [Fact]
    public void Valid_Review_MultipleReviewsWithIntervening()
    {
        var trigger = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var reviewB = Id();
        var c = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, a, Schema(("seed", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: reviewA),
            Review("ReviewA", reviewA, a, nextStepId: b),
            Action("B", b, Schema(("out", "string")), nextStepId: reviewB),
            Review("ReviewB", reviewB, b, nextStepId: c),
            Action("C", c, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review targeting multiple steps back on the same path.
    /// T → A → B → C → Review(target:A) → D.
    /// </summary>
    [Fact]
    public void Valid_Review_TargetMultipleStepsBack()
    {
        var trigger = Id();
        var a = Id();
        var b = Id();
        var c = Id();
        var review = Id();
        var d = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, a, Schema(("seed", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: b),
            Action("B", b, Schema(("out", "string")), nextStepId: c),
            Action("C", c, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, a, nextStepId: d),
            Action("D", d, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review with maxRejections=1 (boundary — minimum allowed).
    /// </summary>
    [Fact]
    public void Valid_Review_MaxRejectionsOne()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, action, Schema(("seed", "string"))),
            Action("Draft", action, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, action, maxRejections: 1),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review inside condition fallback branch.
    /// Condition(rule → A, fallback → B → Review(target:B)) → After.
    /// </summary>
    [Fact]
    public void Valid_Review_InsideConditionFallbackBranch()
    {
        var trigger = Id();
        var condition = Id();
        var a = Id();
        var b = Id();
        var review = Id();
        var after = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, condition, Schema(("seed", "string"))),
            Condition("Route", condition, [Rule("'a' == 'a'", a)], nextStepId: after, fallbackStepId: b),
            Action("A", a, Schema(("out", "string"))),
            Action("B", b, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, b),
            Action("After", after, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Downstream review (ReviewB) targets upstream step (A).
    /// T → A → ReviewA(target:A) → B → ReviewB(target:A) → C.
    /// This is valid — ReviewB rewinds all the way to A.
    /// </summary>
    [Fact]
    public void Valid_Review_DownstreamReviewTargetsUpstreamStep()
    {
        var trigger = Id();
        var a = Id();
        var reviewA = Id();
        var b = Id();
        var reviewB = Id();
        var c = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, a, Schema(("seed", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: reviewA),
            Review("ReviewA", reviewA, a, nextStepId: b),
            Action("B", b, Schema(("out", "string")), nextStepId: reviewB),
            Review("ReviewB", reviewB, a, nextStepId: c),
            Action("C", c, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review after a parallel step, targeting the first action before parallel.
    /// T → A → Parallel(B, C) → Review(target:A) → D.
    /// </summary>
    [Fact]
    public void Valid_Review_AfterParallel_TargetsPreParallelStep()
    {
        var trigger = Id();
        var a = Id();
        var parallel = Id();
        var b = Id();
        var c = Id();
        var review = Id();
        var d = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, a, Schema(("seed", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: parallel),
            Parallel("Fork", parallel, [b, c], nextStepId: review),
            Action("B", b, Schema(("out", "string"))),
            Action("C", c, Schema(("out", "string"))),
            Review("Review", review, a, nextStepId: d),
            Action("D", d, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// Review after a condition step, targeting a step before the condition.
    /// T → A → Condition(rule → B) → Review(target:A) → D.
    /// </summary>
    [Fact]
    public void Valid_Review_AfterCondition_TargetsPreConditionStep()
    {
        var trigger = Id();
        var a = Id();
        var condition = Id();
        var b = Id();
        var review = Id();
        var d = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, a, Schema(("seed", "string"))),
            Action("A", a, Schema(("out", "string")), nextStepId: condition),
            Condition("Route", condition, [Rule("'a' == 'a'", b)], nextStepId: review),
            Action("B", b, Schema(("out", "string"))),
            Review("Review", review, a, nextStepId: d),
            Action("D", d, Schema(("done", "string"))),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Invalid Scenarios
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Review target has FailureStrategy.Skip — invalid.
    /// </summary>
    [Fact]
    public void Invalid_Review_TargetHasSkipFailureStrategy()
    {
        var trigger = Id();
        var optional = Id();
        var review = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, optional, Schema(("seed", "string"))),
            Action("OptionalWork", optional, Schema(("result", "string")), nextStepId: review, failureStrategy: FailureStrategy.Skip),
            Review("Review", review, optional),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("skip", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Review inside parallel branch targets parent scope step — invalid.
    /// </summary>
    [Fact]
    public void Invalid_Review_TargetsParentScopeStep()
    {
        var trigger = Id();
        var beforeParallel = Id();
        var parallel = Id();
        var review = Id();
        var sibling = Id();
        var after = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, beforeParallel, Schema(("seed", "string"))),
            Action("Prepare", beforeParallel, Schema(("prep", "string")), nextStepId: parallel),
            Parallel("Fork", parallel, [review, sibling], nextStepId: after),
            Review("BranchReview", review, beforeParallel),
            Action("Sibling", sibling, Schema(("ok", "string"))),
            Action("After", after, Schema(("done", "string"))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("local path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Two review steps immediately adjacent (Review → Review with no step between).
    /// </summary>
    [Fact]
    public void Invalid_Review_TwoConsecutiveReviews()
    {
        var trigger = Id();
        var action = Id();
        var reviewA = Id();
        var reviewB = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, action, Schema(("seed", "string"))),
            Action("Draft", action, Schema(("draftId", "string")), nextStepId: reviewA),
            Review("ReviewA", reviewA, action, nextStepId: reviewB),
            Review("ReviewB", reviewB, action),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("review", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Review targets a step that is NOT on its local path (different condition branch).
    /// Condition(rule1 → A, rule2 → B → Review(target:A)) → After.
    /// B's review targets A which is on a different branch — invalid.
    /// </summary>
    [Fact]
    public void Invalid_Review_TargetOnDifferentConditionBranch()
    {
        var trigger = Id();
        var condition = Id();
        var a = Id();
        var b = Id();
        var review = Id();
        var after = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, condition, Schema(("seed", "string"))),
            Condition("Route", condition, [Rule("'a' == 'a'", a), Rule("'b' == 'b'", b)], nextStepId: after),
            Action("A", a, Schema(("out", "string"))),
            Action("B", b, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, a),
            Action("After", after, Schema(("done", "string"))),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("local path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Review inside one parallel branch targeting a step in a sibling branch.
    /// Parallel(Branch1: A, Branch2: B → Review(target:A)) — invalid.
    /// </summary>
    [Fact]
    public void Invalid_Review_TargetInSiblingParallelBranch()
    {
        var trigger = Id();
        var parallel = Id();
        var a = Id();
        var b = Id();
        var review = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, parallel, Schema(("seed", "string"))),
            Parallel("Fork", parallel, [a, b]),
            Action("A", a, Schema(("out", "string"))),
            Action("B", b, Schema(("out", "string")), nextStepId: review),
            Review("Review", review, a),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Build(steps));
        Assert.Contains("local path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Review target has FailureStrategy.Retry — this is valid (Retry is not Skip).
    /// </summary>
    [Fact]
    public void Valid_Review_TargetHasRetryFailureStrategy()
    {
        var trigger = Id();
        var action = Id();
        var review = Id();

        var steps = new List<StepDefinition>
        {
            Trigger("T", trigger, action, Schema(("seed", "string"))),
            Action("Work", action, Schema(("result", "string")), nextStepId: review, failureStrategy: FailureStrategy.Retry, retryCount: 3),
            Review("Review", review, action),
        };

        var ex = Record.Exception(() => Build(steps));
        Assert.Null(ex);
    }

    /// <summary>
    /// ReviewStepDefinition constructor rejects maxRejections=0.
    /// </summary>
    [Fact]
    public void Invalid_ReviewStepDefinition_MaxRejectionsZero_Throws()
    {
        var id = Id();
        var target = Id();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReviewStepDefinition(id, "R", target, maxRejections: 0));
    }

    /// <summary>
    /// ReviewStepDefinition constructor rejects default (empty) rejectionTargetStepId.
    /// </summary>
    [Fact]
    public void Invalid_ReviewStepDefinition_EmptyTargetId_Throws()
    {
        var id = Id();

        Assert.Throws<ArgumentException>(
            () => new ReviewStepDefinition(id, "R", default));
    }
}
