# DDD Diagram Set

This document collects the common DDD diagram types used to describe the workflow automation platform. It combines strategic design views, tactical design views, state models, collaboration flows, and the full domain-event surface.

## Included Diagram Types

- strategic subdomain landscape
- strategic context map
- aggregate landscape
- definition-context domain model view
- execution-context domain model view
- aggregate state models
- collaboration sequence diagrams
- comprehensive domain event topology

## Strategic Subdomain Landscape

```mermaid
flowchart TB
    subgraph CORE["Core Domain"]
        CORE1["WorkflowExecution Context<br/>runtime workflow orchestration"]
    end

    subgraph SUP["Supporting Domains"]
        SUP1["WorkflowDefinition Context<br/>authoring and versioning"]
        SUP2["WorkflowLanguage Context<br/>templates, conditions, control language"]
        SUP3["Application coordination<br/>cross-aggregate routing"]
    end

    subgraph GEN["Generic and Foundational"]
        GEN1["SharedKernel.Domain<br/>common domain building blocks"]
        GEN2["Integration context and external systems"]
    end

    SUP1 --> CORE1
    SUP2 --> SUP1
    SUP2 --> CORE1
    SUP3 --> CORE1
    CORE1 --> SUP3
    GEN1 --> SUP1
    GEN1 --> SUP2
    GEN1 --> CORE1
    SUP3 --> GEN2
```

## Strategic Context Map

```mermaid
flowchart LR
    USER["Users and operators"]

    subgraph SK["Shared Kernel"]
        SKN["WorkflowAutomation.SharedKernel.Domain<br/>AggregateRoot, Entity, ValueObject,<br/>IRepository, strongly typed IDs"]
    end

    subgraph LANG["Workflow Language Context"]
        WL["WorkflowAutomation.WorkflowLanguage.Domain<br/>FailureStrategy, ConcurrencyMode,<br/>IterationFailureStrategy,<br/>TemplateResolver, ConditionEvaluator"]
    end

    subgraph DEF["Workflow Definition Context"]
        WD["WorkflowAutomation.WorkflowDefinition.Domain<br/>Workflow, WorkflowDefinition, WorkSpace,<br/>Integration metadata, step definitions,<br/>authoring validation and versioning"]
    end

    subgraph EXEC["Workflow Execution Context"]
        WE["WorkflowAutomation.WorkflowExecution.Domain<br/>WorkflowExecution, ActionExecution,<br/>LoopExecution, StepExecution,<br/>WorkflowDefinitionSnapshot"]
    end

    subgraph APP["Application Coordination (assumed)"]
        APPN["Handlers and process managers<br/>persist aggregates, route events,<br/>spawn child workflows, cancel correlated children,<br/>schedule retries, observe deadlines"]
    end

    subgraph INT["Integration Context and External Systems"]
        INTN["Integration gateway, rate limiting,<br/>auth and token lifecycle, external APIs"]
    end

    USER -->|authors workflows| WD
    SKN -->|shared kernel| WL
    SKN -->|shared kernel| WD
    SKN -->|shared kernel| WE
    WL -->|templates, conditions, enums| WD
    WL -->|templates, conditions, enums| WE
    WD -->|supplies immutable workflow version snapshots| WE
    WE -->|domain events| APPN
    APPN -->|commands and persistence| WE
    APPN -->|dispatch integration requests| INTN
    INTN -->|success and failure outcomes| APPN
```

## Relationship Notes

- `WorkflowAutomation.SharedKernel.Domain` is the actual shared kernel. `WorkflowAutomation.WorkflowLanguage.Domain` is a separate supporting context consumed by both design-time and runtime contexts.
- `WorkflowAutomation.WorkflowDefinition.Domain` is upstream of `WorkflowAutomation.WorkflowExecution.Domain` through immutable workflow version snapshots rather than direct reuse of mutable authoring aggregates.
- `WorkflowAutomation.WorkflowExecution.Domain` contains three cooperating runtime aggregates: `WorkflowExecution`, `ActionExecution`, and `LoopExecution`.
- Application coordination is not itself a bounded context, but it is the seam where cross-aggregate routing happens.
- Integration concerns such as throttling, token lifecycle, and external transport remain outside the domain layer.

## Aggregate Landscape

