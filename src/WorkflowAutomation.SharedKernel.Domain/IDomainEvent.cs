namespace WorkflowAutomation.SharedKernel.Domain;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
