using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Entities;

public sealed class Workflow : Entity<WorkflowId>
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

    public Aggregates.WorkflowDefinition CreateVersion()
    {
        var versionId = WorkflowVersionId.New();
        return new Aggregates.WorkflowDefinition(versionId, Id);
    }
}