```mermaid
flowchart LR
        subgraph DEFAGG["Definition Context"]
                WS["WorkSpace<br/>aggregate"]
                WF["Workflow<br/>aggregate"]
                WFD["WorkflowDefinition<br/>aggregate"]
                INTD["Integration<br/>entity"]
                STEPS["StepDefinition hierarchy"]

                WS -->|tracks workflow ids| WF
                WS -->|tracks integration ids| INTD
                WF -->|versioned as| WFD
                WFD -->|contains| STEPS
        end

        subgraph EXECAGG["Execution Context"]
                WEX["WorkflowExecution<br/>aggregate"]
                SEX["StepExecution<br/>entity"]
                SNAP["WorkflowDefinitionSnapshot<br/>value object"]
                PCTX["ParentExecutionContext<br/>value object"]

                AEX["ActionExecution<br/>aggregate"]
                ADEF["ActionStepDefinitionSnapshot<br/>value object"]
                AATT["ActionAttempt<br/>value object"]

                LEX["LoopExecution<br/>aggregate"]
                LIT["LoopIteration<br/>entity"]

                WEX -->|contains| SEX
                WEX --> SNAP
                WEX -->|optional child correlation| PCTX

                AEX --> ADEF
                AEX -->|contains| AATT

                LEX -->|contains| LIT

                WEX -.step execution correlation.- AEX
                WEX -.parent loop step correlation.- LEX
        end
```

## WorkflowDefinition Domain Model View

```mermaid
classDiagram
direction LR

class WorkSpace {
    +Name
    +Budget
}

class Workflow {
    +WorkSpaceId
    +Name
    +Timeout
}

class WorkflowDefinition {
    +WorkflowId
    +Steps
}

class Integration {
    +Name
    +IntegrationType
    +WorkSpaceId
}

class StepDefinition {
    <<abstract>>
    +Id
    +Name
    +StepType
    +NextStepId
}

class TriggerStepDefinition {
    +IntegrationId
    +CommandName
    +Configuration
    +OutputSchema
}

class ActionStepDefinition {
    +IntegrationId
    +CommandName
    +InputMappings
    +FailureStrategy
    +RetryCount
    +OutputSchema
}

class LoopStepDefinition {
    +SourceArray
    +LoopEntryStepId
    +ConcurrencyMode
    +IterationFailureStrategy
    +OutputSchema
}

class ConditionStepDefinition {
    +Rules
    +FallbackStepId
}

class ParallelStepDefinition {
    +BranchEntryStepIds
}

class ReviewStepDefinition {
    +RejectionTargetStepId
    +MaxRejections
}

WorkSpace --> Workflow : owns
WorkSpace --> Integration : owns
Workflow --> WorkflowDefinition : versioned as
WorkflowDefinition *-- StepDefinition : contains
TriggerStepDefinition --|> StepDefinition
ActionStepDefinition --|> StepDefinition
LoopStepDefinition --|> StepDefinition
ConditionStepDefinition --|> StepDefinition
ParallelStepDefinition --|> StepDefinition
ReviewStepDefinition --|> StepDefinition
```

## WorkflowExecution Domain Model View

```mermaid
classDiagram
direction LR

class WorkflowExecution {
    +WorkflowVersionId
    +EntryStepId
    +Status
    +CreatedAt
    +CompletedAt
}

class StepExecution {
    +StepId
    +Status
    +Input
    +Output
    +StartedAt
    +CompletedAt
}

class WorkflowDefinitionSnapshot {
    +AllSteps
}

class StepDefinitionInfo {
    <<abstract>>
    +StepId
    +Name
    +StepType
    +NextStepId
}

class TriggerStepInfo
class ActionStepInfo
class LoopStepInfo
class ConditionStepInfo
class ParallelStepInfo
class ReviewStepInfo

class ParentExecutionContext {
    +ParentExecutionId
    +LoopStepId
    +UpstreamStepOutputs
}

class ActionExecution {
    +StepExecutionId
    +Status
    +DeadlineUtc
    +AttemptCount
    +NextRetryAtUtc
}

class ActionStepDefinitionSnapshot {
    +StepId
    +IntegrationId
    +CommandName
    +FailureStrategy
    +MaxRetries
}

class ActionAttempt {
    +AttemptNumber
    +StartedAtUtc
    +CompletedAtUtc
    +Succeeded
}

class LoopExecution {
    +StepExecutionId
    +LoopStepId
    +Status
    +ConcurrencyMode
    +IterationFailureStrategy
}

class LoopIteration {
    +Index
    +IterationItem
    +Status
    +Output
    +Error
}

WorkflowExecution *-- StepExecution
WorkflowExecution --> WorkflowDefinitionSnapshot
WorkflowExecution --> ParentExecutionContext : optional
WorkflowDefinitionSnapshot *-- StepDefinitionInfo : contains
TriggerStepInfo --|> StepDefinitionInfo
ActionStepInfo --|> StepDefinitionInfo
LoopStepInfo --|> StepDefinitionInfo
ConditionStepInfo --|> StepDefinitionInfo
ParallelStepInfo --|> StepDefinitionInfo
ReviewStepInfo --|> StepDefinitionInfo
ActionExecution --> ActionStepDefinitionSnapshot
ActionExecution *-- ActionAttempt
LoopExecution *-- LoopIteration
ActionExecution .. WorkflowExecution : correlates by StepExecutionId
LoopExecution .. WorkflowExecution : correlates by parent loop StepExecutionId
```

