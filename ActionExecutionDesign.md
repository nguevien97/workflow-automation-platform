# Execution Runtime Aggregate Design

## Purpose

This document captures the runtime aggregate model for workflow execution. It started as an `ActionExecution`-focused note, but the loop discussion makes it clear that the runtime model is really about three cooperating aggregates:

- `WorkflowExecution`
- `ActionExecution`
- `LoopExecution`

The short version:

- `WorkflowDefinition` owns design-time graph validity.
- `WorkflowLanguage` owns reusable language primitives such as templates, conditions, and failure enums.
- `WorkflowExecution` owns workflow-level graph progression, review and parallel semantics, top-level step state, and terminal workflow status.
- `ActionExecution` owns the runtime lifecycle of a single action step after that step has started: integration dispatch, retry policy, timeout handling, cancellation, and final action outcome.
- `LoopExecution` owns the runtime lifecycle of a single loop step after that step has started: child workflow spawning decisions, concurrency windows, iteration aggregation, iteration-failure handling, and final loop outcome.
- The application layer should route events and commands between aggregates. It should not own action policy or loop policy.

`ActionExecution` should not decide what the next workflow step is. `WorkflowExecution` already owns workflow graph continuation. `LoopExecution` should not decide what the next workflow step is either; it only decides the runtime outcome of the loop step it owns.

---

## Current Architecture Review

### Shared Kernel

`WorkflowAutomation.SharedKernel.Domain` provides aggregate/entity/value-object base classes and strongly typed IDs. The rest of the solution already follows a DDD-style model with aggregate roots per consistency boundary.

### Workflow Definition Context

`WorkflowAutomation.WorkflowDefinition.Domain` is the design-time model.

It already owns the hard parts of authoring validation:

- exactly one trigger and a reachable acyclic graph
- template references only to guaranteed-completed upstream steps
- owner-barrier semantics for condition, parallel, and loop scopes
- review-step local-path validation and rejection-target rules
- action-step failure strategy configuration (`Stop`, `Skip`, `Retry`)

This context is intentionally responsible for what a workflow means, not how a specific run behaves.

### Workflow Language Context

`WorkflowAutomation.WorkflowLanguage.Domain` provides shared execution-language concepts:

- `FailureStrategy`
- loop concurrency and iteration failure enums
- template resolution
- condition parsing/evaluation

This keeps the workflow-definition and workflow-execution contexts from duplicating expression logic.

### Workflow Execution Context

`WorkflowAutomation.WorkflowExecution.Domain` is the runtime domain.

Today the codebase already contains four important runtime building blocks:

1. `WorkflowExecution`
    Owns workflow-level runtime state, current status, step executions, branch merge rules, loop start events, review-step approvals/rejections, and workflow completion/failure.

2. `StepExecution`
   Tracks the lifecycle of an individual step inside the workflow aggregate. It is intentionally lightweight: input, output, status, timestamps, and error.

3. `WorkflowDefinitionSnapshot`
   Freezes a workflow version inside the execution aggregate so running executions are isolated from workflow edits.

4. `ActionExecution`
    Already points in the right direction: some step types have their own long-running runtime policy and should not force that policy into `WorkflowExecution`.

This part of the architecture is coherent: the workflow aggregate is the graph orchestrator and step tracker, and `ActionExecution` already shows that specialized step runtimes can justify their own aggregates.

### The Missing Runtime Pieces

`ActionExecution` exists, but only as a partial worker aggregate, and `LoopExecution` is still implicit rather than modeled.

What already exists:

- `WorkflowExecution.ExecuteStep` resolves action inputs and raises `ActionExecutionRequestedEvent`.
- `WorkflowExecution.ExecuteStep` starts loop steps and raises `LoopExecutionStartedEvent`.
- `ActionExecution` stores input, output, failure strategy snapshot, retry count, and emits terminal action events.

What is still missing:

