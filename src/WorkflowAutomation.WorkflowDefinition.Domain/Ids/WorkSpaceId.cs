using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Ids;

public readonly record struct WorkSpaceId(Guid Value) : IStronglyTypedId
{
    public static WorkSpaceId New() => new(Guid.NewGuid());
}
