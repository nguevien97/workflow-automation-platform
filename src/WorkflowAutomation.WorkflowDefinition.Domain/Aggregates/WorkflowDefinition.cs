using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;
using System.Text.RegularExpressions;
using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.SharedKernel.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Events;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;

namespace WorkflowAutomation.WorkflowDefinition.Domain.Aggregates;

public sealed class WorkflowDefinition : AggregateRoot<WorkflowVersionId>
{
    private readonly List<StepDefinition> _steps = [];

    public WorkflowId WorkflowId { get; }
    public IReadOnlyList<StepDefinition> Steps => _steps.AsReadOnly();

    public WorkflowDefinition(WorkflowVersionId id, WorkflowId workflowId, List<StepDefinition> steps)
        : base(id)
    {
        WorkflowId = workflowId;
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        Validate();
        AddDomainEvent(new WorkflowDefinitionCreatedEvent(id, workflowId));
    }

    private void Validate()
    {
        if (_steps.Count <= 1)
            throw new InvalidOperationException("Workflow definition must contain at least two steps.");

        // validate that there is exactly one trigger step and it's the first step
        var triggerSteps = _steps.Where(s => s.StepType == StepType.Trigger).ToList();
        if (triggerSteps.Count == 0)
            throw new InvalidOperationException("Workflow definition must contain at least one trigger step.");
        if (triggerSteps.Count > 1)
            throw new InvalidOperationException("Workflow definition must contain exactly one trigger step.");

        var triggerStep = (TriggerStepDefinition)_steps[0];
        if (triggerStep.StepType != StepType.Trigger)
            throw new InvalidOperationException("The first step in a workflow definition must be a trigger step.");

        if (triggerStep.NextStepId == null)
            throw new InvalidOperationException("The trigger step must have a NextStepId pointing to the next step.");

        if (triggerStep.OutputSchema == null)
            throw new InvalidOperationException("The trigger step must have a non-null OutputSchema.");

        var stepDict = _steps.ToDictionary(s => s.Id);
        StepDefinition GetStep(StepId stepId)
        {
            if (!stepDict.TryGetValue(stepId, out var step))
                throw new InvalidOperationException($"Step '{stepId}' not found in this workflow definition.");
            return step;
        }

        var duplicateNames = _steps.GroupBy(s => s.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateNames.Count > 0)
            throw new InvalidOperationException($"Duplicate step names: {string.Join(", ", duplicateNames)}");

        var visitStates = new Dictionary<StepId, VisitState>();
        _steps.ForEach(s => visitStates[s.Id] = VisitState.NotVisited);
        visitStates[triggerStep.Id] = VisitState.Completed; // Mark trigger step as completed to allow downstream steps to reference it
        void DFS(StepId stepId, HashSet<StepId> availableSteps)
        {
            var step = GetStep(stepId);
            if (visitStates[stepId] != VisitState.NotVisited)
            {
                throw new InvalidOperationException($"Workflow definition contains a cycle at step '{GetStep(stepId).Name}'.");
            }

            visitStates[stepId] = VisitState.InProgress;
            ValidateTemplatesInStep(step, availableSteps);
            var downStreamAvailableSteps = new HashSet<StepId>(availableSteps);

            switch (step)
            {
                case ActionStepDefinition actionStep:
                    downStreamAvailableSteps.Add(actionStep.Id); // Action step itself becomes available for downstream steps after it
                    if (actionStep.NextStepId is not null)
                    {
                        DFS(actionStep.NextStepId.Value, downStreamAvailableSteps);
                    }
                    break;
                case ConditionStepDefinition conditionStep:
                    foreach (var rule in conditionStep.Rules)
                    {
                        DFS(rule.TargetStepId, downStreamAvailableSteps);
                    }
                    if (conditionStep.FallbackStepId is not null)
                        DFS(conditionStep.FallbackStepId.Value, downStreamAvailableSteps);

                    if (conditionStep.NextStepId is not null)
                        DFS(conditionStep.NextStepId.Value, downStreamAvailableSteps);
                    break;
                case ParallelStepDefinition parallelStep:
                    foreach (var branchEntryId in parallelStep.BranchEntryStepIds)
                    {
                        DFS(branchEntryId, downStreamAvailableSteps);
                    }

                    // Collect all steps in parallel branches as available steps for downstream steps after the parallel
                    var parallelBranchSteps = new HashSet<StepId>();
                    parallelBranchSteps.UnionWith(downStreamAvailableSteps); // Add the parallel step itself as available for downstream steps
                    foreach (var branchEntryId in parallelStep.BranchEntryStepIds)
                    {
                        CollectParallelBranchSteps(branchEntryId, parallelBranchSteps);
                    }

                    if (parallelStep.NextStepId is not null)
                        DFS(parallelStep.NextStepId.Value, parallelBranchSteps);
                    break;
                case LoopStepDefinition loopStep:
                    DFS(loopStep.LoopEntryStepId, downStreamAvailableSteps);

                    var afterLoopStreamAvailableSteps = new HashSet<StepId>(downStreamAvailableSteps) { loopStep.Id }; // The loop step itself is also considered available for downstream steps after the loop
                    if (loopStep.NextStepId is not null)
                        DFS(loopStep.NextStepId.Value, afterLoopStreamAvailableSteps);

                    break;

            }

            visitStates[stepId] = VisitState.Completed;
        }

        void CollectParallelBranchSteps(StepId branchEntryId, HashSet<StepId> collection)
        {
            StepId? currentStepId = branchEntryId;
            while (currentStepId is not null)
            {
                var currentStep = GetStep(currentStepId.Value);
                switch (currentStep)
                {
                    case LoopStepDefinition loopStep:
                    case ActionStepDefinition actionStep:
                        collection.Add(currentStepId.Value);
                        break;
                    case ParallelStepDefinition parallelStep:
                        foreach (var childBranchEntryId in parallelStep.BranchEntryStepIds)
                        {
                            CollectParallelBranchSteps(childBranchEntryId, collection);
                        }
                        break;
                }

                currentStepId = currentStep.NextStepId;
            }

        }

        void ValidateTemplatesInString(string text, HashSet<StepId> availableSteps, StepId stepId)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var matches = Regex.Matches(text, @"\{\{([^.]+)\.([^}]+)\}\}");
            foreach (Match match in matches)
            {
                var refStepName = match.Groups[1].Value;
                var refFieldName = match.Groups[2].Value;

                var referencedStep = _steps.FirstOrDefault(s => s.Name == refStepName);
                if (referencedStep == null)
                    throw new InvalidOperationException($"Step '{stepId}' references unknown step '{refStepName}' in template '{match.Value}'.");

                if (!availableSteps.Contains(referencedStep.Id))
                    throw new InvalidOperationException($"Step '{stepId}' references step '{refStepName}' before it is guaranteed to complete in template '{match.Value}'.");

                // Get OutputSchema
                StepOutputSchema schema = null;
                if (referencedStep is TriggerStepDefinition triggerStep) schema = triggerStep.OutputSchema;
                else if (referencedStep is ActionStepDefinition actionStep) schema = actionStep.OutputSchema;
                else if (referencedStep is LoopStepDefinition loopStep) schema = loopStep.OutputSchema;

                if (schema != null && !schema.Fields.ContainsKey(refFieldName))
                    throw new InvalidOperationException($"Step '{stepId}' references non-existent field '{refFieldName}' on step '{refStepName}' in template '{match.Value}'.");
            }
        }
        