- complete creation and persistence flow for `ActionExecution`
- `execute()` semantics that publish an integration request
- retry scheduling/backoff behavior and timeout handling for action steps
- explicit `LoopExecution` aggregate with its own state, invariants, and event contracts
- child-execution correlation, aggregation, concurrency, and iteration-failure handling for loop steps
- tests for both `ActionExecution` and `LoopExecution`

At the moment, external handlers can call `WorkflowExecution.RecordStepCompleted/Skipped/Failed` directly for specialized step types. That bypasses the reason those runtime aggregates should exist in the first place.

---

## Design Intent

The requirements and existing code imply the following separation:

- `WorkflowExecution` is the workflow graph aggregate.
- `ActionExecution` is the runtime aggregate for one action step.
- `LoopExecution` is the runtime aggregate for one loop step.
- `IntegrationRequest` or an equivalent integration-side process handles rate limiting and the actual external call.

That means the action lifecycle should look like this:

1. `WorkflowExecution` reaches an action step.
2. The action step becomes `Running` and publishes `ActionExecutionRequestedEvent`.
3. An application handler creates or loads the `ActionExecution` aggregate for that step execution and tells it to execute.
4. `ActionExecution` publishes `IntegrationRequested`.
5. Integration-side processing eventually reports success, failure, timeout, or integration unavailability.
6. `ActionExecution` decides whether to complete, skip, fail, retry later, or cancel.
7. Only terminal action events flow back into `WorkflowExecution` to update the step execution and advance or fail the workflow.

For loop steps, there is a parallel but distinct flow:

1. `WorkflowExecution` reaches a loop step, creates a running `StepExecution` for it, and publishes `LoopExecutionStartedEvent`.
2. An application handler creates or loads `LoopExecution` and calls `Start`.
3. `LoopExecution` decides which child `WorkflowExecution` instances may be spawned now and emits spawn requests.
4. Child executions run independently; action steps inside them still use `ActionExecution`.
5. Child terminal results flow back into `LoopExecution`.
6. `LoopExecution` decides whether to spawn more children, mark iterations skipped, fail the loop, or complete the loop with aggregated output.
7. Only the terminal loop outcome flows back into the parent `WorkflowExecution` to complete or fail the loop step.

This keeps graph progression, action policy, and loop policy separate without pushing core loop rules into the application layer.

---

## Runtime Aggregate Model

### WorkflowExecution Aggregate

`WorkflowExecution` remains the aggregate root for one workflow run.

It owns:

- workflow status and terminal transitions
- `StepExecution` entities for the currently retained steps in the workflow run
- condition, parallel, and review semantics
- starting specialized runtimes for action and loop steps

It does not own:

- per-integration retry/timeout policy
- loop concurrency windows, iteration aggregation, or child-workflow spawn decisions

Key invariants:

1. There is exactly one workflow status for the execution.
2. All retained `StepExecution` entities belong to this `WorkflowExecution`.
3. Parallel merge and review rewind rules are enforced inside the aggregate.
4. A loop step `StepExecution` remains `Running` until a `LoopExecution` causes the parent workflow to complete or fail that step.
5. `WorkflowExecution` never directly owns per-attempt integration retry state.

### ActionExecution Aggregate

`ActionExecution` is one runtime execution of one action step for one `StepExecutionId`.

It owns:

- action input after template resolution
- integration dispatch and correlation
- retry and timeout policy for that action step
- terminal action outcome

It does not own:

- workflow graph continuation
- loop iteration spawning or aggregation
- parent workflow failure strategy beyond the action step itself

Key invariants:

1. There is at most one `ActionExecution` for a given `StepExecutionId`.
2. There is at most one active integration attempt at a time.
3. Retry, skip, fail, and timeout decisions are made within this aggregate.
4. A terminal `ActionExecution` cannot be restarted.

### LoopExecution Aggregate

`LoopExecution` is one runtime execution of one loop step for one parent loop `StepExecutionId`.

It owns:

- the loop source items and their stable ordering
- child `WorkflowExecution` spawn decisions
- concurrency windows for sequential or bounded-parallel execution
- per-iteration status and per-iteration result snapshots
- iteration-failure handling according to `IterationFailureStrategy`
- the aggregated loop output and terminal loop outcome

