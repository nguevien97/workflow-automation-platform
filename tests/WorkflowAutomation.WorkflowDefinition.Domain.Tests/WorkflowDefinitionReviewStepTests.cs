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

    private static WorkflowAutomation.WorkflowDefinition.Domain.Aggregates.WorkflowDefinition Build(List<StepDefinition> steps) =>
        new(VersionId(), WfId(), steps);

    private static StepDefinition Review(
        string name,
        StepId id,
        StepId rejectionTargetStepId,
        int maxRejections = 3,
        StepId? nextStepId = null) =>
        new ReviewStepDefinition(id, name, rejectionTargetStepId, maxRejections, nextStepId);

    [Fact]
    public void StepType_Enum_ContainsReview()
    {
        Assert.Contains("Review", Enum.GetNames<StepType>());
    }

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

    [Fact]
    public void Invalid_Review_TwoReviewsOnSameLocalPath()
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

}