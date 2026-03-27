namespace WorkflowAutomation.WorkflowExecution.Domain.Services;

/// <summary>
/// Domain service for resolving template references like
/// <c>{{stepName.fieldName}}</c> against completed step outputs.
/// </summary>
public interface ITemplateResolver
{
    /// <summary>
    /// Resolves all <c>{{stepName.field}}</c> references in the given
    /// text, replacing them with concrete values from the provided outputs.
    /// </summary>
    /// <param name="template">
    /// Text possibly containing template references,
    /// e.g. <c>"New email from {{trigger.sender}}"</c>
    /// </param>
    /// <param name="stepOutputsByName">
    /// Completed step outputs keyed by step name.
    /// </param>
    /// <returns>The resolved string with all references replaced.</returns>
    string Resolve(string template, IReadOnlyDictionary<string, object> stepOutputsByName);
}