It does not own:

- action retry internals inside child executions
- parent workflow graph continuation beyond the loop step
- non-loop workflow semantics such as review rewinds or parallel merges

Key invariants:

1. There is at most one `LoopExecution` for a given parent loop `StepExecutionId`.
2. Every source item maps to exactly one iteration slot and one eventual terminal result.
3. The number of active child executions never exceeds the configured concurrency limit.
4. Aggregated loop output preserves source-item order.
5. A loop completes only when all iterations are terminal.
6. Under `StopAll`, once a terminal-failing iteration is recorded, no new iterations may be spawned.
7. Under `Skip`, failed iterations are recorded explicitly and the loop may still complete.
8. If the parent loop step is re-entered after a rewind, a new parent `StepExecutionId` implies a new `LoopExecution`.

## ActionExecution Specification

### ActionExecution Aggregate Boundary

`ActionExecution` represents one runtime execution of one action step for one `StepExecutionId`.

Consistency boundary:

- one aggregate instance per step execution
- one active integration attempt at a time
- one terminal outcome: `Completed`, `Skipped`, `Failed`, or `Cancelled`

It owns:

- action input payload after template resolution
- immutable step-definition snapshot relevant to runtime behavior
- immutable integration-action snapshot used for hot-swap safety
- retry bookkeeping
- timeout/deadline bookkeeping
- terminal action outcome and last error
- attempt correlation for idempotent response handling

It does not own:

- workflow graph traversal
- step dependency resolution
- branch merge rules
- loop iteration coordination
- rate-limiter queues or external API execution

---

## Required State Model

The current `ActionExecutionStatus` enum is insufficient because it cannot represent a skipped terminal state or a retry wait state.

Replace it with:

```csharp
public enum ActionExecutionStatus
{
    Pending,
    Running,
    WaitingForRetry,
    Completed,
    Skipped,
    Failed,
    Cancelled
}
```

Rationale:

- `Skipped` must be explicit. A skipped action is not the same thing as a failed action that happened to emit `ActionSkippedEvent`.
- `WaitingForRetry` must be explicit. A retryable failure is not terminal and must not be modeled as the same state as a permanently failed action.

Optional future extension:

- `TimedOut` can remain represented as terminal `Failed` plus a failure reason. A separate status is not required unless the UI needs first-class timeout filtering.

---

## Required Data

The aggregate should store the following data.

### Identity and Correlation

- `ActionExecutionId Id`
- `WorkflowExecutionId WorkflowExecutionId`
- `StepExecutionId StepExecutionId`
- `StepId StepId`

### Immutable Runtime Snapshot

- `ActionStepDefinitionSnapshot StepDefinition`
- `IntegrationActionSnapshot IntegrationAction`

`IntegrationActionSnapshot` is not implemented in the current repo, but the spec requires it because running executions must survive integration-definition hot-swaps.

Suggested shape:

```csharp
public sealed class IntegrationActionSnapshot : ValueObject
{
    public IntegrationId IntegrationId { get; }
    public string CommandName { get; }
    public string IntegrationDefinitionVersion { get; }
    public TimeSpan? TimeoutOverride { get; }
    public string? RateLimitPartitionKey { get; }
}
```

The exact fields can evolve, but the aggregate must carry enough information to execute against a frozen integration definition.

### Execution State

- `ActionExecutionStatus Status`
- `StepInput Input`
- `StepOutput? Output`
- `string? LastError`
- `int AttemptCount`
- `DateTime CreatedAtUtc`
- `DateTime? StartedAtUtc`
- `DateTime? CompletedAtUtc`
- `DateTime? LastAttemptStartedAtUtc`
- `DateTime? LastAttemptCompletedAtUtc`
- `DateTime? NextRetryAtUtc`
- `DateTime DeadlineUtc`

Why `AttemptCount` instead of relying only on `RetryCount`:

- requirements talk about `Retry(N)`
- code reads more clearly when total dispatches and allowed retries are separated
- it removes off-by-one ambiguity

