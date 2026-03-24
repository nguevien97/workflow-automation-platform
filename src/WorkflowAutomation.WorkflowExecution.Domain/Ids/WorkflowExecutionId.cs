using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.Ids;

public readonly record struct WorkflowExecutionId(Guid Value) : IStronglyTypedId
{
    public static WorkflowExecutionId New() => new(Guid.NewGuid());
}
