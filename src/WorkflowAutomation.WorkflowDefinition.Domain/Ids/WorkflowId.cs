using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Ids;

public readonly record struct WorkflowId(Guid Value) : IStronglyTypedId
{
    public static WorkflowId New() => new(Guid.NewGuid());
}
