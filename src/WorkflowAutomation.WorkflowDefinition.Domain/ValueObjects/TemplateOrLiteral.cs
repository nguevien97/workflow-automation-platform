using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Templates;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

public sealed class TemplateOrLiteral : ValueObject
{
    public bool IsTemplate { get; }
    public string Value { get; }

    private TemplateOrLiteral(bool isTemplate, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        IsTemplate = isTemplate;
        Value = value;
    }

    public static TemplateOrLiteral Template(string value)
    {
        if (!TemplateResolver.ContainsReference(value))
        {
            throw new ArgumentException(
                "Template values must contain at least one workflow reference like '{{StepName.fieldName}}'.",
                nameof(value));
        }

        return new(isTemplate: true, value);
    }

    public static TemplateOrLiteral Literal(string value) => new(isTemplate: false, value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsTemplate;
        yield return Value;
    }
}
