using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Events;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;

public sealed class Workflow : AggregateRoot<WorkflowId>
{
    public WorkSpaceId WorkSpaceId { get; }
    public string Name { get; private set; }
    public TimeSpan Timeout { get; private set; }

    public Workflow(WorkflowId id, WorkSpaceId workSpaceId, string name, TimeSpan timeout)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        WorkSpaceId = workSpaceId;
        Name = name;
        Timeout = timeout;
    }

    public WorkflowDefinition CreateVersion()
    {
        var versionId = WorkflowVersionId.New();
        AddDomainEvent(new WorkflowVersionCreatedEvent(Id, versionId));
        return new WorkflowDefinition(versionId, Id);
    }
}
