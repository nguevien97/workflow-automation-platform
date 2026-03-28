namespace WorkflowAutomation.WorkflowLanguage.Domain.Templates;

public readonly record struct TemplateReferenceMatch(
    string StepName,
    string FieldName,
    string RawText);