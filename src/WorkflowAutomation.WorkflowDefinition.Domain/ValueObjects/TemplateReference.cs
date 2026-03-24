using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

public sealed class TemplateReference : ValueObject
{
    public string Expression { get; }

    public TemplateReference(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Expression = expression;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Expression;
    }
}
