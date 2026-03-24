using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;

public sealed class StepOutputSchema : ValueObject
{
    private readonly Dictionary<string, string> _fields;

    public IReadOnlyDictionary<string, string> Fields => _fields.AsReadOnly();

    public StepOutputSchema(Dictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = new Dictionary<string, string>(fields);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var kvp in _fields.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
}
