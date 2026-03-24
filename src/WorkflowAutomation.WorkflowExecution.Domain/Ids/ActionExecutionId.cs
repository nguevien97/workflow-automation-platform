using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.Ids;

public readonly record struct ActionExecutionId(Guid Value) : IStronglyTypedId
{
    public static ActionExecutionId New() => new(Guid.NewGuid());
}
