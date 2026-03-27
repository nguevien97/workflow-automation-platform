using System.Text.RegularExpressions;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Services;

/// <summary>
/// Resolves <c>{{stepName.fieldName}}</c> template references against
/// completed step outputs. Unresolved references are replaced with
/// empty string.
/// </summary>
public sealed partial class TemplateResolver : ITemplateResolver
{
    // Matches {{stepName.fieldName}}
    [GeneratedRegex(@"\{\{([^.}]+)\.([^}]+)\}\}")]
    private static partial Regex TemplatePattern();

    public string Resolve(string template, IReadOnlyDictionary<string, object> stepOutputsByName)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(stepOutputsByName);

        return TemplatePattern().Replace(template, match =>
        {
            var stepName = match.Groups[1].Value.Trim();
            var fieldName = match.Groups[2].Value.Trim();

            if (!stepOutputsByName.TryGetValue(stepName, out var stepOutput))
                return string.Empty;

            return ResolveField(stepOutput, fieldName);
        });
    }

    private static string ResolveField(object stepOutput, string fieldName)
    {
        // StepOutput value object — look in its Data dictionary.
        if (stepOutput is StepOutput output)
        {
            return output.Data.TryGetValue(fieldName, out var value)
                ? ConvertToString(value)
                : string.Empty;
        }

        // Raw dictionary (e.g. from parent context passthrough).
        if (stepOutput is IReadOnlyDictionary<string, object> dict)
        {
            return dict.TryGetValue(fieldName, out var value)
                ? ConvertToString(value)
                : string.Empty;
        }

        return string.Empty;
    }

    private static string ConvertToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            _ => value.ToString() ?? string.Empty
        };
    }
}