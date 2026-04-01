namespace WorkflowAutomation.WorkflowExecution.Domain.Enums;

public enum ActionExecutionStatus
{
    Pending,
    Running,
    WaitingForRetry,
    Completed,
    Skipped,
    Failed,
    Cancelled
}