Derived values:

- `RetryCount = Math.Max(AttemptCount - 1, 0)`
- `HasRemainingRetries = RetryCount < StepDefinition.MaxRetries`

If the team wants to keep `RetryCount` as stored state, the invariant must be documented precisely: terminal failure occurs only after recording the failed attempt that makes `RetryCount > MaxRetries`.

### Attempt History

Add a value object collection for auditability and stale-response protection.

```csharp
public sealed class ActionAttempt : ValueObject
{
    public int AttemptNumber { get; }
    public DateTime StartedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; }
    public string? IntegrationRequestId { get; }
    public string? Error { get; }
    public bool Succeeded { get; }
}
```

Only the latest attempt needs to be mutable while in flight; completed attempts can be appended immutably.

---

## Aggregate Invariants

1. There is at most one `ActionExecution` for a given `StepExecutionId`.
2. Terminal states are `Completed`, `Skipped`, `Failed`, and `Cancelled`.
3. A terminal aggregate cannot be executed again.
4. `Output` is non-null only when status is `Completed`.
5. `NextRetryAtUtc` is non-null only when status is `WaitingForRetry`.
6. An integration response must match the current in-flight attempt number. Stale duplicate responses are ignored or rejected.
7. `DeadlineUtc` is immutable once the aggregate is created.
8. `Cancelled` is distinct from `Skipped`.
   `Skipped` means the step's configured failure strategy allowed forward progress.
   `Cancelled` means execution was externally aborted.

---

## Public Behavior

## Creation

Creation happens in an application handler that consumes `ActionExecutionRequestedEvent` from `WorkflowExecution`.

The handler must:

1. create a new `ActionExecution` for the `StepExecutionId` if one does not already exist
2. materialize the required snapshots
3. compute `DeadlineUtc`
4. call `Execute(now)` on the aggregate

Creation must be idempotent. If the same workflow event is delivered twice, the handler should load the existing action execution by `StepExecutionId` and avoid creating a duplicate.

## Execute

Command:

```csharp
void Execute(DateTime nowUtc)
```

Valid from:

- `Pending`
- `WaitingForRetry` when `nowUtc >= NextRetryAtUtc`

Effects:

- transition to `Running`
- increment `AttemptCount`
- set `StartedAtUtc` if first attempt
- set `LastAttemptStartedAtUtc`
- clear `NextRetryAtUtc`
- publish `IntegrationRequested`

Required event payload:

- `ActionExecutionId`
- `WorkflowExecutionId`
- `StepExecutionId`
- `StepId`
- `IntegrationId`
- `CommandName`
- frozen integration definition version/correlation data
- `StepInput`
- `AttemptNumber`
- `DeadlineUtc`
- idempotency key such as `ActionExecutionId + AttemptNumber`

This event is the runtime handoff from action policy to integration execution.

## Record Success

Command:

```csharp
void RecordIntegrationSucceeded(int attemptNumber, StepOutput output, DateTime nowUtc)
```

Valid from:

- `Running`

Preconditions:

- `attemptNumber` must equal the current in-flight attempt

Effects:

- set `Status = Completed`
- set `Output`
- set completion timestamps
- publish `ActionCompletedEvent`

`ActionCompletedEvent` should include `ActionExecutionId` in addition to the workflow/step identifiers.

## Record Failure

Command:

```csharp
void RecordIntegrationFailed(
    int attemptNumber,
    string error,
    DateTime nowUtc,
    DateTime? retryAtUtc)
```

Valid from:

- `Running`

Preconditions:

- `attemptNumber` must equal the current in-flight attempt

Decision table:

- `FailureStrategy.Stop`: transition to `Failed`, publish `ActionFailedEvent`
- `FailureStrategy.Skip`: transition to `Skipped`, publish `ActionSkippedEvent`
- `FailureStrategy.Retry` and retries remain and `retryAtUtc < DeadlineUtc`: transition to `WaitingForRetry`, publish `ActionRetryScheduledEvent`
- `FailureStrategy.Retry` but no retries remain: transition to `Failed`, publish `ActionFailedEvent`
- `FailureStrategy.Retry` but deadline already passed: transition to `Failed`, publish `ActionFailedEvent`

