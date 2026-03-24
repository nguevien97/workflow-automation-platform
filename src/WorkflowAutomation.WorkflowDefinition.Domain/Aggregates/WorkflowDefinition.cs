using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.WorkflowDefinition.Domain.Events;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;

public sealed class WorkflowDefinition : AggregateRoot<WorkflowVersionId>
{
    private readonly List<StepDefinition> _steps = [];

    public WorkflowId WorkflowId { get; }
    public IReadOnlyList<StepDefinition> Steps => _steps.AsReadOnly();

    public WorkflowDefinition(WorkflowVersionId id, WorkflowId workflowId)
        : base(id)
    {
        WorkflowId = workflowId;
        AddDomainEvent(new WorkflowDefinitionCreatedEvent(id, workflowId));
    }

    public void AddStep(StepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (_steps.Any(s => s.Id == step.Id))
            throw new InvalidOperationException($"Step '{step.Id}' already exists in this workflow definition.");

        _steps.Add(step);
    }

    public void RemoveStep(StepId stepId)
    {
        var step = _steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step '{stepId}' not found in this workflow definition.");

        _steps.Remove(step);
    }

    public StepDefinition? GetNextStep(StepId? currentStepId)
    {
        if (currentStepId is null)
            return _steps.Count > 0 ? _steps[0] : null;

        var index = _steps.FindIndex(s => s.Id == currentStepId.Value);
        if (index < 0)
            throw new InvalidOperationException($"Step '{currentStepId}' not found in this workflow definition.");

        var nextIndex = index + 1;
        return nextIndex < _steps.Count ? _steps[nextIndex] : null;
    }

    public StepDefinition GetStep(StepId stepId)
    {
        return _steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step '{stepId}' not found in this workflow definition.");
    }

    public void Validate()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Workflow definition must contain at least one step.");

        var hasTrigger = _steps.Any(s => s.StepType == StepType.Trigger);
        if (!hasTrigger)
            throw new InvalidOperationException("Workflow definition must contain at least one trigger step.");

        if (_steps[0].StepType != StepType.Trigger)
            throw new InvalidOperationException("The first step in a workflow definition must be a trigger step.");
    }
}
