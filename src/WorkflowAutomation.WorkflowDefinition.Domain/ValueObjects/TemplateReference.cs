using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Templates;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

public sealed class TemplateReference : ValueObject
{
    public string Expression { get; }

    public TemplateReference(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        if (!TemplateResolver.TryParseWholeReference(expression, out _))
        {
            throw new ArgumentException(
                "Template reference must be a single workflow reference like '{{StepName.fieldName}}'.",
                nameof(expression));
        }

        Expression = expression;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Expression;
    }
}