`ActionRetryScheduledEvent` is missing today and must be added. Without it, the application layer has no durable signal that the aggregate is waiting to be re-dispatched.

## Record Timeout

Command:

```csharp
void RecordTimedOut(int attemptNumber, DateTime nowUtc, string reason)
```

Timeout is treated as a specialized failure path.

It must reuse the same failure-strategy decision table:

- stop -> failed
- skip -> skipped
- retry -> waiting for retry if budget remains and deadline allows it

This keeps timeout semantics aligned with ordinary integration failures.

## Cancel

Command:

```csharp
void Cancel(DateTime nowUtc, string reason)
```

Valid from:

- `Pending`
- `Running`
- `WaitingForRetry`

Effects:

- transition to `Cancelled`
- set completion timestamps
- publish `ActionCancelledEvent`

Not valid from:

- `Completed`
- `Skipped`
- `Failed`
- `Cancelled`

This is stricter than the current implementation and avoids turning a terminal failed action into a cancelled one after the fact.

---

## Event Choreography

## Existing Events to Keep

- `ActionExecutionRequestedEvent`
- `ActionCompletedEvent`
- `ActionFailedEvent`
- `ActionSkippedEvent`
- `ActionCancelledEvent`

## Events to Add

- `IntegrationRequested`
- `IntegrationRequestSucceeded`
- `IntegrationRequestFailed`
- `ActionRetryScheduledEvent`

If the integration layer already has its own aggregate and names, use those names. The important point is the contract shape, not the exact class name.

## Recommended End-to-End Flow

1. `WorkflowExecution` emits `ActionExecutionRequestedEvent`.
2. Action application handler upserts `ActionExecution` and calls `Execute(now)`.
3. `ActionExecution` emits `IntegrationRequested`.
4. Integration context executes under rate-limit control.
5. Integration success emits `IntegrationRequestSucceeded`.
6. `ActionExecution` consumes it and emits `ActionCompletedEvent`.
7. Workflow application handler consumes `ActionCompletedEvent` and calls `WorkflowExecution.RecordStepCompleted`.

Failure path:

1. Integration failure emits `IntegrationRequestFailed`.
2. `ActionExecution` decides stop, skip, or retry.
3. If retry: emit `ActionRetryScheduledEvent`; scheduler reissues `Execute(now)` when due.
4. If skip: workflow handler calls `WorkflowExecution.RecordStepSkipped`.
5. If fail: workflow handler calls `WorkflowExecution.RecordStepFailed`.

This preserves the intended orchestration split already implied by the requirements.

## Loop Child-Execution Flow

With a dedicated `LoopExecution` aggregate, loop completion has its own domain-owned path in addition to the action path above.

Recommended flow:

1. Parent `WorkflowExecution` reaches a loop step and emits `LoopExecutionStartedEvent`.
2. An application handler creates or loads `LoopExecution` using the parent workflow ID and parent loop `StepExecutionId`, then calls `Start`.
3. `LoopExecution` decides which child `WorkflowExecution` instances may be spawned immediately and emits `LoopIterationSpawnRequestedEvent` records.
4. The application layer materializes those child `WorkflowExecution` instances.
5. Action steps inside each child execution still use `ActionExecution` normally.
6. When a child execution reaches a terminal state, the application layer routes that result into `LoopExecution`.
7. `LoopExecution` records the iteration result, applies `IterationFailureStrategy`, decides whether more iterations may be spawned, and checks whether the loop is now terminal.
8. When `LoopExecution` becomes terminal, it should publish either `LoopCompletedEvent` or `LoopFailedEvent`.
9. The application layer handles that terminal loop event and calls the parent `WorkflowExecution` to complete or fail the loop step.

That final step should not be modeled as `ActionCompletedEvent`.

The loop step is not an action step, and there may be no `ActionExecution` aggregate for it at all. The correct completion path is:

- `LoopExecution` publishes `LoopCompletedEvent(parentWorkflowExecutionId, parentLoopStepExecutionId, aggregatedLoopOutput)`
- the application layer handles that event and calls `WorkflowExecution.RecordStepCompleted(parentLoopStepExecutionId, aggregatedLoopOutput)`
- `LoopExecution` publishes `LoopFailedEvent(parentWorkflowExecutionId, parentLoopStepExecutionId, error)`
- the application layer handles that event and calls `WorkflowExecution.RecordStepFailed(parentLoopStepExecutionId, error)`

Important boundary:

- `ActionExecution` reacts to integration outcomes for action steps.
- `LoopExecution` owns loop spawning, progression, aggregation, and iteration-failure policy.
- the application layer only routes events into aggregate commands and persists the resulting state transitions.

## Where the Coordination Code Belongs

Once `LoopExecution` exists, the coordination code splits into two layers.

Domain layer:

- `LoopExecution` owns spawning policy, concurrency decisions, aggregation rules, and iteration-failure handling.

Application layer:

- thin handlers route domain events into aggregate commands and persist the results.

Recommended home:

- a new application project such as `WorkflowAutomation.WorkflowExecution.Application`
- thin handlers or process-manager-style components such as `StartLoopExecutionHandler` and `HandleChildWorkflowTerminalStateHandler`

Why the application layer is still needed:

- it loads and saves multiple aggregate instances
- it reacts to domain events and turns them into commands on aggregates
- it performs routing, not business-policy decisions

Minimum routing responsibilities:

1. handle `LoopExecutionStartedEvent`, create or load `LoopExecution`, and call `Start`
2. handle `LoopIterationSpawnRequestedEvent` from `LoopExecution` and create child `WorkflowExecution` instances
3. handle child terminal workflow events and route them into `LoopExecution`
4. handle `LoopCompletedEvent` and `LoopFailedEvent` from `LoopExecution` and call `WorkflowExecution.RecordStepCompleted` or `WorkflowExecution.RecordStepFailed` on the parent workflow

With the current contracts, item 3 means `WorkflowCompletedEvent` and `WorkflowFailedEvent`, but only for child executions.

If generic workflow terminal events are kept, the application handler must load the completed workflow and inspect its `ParentContext` to determine whether it is a child iteration execution. If `ParentContext` is null, it should ignore that event.

The current `WorkflowCompletedEvent` and `WorkflowFailedEvent` are workable as routing inputs only if the child workflow can be reloaded and its parent correlation is strong enough. If the team wants stateless handlers or simpler event contracts, enrich the child terminal event or add loop-specific iteration terminal events.

Use one of these options:

1. keep generic workflow terminal events, but extend child correlation so handlers can load the child workflow and route deterministically
2. enrich workflow terminal events for child executions with `ParentWorkflowExecutionId`, parent `StepExecutionId`, iteration identity, and terminal output
3. add explicit loop-facing events such as `IterationCompletedEvent` and `IterationFailedEvent`

Required payload for the loop-facing event:

- child `WorkflowExecutionId`
- parent `WorkflowExecutionId`
- parent `StepExecutionId`
- parent `LoopStepId`
- iteration index or stable iteration key
- terminal status
- final iteration output when successful
- error when failed

`StepExecutionId` is the more reliable correlation key than `LoopStepId` alone, because the same loop definition step can be re-entered in a later re-execution cycle after a review-step rewind.

That also means the current `ParentExecutionContext` shape is not quite sufficient for robust loop routing. It should be extended, or the child terminal event should carry the missing correlation values.

Without reliable correlation, the parent loop cannot deterministically decide when the loop step is complete and what output array it should produce.

---

## Retry Semantics

## Meaning of `Retry(N)`

`Retry(N)` means:

- one initial attempt
- up to `N` additional retries
- fail only after the failed attempt that exhausts that retry budget

Examples:

- `Retry(0)` is invalid and already rejected by definition-side validation.
- `Retry(2)` allows at most 3 dispatches total.

