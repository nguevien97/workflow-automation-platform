using System.Collections;
using WorkflowAutomation.SharedKernel.Domain;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Normalized collection of items that a loop step will iterate over.
/// This prevents loop orchestration from receiving an arbitrary object.
/// </summary>
public sealed class LoopSourceItems : ValueObject
{
    private readonly List<object?> _items;

    public IReadOnlyList<object?> Items => _items.AsReadOnly();
    public int Count => _items.Count;

    private LoopSourceItems(List<object?> items)
    {
        _items = items;
    }

    public static LoopSourceItems FromResolvedValue(object? resolvedValue)
    {
        if (resolvedValue is null)
        {
            throw new InvalidOperationException(
                "Loop source resolved to null. Expected an enumerable collection.");
        }

        if (resolvedValue is string)
        {
            throw new InvalidOperationException(
                "Loop source resolved to a string. Expected an enumerable collection.");
        }

        if (resolvedValue is not IEnumerable enumerable)
        {
            throw new InvalidOperationException(
                $"Loop source resolved to '{resolvedValue.GetType().Name}'. Expected an enumerable collection.");
        }

        var items = new List<object?>();
        foreach (var item in enumerable)
            items.Add(item);

        return new LoopSourceItems(items);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var item in _items)
            yield return item;
    }
}