## Aggregate State Models

### WorkflowExecution State Model

```mermaid
stateDiagram-v2
        [*] --> Pending
        Pending --> Running : Start
        Running --> Completed : CompleteWorkflow
        Running --> Failed : FailWorkflow
        Running --> Suspended : Suspend
        Suspended --> Running : Resume
        Running --> Cancelled : Cancel
        Suspended --> Cancelled : Cancel
```

### ActionExecution State Model

```mermaid
stateDiagram-v2
        [*] --> Pending
        Pending --> Running : Execute
        Pending --> Failed : RecordDeadlineExceeded
        Pending --> Cancelled : Cancel

        Running --> Completed : RecordIntegrationSucceeded
        Running --> WaitingForRetry : RecordIntegrationFailed with retry window
        Running --> Failed : RecordIntegrationFailed stop or exhausted
        Running --> Failed : RecordDeadlineExceeded
        Running --> Skipped : RecordIntegrationFailed skip
        Running --> Cancelled : Cancel

        WaitingForRetry --> Running : Execute when due
        WaitingForRetry --> Failed : RecordDeadlineExceeded
        WaitingForRetry --> Cancelled : Cancel
```

### LoopExecution State Model

```mermaid
stateDiagram-v2
        [*] --> Pending
        Pending --> Running : Start with source items
        Pending --> Completed : Start with empty source
        Pending --> Cancelled : Cancel

        Running --> Completed : All iterations terminal
        Running --> Failed : Stop strategy failure
        Running --> Cancelled : Cancel
```

## Collaboration Sequence Diagrams

### Action Step Coordination

```mermaid
sequenceDiagram
        participant WE as WorkflowExecution
        participant APP as Application handlers
        participant AE as ActionExecution
        participant INT as Integration context
        participant WDG as Deadline watchdog

        WE-->>APP: ActionExecutionRequestedEvent
        APP->>AE: Execute(now)
        AE-->>INT: IntegrationRequestedEvent
        INT-->>APP: integration success or failure result
        APP->>AE: RecordIntegrationSucceeded or RecordIntegrationFailed
        AE-->>APP: ActionCompletedEvent or ActionFailedEvent or ActionSkippedEvent
        WDG->>AE: RecordDeadlineExceeded when overdue
        AE-->>APP: ActionFailedEvent on deadline expiry
        APP->>WE: RecordStepCompleted or RecordStepFailed or RecordStepSkipped
```

### Loop Step Coordination

```mermaid
sequenceDiagram
        participant PWE as Parent WorkflowExecution
        participant APP as Application handlers
        participant LE as LoopExecution
        participant CWE as Child WorkflowExecution

        PWE-->>APP: LoopExecutionStartedEvent
        APP->>LE: Start
        LE-->>APP: LoopIterationStartedEvent
        APP->>CWE: Create child workflow execution
        CWE-->>APP: WorkflowCompletedEvent or WorkflowFailedEvent
        APP->>LE: RecordIterationCompleted or RecordIterationFailed
        LE-->>APP: LoopCompletedEvent or LoopFailedEvent or LoopCancelledEvent
        APP->>PWE: RecordStepCompleted or RecordStepFailed
        APP->>CWE: Cancel correlated child workflows on loop failure or cancellation
```

## Comprehensive Domain Event Topology

