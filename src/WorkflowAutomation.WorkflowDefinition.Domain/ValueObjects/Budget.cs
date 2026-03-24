using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

/// <summary>
/// Represents the monthly step-execution budget for a workspace.
/// A free workspace gets 1,000 step executions per month;
/// a paid workspace gets 50,000 or more.
/// </summary>
public sealed class Budget : ValueObject
{
    public int MonthlyStepExecutionLimit { get; }

    public Budget(int monthlyStepExecutionLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyStepExecutionLimit);
        MonthlyStepExecutionLimit = monthlyStepExecutionLimit;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MonthlyStepExecutionLimit;
    }
}
