using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.Ids;

public readonly record struct LoopExecutionId(Guid Value) : IStronglyTypedId
{
    public static LoopExecutionId New() => new(Guid.NewGuid());
}
