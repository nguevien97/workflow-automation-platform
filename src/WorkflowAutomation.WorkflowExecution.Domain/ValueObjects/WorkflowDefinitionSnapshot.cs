using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class WorkflowDefinitionSnapshot : ValueObject
{
    private readonly List<StepId> _orderedStepIds;

    public WorkflowVersionId WorkflowVersionId { get; }
    public IReadOnlyList<StepId> OrderedStepIds => _orderedStepIds.AsReadOnly();

    public WorkflowDefinitionSnapshot(
        WorkflowVersionId workflowVersionId,
        IEnumerable<StepId> orderedStepIds)
    {
        ArgumentNullException.ThrowIfNull(orderedStepIds);

        WorkflowVersionId = workflowVersionId;
        _orderedStepIds = orderedStepIds.ToList();

        if (_orderedStepIds.Count == 0)
            throw new ArgumentException("Workflow definition must contain at least one step.", nameof(orderedStepIds));
    }

    public StepId GetFirstStepId() => _orderedStepIds[0];

    public StepId? GetNextStepId(StepId currentStepId)
    {
        var index = _orderedStepIds.IndexOf(currentStepId);
        if (index < 0)
            throw new InvalidOperationException($"Step '{currentStepId}' not found in workflow definition.");

        var nextIndex = index + 1;
        return nextIndex < _orderedStepIds.Count ? _orderedStepIds[nextIndex] : null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return WorkflowVersionId;
        foreach (var stepId in _orderedStepIds)
            yield return stepId;
    }
}
