namespace WorkflowAutomation.WorkflowExecution.Domain.Enums;

public enum LoopIterationStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Cancelled
}