        void ValidateTemplatesInStep(StepDefinition step, HashSet<StepId> availableSteps)
        {
            if (step is ActionStepDefinition actionStep)
            {
                foreach (var mapping in actionStep.InputMappings.Values)
                {
                    if (mapping.IsTemplate)
                        ValidateTemplatesInString(mapping.Value, availableSteps, step.Id);
                }
            }
            else if (step is ConditionStepDefinition conditionStep)
            {
                foreach (var rule in conditionStep.Rules)
                {
                    ValidateTemplatesInString(rule.Expression, availableSteps, step.Id);
                }
            }
            else if (step is LoopStepDefinition loopStep)
            {
                ValidateTemplatesInString(loopStep.SourceArray.Expression, availableSteps, step.Id);
            }
        }
        
        DFS(triggerStep.NextStepId.Value, new HashSet<StepId> { triggerStep.Id });

        // After DFS, check if there are any steps that were not visited, which means they are unreachable
        var unreachableSteps = visitStates.Where(kv => kv.Value == VisitState.NotVisited).Select(kv => GetStep(kv.Key).Name).ToList();
        if (unreachableSteps.Count > 0)
        {
            throw new InvalidOperationException($"Workflow definition contains unreachable steps: {string.Join(", ", unreachableSteps)}");
        }
    }

    private enum VisitState
    {
        NotVisited,
        InProgress,
        Completed
    }
}
