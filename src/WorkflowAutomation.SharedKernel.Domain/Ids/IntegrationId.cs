using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.SharedKernel.Domain.Ids;

public readonly record struct IntegrationId(Guid Value) : IStronglyTypedId
{
    public static IntegrationId New() => new(Guid.NewGuid());
}
