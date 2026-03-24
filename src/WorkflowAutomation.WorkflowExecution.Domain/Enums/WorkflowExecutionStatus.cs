namespace WorkflowAutomation.WorkflowExecution.Domain.Enums;

public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    /// <summary>
    /// Workflow is waiting for an external condition to be resolved,
    /// e.g. an integration token that needs re-authorisation.
    /// </summary>
    Suspended
}
