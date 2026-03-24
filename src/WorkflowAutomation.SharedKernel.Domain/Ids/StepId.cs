using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.SharedKernel.Domain.Ids;

public readonly record struct StepId(Guid Value) : IStronglyTypedId
{
    public static StepId New() => new(Guid.NewGuid());
}