```mermaid
flowchart LR
    APP["Application handlers and projections"]
    INT["Integration context"]

    subgraph DEFCTX["WorkflowDefinition Context"]
        WD["WorkflowDefinition"]
        WD1["WorkflowDefinitionCreatedEvent"]
        WD2["WorkflowVersionCreatedEvent"]
        WD --> WD1
        WD --> WD2
    end

    subgraph EXECCTX["WorkflowExecution Context"]
        subgraph WEGRP["WorkflowExecution aggregate"]
            WE["WorkflowExecution"]
            WE1["WorkflowStartedEvent"]
            WE2["WorkflowCompletedEvent"]
            WE3["WorkflowFailedEvent"]
            WE4["StepCompletedEvent"]
            WE5["StepFailedEvent"]
            WE6["StepSkippedEvent"]
            WE7["ConditionBranchSelectedEvent"]
            WE8["ParallelBranchesForkedEvent"]
            WE9["ParallelBranchesMergedEvent"]
            WE10["ReviewStepReachedEvent"]
            WE11["ReviewStepRejectedEvent"]
            WE12["ActionExecutionRequestedEvent"]
            WE13["LoopExecutionStartedEvent"]
            WE --> WE1
            WE --> WE2
            WE --> WE3
            WE --> WE4
            WE --> WE5
            WE --> WE6
            WE --> WE7
            WE --> WE8
            WE --> WE9
            WE --> WE10
            WE --> WE11
            WE --> WE12
            WE --> WE13
        end

        subgraph AEGRP["ActionExecution aggregate"]
            AE["ActionExecution"]
            AE1["IntegrationRequestedEvent"]
            AE2["ActionCompletedEvent"]
            AE3["ActionFailedEvent"]
            AE4["ActionSkippedEvent"]
            AE5["ActionCancelledEvent"]
            AE --> AE1
            AE --> AE2
            AE --> AE3
            AE --> AE4
            AE --> AE5
        end

        subgraph LEGRP["LoopExecution aggregate"]
            LE["LoopExecution"]
            LE1["LoopIterationStartedEvent"]
            LE2["LoopIterationCompletedEvent"]
            LE3["LoopIterationFailedEvent"]
            LE4["LoopIterationSkippedEvent"]
            LE5["LoopCompletedEvent"]
            LE6["LoopFailedEvent"]
            LE7["LoopCancelledEvent"]
            LE --> LE1
            LE --> LE2
            LE --> LE3
            LE --> LE4
            LE --> LE5
            LE --> LE6
            LE --> LE7
        end
    end

    WD1 --> APP
    WD2 --> APP

    WE1 --> APP
    WE2 --> APP
    WE3 --> APP
    WE4 --> APP
    WE5 --> APP
    WE6 --> APP
    WE7 --> APP
    WE8 --> APP
    WE9 --> APP
    WE10 --> APP
    WE11 --> APP
    WE12 --> APP
    WE13 --> APP

    AE1 --> INT
    AE2 --> APP
    AE3 --> APP
    AE4 --> APP
    AE5 --> APP

    LE1 --> APP
    LE2 --> APP
    LE3 --> APP
    LE4 --> APP
    LE5 --> APP
    LE6 --> APP
    LE7 --> APP
```

This topology is intentionally event-focused. Commands such as `Execute`, `Start`, `RecordIntegrationSucceeded`, `RecordIterationCompleted`, and `RecordStepCompleted` are omitted so the full event surface remains readable.

## Ownership Summary

| Context | Main model | Owns decisions about |
| --- | --- | --- |
| SharedKernel.Domain | `AggregateRoot`, `Entity`, `ValueObject`, `IRepository`, IDs | shared domain building blocks |
| WorkflowLanguage.Domain | enums, template resolution, condition evaluation | expression and control-language semantics |
| WorkflowDefinition.Domain | `Workflow`, `WorkflowDefinition`, step definitions, output schemas | authoring-time validity, versioning, allowed references |
| WorkflowExecution.Domain | `WorkflowExecution`, `ActionExecution`, `LoopExecution`, `StepExecution` | runtime progression, retries, deadline state, loop policy |
| Application coordination | handlers and process managers | cross-aggregate routing, child-workflow correlation, deadline observation |
| Integration and external systems | gateways and provider adapters | transport, rate limiting, auth, external API interaction |

## Current Architectural Decisions

- Action timeout is modeled as overall deadline expiry. The domain aggregate owns `DeadlineUtc` and terminal expiry through `RecordDeadlineExceeded`, while application services or watchdogs decide when to invoke it.
- `LoopExecution` owns loop policy, but application handlers own child-workflow correlation and cancellation after terminal loop events.
- `WorkflowExecution` remains the graph orchestrator. It delegates action-step policy to `ActionExecution` and loop-step policy to `LoopExecution`.
