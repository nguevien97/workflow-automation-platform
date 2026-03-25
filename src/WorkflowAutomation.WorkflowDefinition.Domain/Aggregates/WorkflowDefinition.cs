using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Events;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;

public sealed class WorkflowDefinition : AggregateRoot<WorkflowVersionId>
{
    private readonly List<StepDefinition> _steps = [];

    public WorkflowId WorkflowId { get; }
    public IReadOnlyList<StepDefinition> Steps => _steps.AsReadOnly();

    public WorkflowDefinition(WorkflowVersionId id, WorkflowId workflowId, List<StepDefinition> steps)
        : base(id)
    {
        WorkflowId = workflowId;
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        Validate();
        AddDomainEvent(new WorkflowDefinitionCreatedEvent(id, workflowId));
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