## Backoff Ownership

The aggregate must own the decision that a retry is required.

The exact backoff formula can live in either place:

- inside the aggregate, if the team wants the retry policy to be fully domain-owned
- in the application layer, if the team prefers infrastructure-configurable backoff strategies

Recommendation for this repo:

- keep the aggregate responsible for terminal vs retryable decisions
- let the application layer compute `retryAtUtc`
- persist `NextRetryAtUtc` inside the aggregate

This keeps the domain model deterministic without baking scheduling algorithms into it.

---

## Timeout Semantics

Requirements state that steps can time out and that long-running workflows must remain resumable.

For `ActionExecution`, timeout should be modeled as an execution deadline, not as an infrastructure-only concern.

Minimum required rule:

- when the current time passes `DeadlineUtc` before a terminal success is recorded, the action must transition through the same policy as any other failure

How to compute `DeadlineUtc`:

1. start from the workflow-level timeout on `Workflow`
2. optionally apply an integration action timeout override if that concept exists in the integration snapshot
3. persist the effective deadline in `ActionExecution`

This avoids non-deterministic behavior when work is queued under rate limiting.

---

## Interaction with Existing WorkflowExecution Behavior

## Normal Action Step

No change to graph traversal responsibility.

`WorkflowExecution` still:

- resolves templates
- starts the step execution
- stays `Running`
- waits for an action-terminal event to be translated back into `RecordStepCompleted/Skipped/Failed`

## Parallel Branches

If an action terminal result causes `WorkflowExecution.RecordStepFailed`, the workflow aggregate already fails the workflow and cancels active step executions.

The application layer should also cancel any running `ActionExecution` instances belonging to steps that were cancelled by workflow failure, so the worker state stays consistent with the step state.

## Loop Iterations

The loop model is clearer once `LoopExecution` is explicit:

- step-level action failures are resolved inside each child iteration `WorkflowExecution` and `ActionExecution`
- `LoopExecution` owns iteration spawning, progress tracking, aggregation, and `IterationFailureStrategy`
- `WorkflowExecution` itself only keeps the parent loop `StepExecution` open until `LoopExecution` completes or fails it

So the missing reactive piece is real, but it belongs to `LoopExecution` as a domain aggregate. The application layer only routes child workflow terminal events into `LoopExecution` commands.

## Review Steps

No direct interaction is required. Review-step rewind logic remains fully inside `WorkflowExecution`.

If a rejection invalidates a running action step, the application layer should cancel the corresponding `ActionExecution` after the invalidated `StepExecution` is removed.

---

## Required Changes to the Current Code Model

## Must Change

1. Introduce `LoopExecution` as a first-class aggregate root with its own ID, status, iteration state, and repository.
2. Add loop-domain events or commands for spawn decisions, iteration completion/failure, and terminal loop outcome.
3. Extend `ParentExecutionContext` or equivalent child correlation data with the parent loop `StepExecutionId` and iteration identity.
4. Keep the application layer thin: `LoopExecutionStartedEvent` should create/load `LoopExecution`, child terminal events should route into `LoopExecution`, and terminal loop outcomes should route back into the parent `WorkflowExecution`.
5. Extend `ActionExecutionStatus` with `WaitingForRetry` and `Skipped`.
6. Stop treating `Skip` as a failed aggregate that merely emits `ActionSkippedEvent`.
7. Add an execute/dispatch method that emits the integration request event.
8. Add retry scheduling state, deadline/timeout handling, and attempt correlation to `ActionExecution`.
9. Add tests for both `ActionExecution` and `LoopExecution`.

## Should Change

1. Include `ActionExecutionId` in terminal action events.
2. Replace ambiguous `RetryCount` state with `AttemptCount`, or document the existing semantics rigorously.
3. Introduce integration-action snapshot data for hot-swap safety.
4. Add a dedicated `LoopExecutionStatus` and explicit iteration result value objects instead of scattering loop state across handlers.
5. Store timestamps for first start, last attempt, and completion.

## Can Wait

