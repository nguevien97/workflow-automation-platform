using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

public sealed class StepInput : ValueObject
{
    private readonly Dictionary<string, object> _data;

    public IReadOnlyDictionary<string, object> Data => _data.AsReadOnly();

    public StepInput(Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = new Dictionary<string, object>(data);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var kvp in _data.OrderBy(x => x.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
}
