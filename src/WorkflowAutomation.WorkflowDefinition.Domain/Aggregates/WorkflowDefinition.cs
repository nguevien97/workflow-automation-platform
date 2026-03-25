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

    public StepDefinition GetStep(StepId stepId)
    {
        return _steps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException($"Step '{stepId}' not found in this workflow definition.");
    }

    public void Validate()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Workflow definition must contain at least one step.");

        var triggerSteps = _steps.Where(s => s.StepType == StepType.Trigger).ToList();
        if (triggerSteps.Count == 0)
            throw new InvalidOperationException("Workflow definition must contain at least one trigger step.");
        if (triggerSteps.Count > 1)
            throw new InvalidOperationException("Workflow definition must contain exactly one trigger step.");

        if (_steps[0].StepType != StepType.Trigger)
            throw new InvalidOperationException("The first step in a workflow definition must be a trigger step.");

        var stepDict = _steps.ToDictionary(s => s.Id);
        var visited = new HashSet<StepId>();
        var recursionStack = new HashSet<StepId>();
        
        // Output schemas required for Trigger and Action
        foreach (var step in _steps)
        {
            if (step is TriggerStepDefinition trigger && trigger.OutputSchema == null)
                throw new InvalidOperationException($"Trigger step '{step.Id}' must have a non-null OutputSchema.");
            
            if (step is ActionStepDefinition action && action.OutputSchema == null)
                throw new InvalidOperationException($"Action step '{step.Id}' must have a non-null OutputSchema.");
        }

        void ValidateBranch(StepId entryStepId, HashSet<StepId> availableSteps)
        {
            var currentId = entryStepId;
            var currentAvailableSteps = new HashSet<StepId>(availableSteps);

            while (currentId != null)
            {
                if (!stepDict.TryGetValue(currentId, out var currentStep))
                    throw new InvalidOperationException($"Step '{currentId}' is referenced but does not exist in the workflow.");

                if (visited.Contains(currentId))
                    break;
                
                visited.Add(currentId);
                recursionStack.Add(currentId);
                
                // Validate templates for the current step
                ValidateStepTemplates(currentStep, currentAvailableSteps);
                
                // Add current step to available steps for downstream
                currentAvailableSteps.Add(currentId);

                if (currentStep is ConditionStepDefinition conditionStep)
                {
                    foreach (var rule in conditionStep.Rules)
                    {
                        ValidateBranch(rule.TargetStepId, currentAvailableSteps);
                    }
                    if (conditionStep.FallbackStepId != null)
                    {
                        ValidateBranch(conditionStep.FallbackStepId.Value, currentAvailableSteps);
                    }
                }
                else if (currentStep is ParallelStepDefinition parallelStep)
                {
                    foreach (var branchEntryId in parallelStep.BranchEntryStepIds)
                    {
                        ValidateBranch(branchEntryId, currentAvailableSteps);
                    }
                    
                    // The merge step (NextStepId) can reference all steps from all parallel branches
                    // We need to collect them, but traversing them here again just to collect ids is complex. 
                    // For now, any step in any parallel branch is added to the main available steps after completion.
                    // Let's defer adding branch steps to available steps until we traverse them properly or simplify it.
                    // According to requirements: "The merge step ... Can reference all steps upstream ... AND steps from all parallel branches"
                    // So we add all reachable steps from branches to currentAvailableSteps.
                    foreach (var branchEntryId in parallelStep.BranchEntryStepIds)
                    {
                        CollectBranchSteps(branchEntryId, currentAvailableSteps);
                    }
                }

                recursionStack.Remove(currentId);
                
                if (currentStep.NextStepId != null)
                {
                    if (recursionStack.Contains(currentStep.NextStepId.Value))
                        throw new InvalidOperationException($"Cycle detected in workflow definition at step '{currentStep.NextStepId.Value}'.");
                    
                    currentId = currentStep.NextStepId.Value;
                }
                else
                {
                    break;
                }
            }
        }

        void CollectBranchSteps(StepId branchEntryId, HashSet<StepId> collection)
        {
            StepId? cid = branchEntryId;
            while (cid != null && stepDict.TryGetValue(cid.Value, out var currentStep))
            {
                collection.Add(cid.Value);
                cid = currentStep.NextStepId;
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
                // Currently conditions, parallels don't have outputschemas, loops outputs are complex. Let's assume Trigger/Action for now.

                if (schema != null && !schema.Fields.ContainsKey(refFieldName))
                    throw new InvalidOperationException($"Step '{stepId}' references non-existent field '{refFieldName}' on step '{refStepName}' in template '{match.Value}'.");
            }
        }
        
        void ValidateStepTemplates(StepDefinition step, HashSet<StepId> availableSteps)
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
                
                // For loops, we need to validate inner steps with the loop context
                var loopAvailableSteps = new HashSet<StepId>(availableSteps);
                foreach (var s in loopStep.Steps)
                {
                    ValidateStepTemplates(s, loopAvailableSteps);
                    loopAvailableSteps.Add(s.Id);
                }
            }
        }

        var initialAvailableSteps = new HashSet<StepId>();
        ValidateBranch(_steps[0].Id, initialAvailableSteps);

        var reachableStepIds = new HashSet<StepId>(visited);

        // Check steps contained inside LoopStepDefinition.
        void CheckLoopSteps(StepDefinition step)
        {
            if (step is LoopStepDefinition loopStep && loopStep.Steps != null)
            {
                foreach(var s in loopStep.Steps)
                {
                    reachableStepIds.Add(s.Id);
                    CheckLoopSteps(s);
                }
            }
        }

        foreach(var stepId in visited)
        {
            CheckLoopSteps(stepDict[stepId]);
        }

        if (reachableStepIds.Count < _steps.Count)
        {
            var orphanedSteps = _steps.Where(s => !reachableStepIds.Contains(s.Id)).Select(s => s.Id).ToList();
            throw new InvalidOperationException($"Workflow definition contains unreachable (orphaned) steps: {string.Join(", ", orphanedSteps)}");
        }
    }
}
