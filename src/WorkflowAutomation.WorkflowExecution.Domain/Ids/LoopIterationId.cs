using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.Ids;

public readonly record struct LoopIterationId(Guid Value) : IStronglyTypedId
{
    public static LoopIterationId New() => new(Guid.NewGuid());
}
