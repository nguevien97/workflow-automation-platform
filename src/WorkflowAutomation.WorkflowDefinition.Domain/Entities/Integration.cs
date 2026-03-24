using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowDefinition.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Entities;

public sealed class Integration : Entity<IntegrationId>
{
    public string Name { get; private set; }
    public IntegrationType IntegrationType { get; }
    public WorkSpaceId WorkSpaceId { get; }

    public Integration(IntegrationId id, string name, IntegrationType integrationType, WorkSpaceId workSpaceId)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        IntegrationType = integrationType;
        WorkSpaceId = workSpaceId;
    }
}
