using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;

namespace WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

public abstract class StepDefinition : Entity<StepId>
{
    public StepType StepType { get; }
    public string Name { get; private set; }

    protected StepDefinition(StepId id, StepType stepType, string name)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        StepType = stepType;
        Name = name;
    }
}
