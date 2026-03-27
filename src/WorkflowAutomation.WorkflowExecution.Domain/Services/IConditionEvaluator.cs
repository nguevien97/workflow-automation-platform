namespace WorkflowAutomation.WorkflowExecution.Domain.Services;

/// <summary>
/// Domain service for evaluating condition rule expressions.
/// The aggregate owns the routing logic (first-match-wins, fallback);
/// this service handles the expression parsing and evaluation.
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// Evaluates a boolean expression with template references already
    /// resolved to concrete values.
    /// </summary>
    /// <param name="expression">
    /// e.g. <c>"urgent" == "urgent"</c> (after template resolution)
    /// </param>
    /// <returns>True if the expression evaluates to true.</returns>
    bool Evaluate(string expression);
}