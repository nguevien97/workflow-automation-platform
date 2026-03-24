namespace WorkflowAutomation.SharedKernel.Domain;

public interface IStronglyTypedId
{
    Guid Value { get; }
}