1. richer timeout taxonomy
2. full read-model projection design
3. cross-tenant observability metrics
4. integration unavailable versus ordinary failure distinctions

---

## Test Specification

Add a dedicated `ActionExecutionTests` suite.

Minimum cases:

1. `Execute_FromPending_PublishesIntegrationRequested`
2. `Execute_FromWaitingForRetry_BeforeDue_Throws`
3. `Success_CompletesAndPublishesActionCompleted`
4. `FailureStrategyStop_FirstFailure_PublishesActionFailed`
5. `FailureStrategySkip_FirstFailure_PublishesActionSkipped_AndStatusIsSkipped`
6. `FailureStrategyRetry_WithRetriesRemaining_TransitionsToWaitingForRetry`
7. `FailureStrategyRetry_Exhausted_PublishesActionFailed`
8. `Timeout_WithRetryRemaining_SchedulesRetry`
9. `Timeout_AfterDeadline_PublishesActionFailed`
10. `Cancel_FromRunning_PublishesActionCancelled`
11. `Cancel_FromTerminalState_Throws`
12. `DuplicateSuccessForOldAttempt_IsIgnoredOrRejected`
13. `DuplicateFailureForOldAttempt_IsIgnoredOrRejected`
14. `RetryEvent_PreservesAttemptNumberAndCorrelation`
15. `ActionExecution_CannotBeCreatedTwiceForSameStepExecution` at the repository/application-service level

Add a dedicated `LoopExecutionTests` suite.

Minimum cases:

1. `Start_Sequential_SpawnsExactlyOneChild`
2. `Start_ParallelMaxN_SpawnsUpToConfiguredLimit`
3. `ChildCompletion_Sequential_SpawnsNextIteration`
4. `ChildSuccess_AggregatesOutputInSourceOrder`
5. `ChildFailure_Skip_RecordsNullAndContinues`
6. `ChildFailure_StopAll_FailsLoopAndStopsFurtherSpawning`
7. `AllIterationsTerminal_CompletesLoopWithAggregatedOutput`
8. `ReenteredLoopStep_UsesNewParentStepExecutionIdAndNewLoopExecution`

Add a smaller set of workflow integration tests after wiring:

1. action success flows back into `WorkflowExecution.RecordStepCompleted`
2. retryable action remains on the same workflow step until terminal outcome
3. skip advances workflow without output
4. stop fails workflow and cancels sibling branch actions
5. loop success flows through `LoopExecution` and then completes the parent loop step
6. child loop iteration action failure bubbles into `LoopExecution` correctly
7. `LoopExecution` applies `IterationFailureStrategy.Skip` and still completes the parent loop step
8. `LoopExecution` applies `IterationFailureStrategy.StopAll` and fails the parent loop step

---

## Recommended Implementation Order

1. Introduce the `LoopExecution` aggregate boundary, status model, and event contracts.
2. Add parent/child correlation data for loop iterations and write `LoopExecutionTests`.
3. Add thin application handlers that route loop start and child terminal events into `LoopExecution`.
4. Complete the `ActionExecution` state model and event contracts.
5. Add thin application handlers that route action events between `WorkflowExecution`, `ActionExecution`, and the integration side.
6. Add end-to-end tests that prove the three-aggregate choreography.

This order makes the loop boundary explicit first, then finishes the action boundary, and only then wires the application layer around them.

---

## Final Decision Summary

The project already has the right high-level direction.

The main problem is not architectural confusion; it is that the runtime model is only partially explicit. The completion work should preserve the current split and make the missing loop boundary first-class:

- `WorkflowExecution` remains the workflow graph aggregate.
- `StepExecution` remains a lightweight entity inside it.
- `ActionExecution` becomes the durable runtime aggregate for action-step policy.
- `LoopExecution` becomes the durable runtime aggregate for loop-step policy.
- Integration/rate limiting remains outside the workflow aggregates.

If implemented this way, the design remains consistent with the current definition model, review-step model, loop semantics, and the original requirements around retries, timeouts, rate limiting, and version isolation.