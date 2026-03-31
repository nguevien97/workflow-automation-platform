using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Entities;

public sealed class LoopIteration : Entity<LoopIterationId>
{
    public int Index { get; }
    public object? IterationItem { get; }
    public LoopIterationStatus Status { get; private set; }
    public StepOutput? Output { get; private set; }
    public string? Error { get; private set; }

    public LoopIteration(LoopIterationId id, int index, object? iterationItem) : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        Index = index;
        IterationItem = iterationItem;
        Status = LoopIterationStatus.Pending;
    }

    public void MarkRunning()
    {
        GuardStatus(LoopIterationStatus.Pending, nameof(MarkRunning));
        Status = LoopIterationStatus.Running;
    }

    public void MarkCompleted(StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(LoopIterationStatus.Running, nameof(MarkCompleted));

        Status = LoopIterationStatus.Completed;
        Output = output;
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardStatus(LoopIterationStatus.Running, nameof(MarkFailed));

        Status = LoopIterationStatus.Failed;
        Error = error;
    }

    public void MarkSkipped()
    {
        GuardStatus(LoopIterationStatus.Failed, nameof(MarkSkipped));
        Status = LoopIterationStatus.Skipped;
    }

    public void Cancel()
    {
        if (Status is LoopIterationStatus.Completed
                   or LoopIterationStatus.Failed
                   or LoopIterationStatus.Skipped
                   or LoopIterationStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot cancel a loop iteration in '{Status}' status.");

        Status = LoopIterationStatus.Cancelled;
    }

    private void GuardStatus(LoopIterationStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} a loop iteration in '{Status}' status. Expected '{expected}'.");
    }
}
