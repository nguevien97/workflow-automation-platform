using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.Entities;
using WorkflowAutomation.WorkflowExecution.Domain.Enums;
using WorkflowAutomation.WorkflowExecution.Domain.Events;
using WorkflowAutomation.WorkflowExecution.Domain.Ids;
using WorkflowAutomation.WorkflowExecution.Domain.ValueObjects;

namespace WorkflowAutomation.WorkflowExecution.Domain.Aggregates;

public sealed class WorkflowExecution : AggregateRoot<WorkflowExecutionId>
{
    private readonly List<StepExecution> _stepExecutions = [];

    public WorkflowDefinitionSnapshot Definition { get; }
    public ExecutionStatus Status { get; private set; }
    public IReadOnlyList<StepExecution> StepExecutions => _stepExecutions.AsReadOnly();
    public DateTime CreatedAt { get; private init; }
    public DateTime? CompletedAt { get; private set; }

    public WorkflowExecution(
        WorkflowExecutionId id,
        WorkflowDefinitionSnapshot definition)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        Status = ExecutionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        GuardStatus(ExecutionStatus.Pending, nameof(Start));

        Status = ExecutionStatus.Running;

        var firstStepId = Definition.GetFirstStepId();
        var stepExecution = new StepExecution(StepExecutionId.New(), firstStepId);
        _stepExecutions.Add(stepExecution);

        AddDomainEvent(new WorkflowStartedEvent(Id));
    }

    public void RecordStepCompleted(StepExecutionId stepExecutionId, StepOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        GuardStatus(ExecutionStatus.Running, nameof(RecordStepCompleted));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Complete(output);

        AdvanceOrComplete(step.StepId);
    }

    public void RecordStepFailed(StepExecutionId stepExecutionId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        GuardStatus(ExecutionStatus.Running, nameof(RecordStepFailed));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Fail(error);

        Status = ExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new WorkflowFailedEvent(Id, error));
    }

    public void RecordStepSkipped(StepExecutionId stepExecutionId)
    {
        GuardStatus(ExecutionStatus.Running, nameof(RecordStepSkipped));

        var step = GetStepExecutionOrThrow(stepExecutionId);
        step.Skip();

        AdvanceOrComplete(step.StepId);
    }

    public void Cancel()
    {
        if (Status is ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot cancel a workflow execution in '{Status}' status.");

        Status = ExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public StepExecution? GetCurrentStep() =>
        _stepExecutions.FirstOrDefault(s => s.Status == ExecutionStatus.Running);

    private void AdvanceOrComplete(StepId completedStepId)
    {
        var nextStepId = Definition.GetNextStepId(completedStepId);

        if (nextStepId is null)
        {
            Status = ExecutionStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            AddDomainEvent(new WorkflowCompletedEvent(Id));
        }
        else
        {
            var nextStepExecution = new StepExecution(StepExecutionId.New(), nextStepId.Value);
            _stepExecutions.Add(nextStepExecution);
        }
    }

    private StepExecution GetStepExecutionOrThrow(StepExecutionId stepExecutionId) =>
        _stepExecutions.Find(s => s.Id == stepExecutionId)
        ?? throw new InvalidOperationException(
            $"Step execution '{stepExecutionId}' not found in this workflow execution.");

    private void GuardStatus(ExecutionStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Cannot {operation} a workflow execution in '{Status}' status. Expected '{expected}'.");
    }
}
