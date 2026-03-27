using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Entities;

public sealed class StepExecution : Entity<StepExecutionId>
{
    public StepId StepId { get; private init; }
    public ExecutionStatus Status { get; private set; }
    public StepInput? Input { get; private set; }
    public StepOutput? Output { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Error { get; private set; }

    public StepExecution(StepExecutionId id, StepId stepId) : base(id)
    {
        StepId = stepId;
        Status = ExecutionStatus.Pending;
    }

    public void Start(StepInput? input)
    {
        GuardStatus(ExecutionStatus.Pending, nameof(Start));
        Status = ExecutionStatus.Running;

        Input = input;
        StartedAt = DateTime.UtcNow;
    }

    public void CompleteWithOutput(StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(ExecutionStatus.Running, nameof(CompleteWithOutput));

        Status = ExecutionStatus.Completed;
        Output = output;
        CompletedAt = DateTime.UtcNow;
    }

    public void CompleteWithoutOutput()
    {
        GuardStatus(ExecutionStatus.Running, nameof(CompleteWithoutOutput));

        Status = ExecutionStatus.Completed;
        Output = null;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardStatus(ExecutionStatus.Running, nameof(Fail));

        Status = ExecutionStatus.Failed;
        Error = error;
        CompletedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        GuardStatus(ExecutionStatus.Running, nameof(Skip));

        Status = ExecutionStatus.Skipped;
        CompletedAt = DateTime.UtcNow;
    }

    private void GuardStatus(ExecutionStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} a step execution in '{Status}' status. Expected '{expected}'.");
    }
}
