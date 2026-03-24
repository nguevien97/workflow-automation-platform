using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;

public sealed class WorkSpace : AggregateRoot<WorkSpaceId>
{
    private readonly List<WorkflowId> _workflows = [];
    private readonly List<IntegrationId> _integrations = [];

    public string Name { get; private set; }
    public Budget Budget { get; private set; }
    public IReadOnlyList<WorkflowId> Workflows => _workflows.AsReadOnly();
    public IReadOnlyList<IntegrationId> Integrations => _integrations.AsReadOnly();

    public WorkSpace(WorkSpaceId id, string name, Budget budget)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(budget);

        Name = name;
        Budget = budget;
    }

    public void AddWorkflow(WorkflowId workflowId)
    {
        if (_workflows.Contains(workflowId))
            throw new InvalidOperationException($"Workflow '{workflowId}' already exists in this workspace.");

        _workflows.Add(workflowId);
    }

    public void AddIntegration(IntegrationId integrationId)
    {
        if (_integrations.Contains(integrationId))
            throw new InvalidOperationException($"Integration '{integrationId}' already exists in this workspace.");

        _integrations.Add(integrationId);
    }
}
