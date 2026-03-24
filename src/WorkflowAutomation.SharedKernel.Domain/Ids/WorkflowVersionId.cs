using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.SharedKernel.Domain.Ids;

public readonly record struct WorkflowVersionId(Guid Value) : IStronglyTypedId
{
    public static WorkflowVersionId New() => new(Guid.NewGuid());
}
