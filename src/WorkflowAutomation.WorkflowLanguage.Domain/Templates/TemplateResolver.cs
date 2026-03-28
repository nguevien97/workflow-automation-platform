using System.Text.RegularExpressions;

namespace WorkflowAutomation.WorkflowLanguage.Domain.Templates;

/// <summary>
/// Shared workflow-language helper for parsing and resolving
/// <c>{{stepName.fieldName}}</c> references.
/// </summary>
public static partial class TemplateResolver
{
    [GeneratedRegex(@"\{\{([^.}]+)\.([^}]+)\}\}")]
    private static partial Regex TemplatePattern();

    [GeneratedRegex(@"^\s*\{\{([^.}]+)\.([^}]+)\}\}\s*$")]
    private static partial Regex WholeTemplatePattern();

    public static IReadOnlyList<TemplateReferenceMatch> FindReferences(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var matches = TemplatePattern().Matches(text);
        if (matches.Count == 0)
            return [];

        var references = new List<TemplateReferenceMatch>(matches.Count);
        foreach (Match match in matches)
            references.Add(CreateReference(match));

        return references;
    }

    public static bool TryParseWholeReference(
        string text,
        out TemplateReferenceMatch templateReference)
    {
        ArgumentNullException.ThrowIfNull(text);

        var match = WholeTemplatePattern().Match(text);
        if (!match.Success)
        {
            templateReference = default;
            return false;
        }

        templateReference = CreateReference(match);
        return true;
    }

    public static bool ContainsReference(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TemplatePattern().IsMatch(text);
    }

    public static string ReplaceReferences(
        string text,
        Func<TemplateReferenceMatch, string> replacement)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);

        return TemplatePattern().Replace(text, match => replacement(CreateReference(match)));
    }

    public static string ResolveText(
        string template,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> stepOutputsByName)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(stepOutputsByName);

        return ReplaceReferences(template, reference =>
            ConvertToString(ResolveReferenceValue(stepOutputsByName, reference)));
    }

    public static object ResolveValue(
        string templateOrLiteral,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> stepOutputsByName)
    {
        ArgumentNullException.ThrowIfNull(templateOrLiteral);
        ArgumentNullException.ThrowIfNull(stepOutputsByName);

        if (TryParseWholeReference(templateOrLiteral, out var templateReference))
        {
            return ResolveReferenceValue(stepOutputsByName, templateReference)
                ?? string.Empty;
        }

        return ResolveText(templateOrLiteral, stepOutputsByName);
    }

    private static TemplateReferenceMatch CreateReference(Match match) =>
        new(
            match.Groups[1].Value.Trim(),
            match.Groups[2].Value.Trim(),
            match.Value);

    private static object? ResolveReferenceValue(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> stepOutputsByName,
        TemplateReferenceMatch templateReference)
    {
        if (!stepOutputsByName.TryGetValue(templateReference.StepName, out var stepOutput))
            return null;

        return stepOutput.TryGetValue(templateReference.FieldName, out var value)
            ? value
            : null;
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