using WorkflowAutomation.WorkflowDefinition.Domain.ValueObjects;
using WorkflowAutomation.SharedKernel.Domain;
using WorkflowAutomation.WorkflowLanguage.Domain.Enums;
using WorkflowAutomation.SharedKernel.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.Events;
using WorkflowAutomation.WorkflowDefinition.Domain.Ids;
using WorkflowAutomation.WorkflowDefinition.Domain.StepDefinitions;
using WorkflowAutomation.WorkflowLanguage.Domain.Conditions;
using WorkflowAutomation.WorkflowLanguage.Domain.Templates;

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

        ValidateReferencedStepIdsExist();
        ValidateRequiredSchemas();
        ValidateGraphShape();
        ValidateReferenceSemantics();

        void ValidateReferencedStepIdsExist()
        {
            foreach (var step in _steps)
            {
                if (step.NextStepId.HasValue)
                    GetStep(step.NextStepId.Value);

                switch (step)
                {
                    case ConditionStepDefinition condition:
                        foreach (var rule in condition.Rules)
                            GetStep(rule.TargetStepId);
                        if (condition.FallbackStepId.HasValue)
                            GetStep(condition.FallbackStepId.Value);
                        break;
                    case ParallelStepDefinition parallel:
                        foreach (var branchEntryId in parallel.BranchEntryStepIds)
                            GetStep(branchEntryId);
                        break;
                    case LoopStepDefinition loop:
                        GetStep(loop.LoopEntryStepId);
                        break;
                }
            }
        }

        void ValidateRequiredSchemas()
        {
            foreach (var step in _steps)
            {
                if (step is TriggerStepDefinition trigger && trigger.OutputSchema is null)
                    throw new InvalidOperationException(
                        $"Trigger step '{trigger.Name}' must have a non-null OutputSchema.");

                if (step is ActionStepDefinition action && action.OutputSchema is null)
                    throw new InvalidOperationException(
                        $"Action step '{action.Name}' must have a non-null OutputSchema.");
            }
        }

        void ValidateGraphShape()
        {
            var visitStates = _steps.ToDictionary(step => step.Id, _ => VisitState.NotVisited);

            void Visit(StepId stepId)
            {
                var state = visitStates[stepId];
                if (state == VisitState.InProgress)
                    throw new InvalidOperationException(
                        $"Workflow definition contains a cycle at step '{GetStep(stepId).Name}'.");
                if (state == VisitState.Completed)
                    return;

                visitStates[stepId] = VisitState.InProgress;

                foreach (var nextStepId in GetStructuralOutgoingEdges(GetStep(stepId)))
                    Visit(nextStepId);

                visitStates[stepId] = VisitState.Completed;
            }

            Visit(triggerStep.Id);

            var unreachableSteps = visitStates
                .Where(kv => kv.Value == VisitState.NotVisited)
                .Select(kv => GetStep(kv.Key).Name)
                .ToList();

            if (unreachableSteps.Count > 0)
                throw new InvalidOperationException(
                    $"Workflow definition contains unreachable steps: {string.Join(", ", unreachableSteps)}");
        }

        IReadOnlyList<StepId> GetStructuralOutgoingEdges(StepDefinition step)
        {
            var edges = new List<StepId>();

            if (step.NextStepId.HasValue)
                edges.Add(step.NextStepId.Value);

            switch (step)
            {
                case ConditionStepDefinition condition:
                    edges.AddRange(condition.Rules.Select(rule => rule.TargetStepId));
                    if (condition.FallbackStepId.HasValue)
                        edges.Add(condition.FallbackStepId.Value);
                    break;
                case ParallelStepDefinition parallel:
                    edges.AddRange(parallel.BranchEntryStepIds);
                    break;
                case LoopStepDefinition loop:
                    edges.Add(loop.LoopEntryStepId);
                    break;
            }

            return edges;
        }

        void ValidateReferenceSemantics()
        {
            /*
             * Reference visibility in this workflow model is defined by owner-step
             * barriers, not by explicit branch-tail edges:
             *
             * 1. Condition, Parallel, and Loop each own a nested local scope.
             *    Their branch/body chains terminate at null.
             *
             * 2. When a local chain reaches null, control returns to the owner step.
             *    If the owner has NextStepId, execution continues there after the
             *    owner's own completion rule is satisfied. If not, control returns to
             *    the enclosing scope.
             *
             * 3. Guaranteed-completed references are therefore scope-based:
             *    - Inside a branch/body: upstream before the owner + earlier steps in
             *      the same local path.
             *    - After Condition: only the upstream set before the condition.
             *      Branch-internal outputs are not guaranteed because only one branch
             *      executes.
             *    - After Parallel: upstream before the parallel + all outputs that are
             *      guaranteed when each branch returns to the parallel owner.
             *    - After Loop: upstream before the loop + the loop node's own output.
             *      Loop-body step outputs are iteration-local and must not leak out.
             *
             * 4. A branch/body path must terminate at null. It must not explicitly
             *    jump into its owner's continuation step via NextStepId. That would
             *    bypass the owner-step barrier semantics and make the graph mean two
             *    different things at once.
             */
            ValidateLocalPath(triggerStep.NextStepId, new HashSet<StepId> { triggerStep.Id });
        }

        HashSet<StepId> ValidateLocalPath(
            StepId? startStepId,
            HashSet<StepId> guaranteedAvailable,
            StepId? forbiddenContinuationStepId = null)
        {
            var availableAfterPath = new HashSet<StepId>(guaranteedAvailable);
            var localPathSteps = new HashSet<StepId>();
            var lastWasReview = false;
            var currentStepId = startStepId;

            while (currentStepId.HasValue)
            {
                if (forbiddenContinuationStepId.HasValue &&
                    currentStepId.Value == forbiddenContinuationStepId.Value)
                {
                    throw new InvalidOperationException(
                        $"Branch/body path must terminate at null and cannot jump directly to owner continuation step '{GetStep(currentStepId.Value).Name}'.");
                }
                var step = GetStep(currentStepId.Value);
                ValidateTemplatesInStep(step, availableAfterPath);

                switch (step)
                {
                    case ActionStepDefinition actionStep:
                    {
                        localPathSteps.Add(actionStep.Id);
                        availableAfterPath.Add(actionStep.Id);
                        lastWasReview = false;
                        currentStepId = actionStep.NextStepId;
                        break;
                    }
                    case ReviewStepDefinition reviewStep:
                    {
                        if (lastWasReview)
                            throw new InvalidOperationException(
                                $"Review step '{reviewStep.Name}' immediately follows another review step. At least one non-review step must separate consecutive review steps.");

                        if (!localPathSteps.Contains(reviewStep.RejectionTargetStepId))
                            throw new InvalidOperationException(
                                $"Review step '{reviewStep.Name}' targets step '{GetStep(reviewStep.RejectionTargetStepId).Name}' which is not on the same local path.");

                        var targetStep = GetStep(reviewStep.RejectionTargetStepId);
                        if (targetStep is ActionStepDefinition { FailureStrategy: FailureStrategy.Skip })
                            throw new InvalidOperationException(
                                $"Review step '{reviewStep.Name}' targets action step '{targetStep.Name}' whose FailureStrategy is Skip. A review target must not be skippable.");

                        localPathSteps.Add(reviewStep.Id);
                        lastWasReview = true;
                        currentStepId = reviewStep.NextStepId;
                        break;
                    }
                    case ConditionStepDefinition conditionStep:
                    {
                        foreach (var rule in conditionStep.Rules)
                        {
                            ValidateLocalPath(
                                rule.TargetStepId,
                                new HashSet<StepId>(availableAfterPath),
                                conditionStep.NextStepId);
                        }

                        if (conditionStep.FallbackStepId.HasValue)
                        {
                            ValidateLocalPath(
                                conditionStep.FallbackStepId.Value,
                                new HashSet<StepId>(availableAfterPath),
                                conditionStep.NextStepId);
                        }

                        lastWasReview = false;
                        currentStepId = conditionStep.NextStepId;
                        break;
                    }
                    case ParallelStepDefinition parallelStep:
                    {
                        var guaranteedAfterParallel = new HashSet<StepId>(availableAfterPath);

                        foreach (var branchEntryId in parallelStep.BranchEntryStepIds)
                        {
                            guaranteedAfterParallel.UnionWith(
                                ValidateLocalPath(
                                    branchEntryId,
                                    new HashSet<StepId>(availableAfterPath),
                                    parallelStep.NextStepId));
                        }

                        availableAfterPath = guaranteedAfterParallel;
                        lastWasReview = false;
                        currentStepId = parallelStep.NextStepId;
                        break;
                    }
                    case LoopStepDefinition loopStep:
                    {
                        ValidateLocalPath(
                            loopStep.LoopEntryStepId,
                            new HashSet<StepId>(availableAfterPath),
                            loopStep.NextStepId);

                        availableAfterPath.Add(loopStep.Id);
                        lastWasReview = false;
                        currentStepId = loopStep.NextStepId;
                        break;
                    }
                    case TriggerStepDefinition nestedTrigger:
                    {
                        availableAfterPath.Add(nestedTrigger.Id);
                        lastWasReview = false;
                        currentStepId = nestedTrigger.NextStepId;
                        break;
                    }
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported step type '{step.StepType}'.");
                }
            }

            return availableAfterPath;
        }

        void ValidateTemplatesInString(string text, HashSet<StepId> availableSteps, StepId stepId)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var templateReference in TemplateResolver.FindReferences(text))
            {
                ResolveReferenceFieldType(
                    templateReference.StepName,
                    templateReference.FieldName,
                    availableSteps,
                    stepId,
                    templateReference.RawText);
            }
        }

        string ResolveReferenceFieldType(
            string refStepName,
            string refFieldName,
            HashSet<StepId> availableSteps,
            StepId stepId,
            string rawReference)
        {
            if (string.Equals(refStepName, "env", StringComparison.OrdinalIgnoreCase))
                return "string";

            var referencedStep = _steps.FirstOrDefault(s => s.Name == refStepName);
            if (referencedStep == null)
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' references unknown step '{refStepName}' in template '{rawReference}'.");
            }

            if (!availableSteps.Contains(referencedStep.Id))
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' references step '{refStepName}' before it is guaranteed to complete in template '{rawReference}'.");
            }

            var schema = GetOutputSchema(referencedStep);
            if (schema is null || !schema.Fields.TryGetValue(refFieldName, out var fieldType))
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' references non-existent field '{refFieldName}' on step '{refStepName}' in template '{rawReference}'.");
            }

            return fieldType;
        }

        static StepOutputSchema? GetOutputSchema(StepDefinition referencedStep)
        {
            return referencedStep switch
            {
                TriggerStepDefinition triggerStep => triggerStep.OutputSchema,
                ActionStepDefinition actionStep => actionStep.OutputSchema,
                LoopStepDefinition loopStep => loopStep.OutputSchema,
                _ => null
            };
        }

        static string CreateConditionSampleLiteral(string fieldType)
        {
            return fieldType.Trim().ToLowerInvariant() switch
            {
                "bool" or "boolean" => "true",
                "byte" or "short" or "int" or "integer" or "long"
                    or "float" or "double" or "decimal" or "number" => "1",
                _ => "'sample'"
            };
        }

        void ValidateConditionExpressionSyntax(
            string expression,
            HashSet<StepId> availableSteps,
            StepId stepId)
        {
            var expressionSample = TemplateResolver.ReplaceReferences(expression, templateReference =>
            {
                var fieldType = ResolveReferenceFieldType(
                    templateReference.StepName,
                    templateReference.FieldName,
                    availableSteps,
                    stepId,
                    templateReference.RawText);

                return CreateConditionSampleLiteral(fieldType);
            });

            try
            {
                ConditionEvaluator.ValidateSyntax(expressionSample);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Step '{stepId}' has invalid condition expression '{expression}': {ex.Message}",
                    ex);
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
                    ValidateConditionExpressionSyntax(rule.Expression, availableSteps, step.Id);
                }
            }
            else if (step is LoopStepDefinition loopStep)
            {
                ValidateTemplatesInString(loopStep.SourceArray.Expression, availableSteps, step.Id);
            }
        }
    }

    private enum VisitState
    {
        NotVisited,
        InProgress,
        Completed
    }
}
