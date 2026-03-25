Workflow Definition Context
WorkSpace
    WorkSpaceId
    budget

Workflow
    WorkflowId
    WorkSpaceId
    timeout

Integration
    type: builtIn | custom
    WorkSpaceId


WorkflowDefinition
    WorkflowId
    WorkflowVersionId
    StepDefinitions

A step can only reference steps in its "guaranteed completed" set:
    All steps that are ancestors of the current step following the same execution path
    For steps inside a parallel branch: ancestors of the ParallelStepDefinition + earlier steps within the same branch
    For steps inside a condition branch: ancestors of the ConditionStepDefinition + earlier steps within the same branch
    For steps inside a loop body: ancestors of the LoopStepDefinition + earlier steps within the same body + the loop iteration item
    For the merge step (after parallel/condition): everything above + outputs from all branches

StepDefinition (base)
    stepId
    type: Trigger | Action | Condition

TriggerStepDefinition : StepDefinition
    integrationId
    commandName
    configuration: Dictionary  (e.g., channel: "#general")
    outputSchema: StepOutputSchema

ActionStepDefinition : StepDefinition
    integrationId
    commandName
    inputMappings: Dictionary<string, TemplateOrLiteral>
    outputSchema: StepOutputSchema
    failureStrategy: Stop | Skip | Retry(n)

LoopStepDefinition : StepDefinition
    sourceArray: TemplateReference
    body: WorkflowDefinition
    concurrency: Sequential | Parallel(maxN)
    iterationFailureStrategy: StopAll | Skip | Retry(n)
    outputSchema: array of body's final step output

ConditionStepDefinition : StepDefinition
    expression: string  (e.g., "{{step1.priority}}")
    branches: Dictionary<string, StepId[]>  
    (e.g., "urgent" → [stepA], "normal" → [stepB])


Workflow Execution context
WorkflowExecution
    WorkflowExecutionId
    WorkflowVersionId
    WorkflowDefinitionContext (getNextStep)
    status
    stepExecutions: []
    StepExecution:
        status
        getInput();
        start();

AcionExecution
    WorkflowExecutionId
    workflowDefinitionId
    stepExecutionId
    retyTimes
    status
    input
    output
    execute() call integration sevice
    cancel()
    When done → publish a domain event ActionStopTheWorkflow/ActionDone/ActionSkip(workflowExecutionId, stepExecutionId, output)



