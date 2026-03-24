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



