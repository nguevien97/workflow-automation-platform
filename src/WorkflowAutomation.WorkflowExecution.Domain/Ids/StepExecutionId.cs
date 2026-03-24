using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.Ids;

public readonly record struct StepExecutionId(Guid Value) : IStronglyTypedId
{
    public static StepExecutionId New() => new(Guid.NewGuid());
}
