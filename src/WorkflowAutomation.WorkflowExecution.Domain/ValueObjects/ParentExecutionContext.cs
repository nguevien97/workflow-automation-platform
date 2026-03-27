using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;

namespace WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

/// <summary>
/// Carries the parent execution's upstream step outputs into a child
/// (loop iteration) execution so body steps can reference parent data
/// via templates like <c>{{parentStepName.field}}</c>.
/// </summary>
public sealed class ParentExecutionContext : ValueObject
{
    public WorkflowExecutionId ParentExecutionId { get; }
    public StepId LoopStepId { get; }

    /// <summary>
    /// Step outputs from the parent workflow, keyed by step name.
    /// Includes all steps upstream of the loop step that produced output.
    /// </summary>
    private readonly Dictionary<string, StepOutput> _upstreamStepOutputs;
    public IReadOnlyDictionary<string, StepOutput> UpstreamStepOutputs => _upstreamStepOutputs.AsReadOnly();

    public ParentExecutionContext(
        WorkflowExecutionId parentExecutionId,
        StepId loopStepId,
        Dictionary<string, StepOutput> upstreamStepOutputs)
    {
        ArgumentNullException.ThrowIfNull(upstreamStepOutputs);

        ParentExecutionId = parentExecutionId;
        LoopStepId = loopStepId;
        _upstreamStepOutputs = new Dictionary<string, StepOutput>(upstreamStepOutputs);
    }

    /// <summary>
    /// Tries to resolve a step output by name from the parent context.
    /// </summary>
    public StepOutput? GetStepOutput(string stepName)
    {
        return _upstreamStepOutputs.TryGetValue(stepName, out var output) ? output : null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ParentExecutionId;
        yield return LoopStepId;
        foreach (var kvp in _upstreamStepOutputs.OrderBy(x => x.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
}