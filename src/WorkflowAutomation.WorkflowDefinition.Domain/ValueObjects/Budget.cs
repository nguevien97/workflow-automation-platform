using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

public sealed class Budget : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Budget(decimal amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
