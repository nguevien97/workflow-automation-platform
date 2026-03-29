# Review Step Design

## Overview

A **Review Step** is a workflow step type that allows a user to either **approve** (advance the workflow) or **reject** (rewind execution to a prior step). It is placed explicitly in the workflow graph, not invoked ad-hoc on arbitrary steps.

---

## Core Concept

When workflow execution reaches a `ReviewStep`, it pauses (stays `Running`) and emits a `ReviewStepReachedEvent`. An external actor then calls either:

- **`ApproveReviewStep(stepExecutionId)`** — completes the review step and advances to `NextStepId`.
- **`RejectReviewStep(stepExecutionId, reason)`** — invalidates steps from the configured target back to the review step, snapshots them into rejection history, and re-executes from the target.

---

## Definition Side (`WorkflowDefinition` bounded context)

### `ReviewStepDefinition`

Extends `StepDefinition` with `StepType.Review`.

| Property | Type | Description |
|---|---|---|
| `RejectionTargetStepId` | `StepId` | The step to rewind to on rejection. Must be preceding and on the same local path. |
| `MaxRejections` | `int` | Maximum times this review step can reject before the workflow fails. Default: 3. |
| `NextStepId` | `StepId?` | Inherited. The step to advance to on approval. `null` if terminal. |

### Validation Rules

Three rules enforced during `WorkflowDefinition` construction:

1. **Reachable** — Already covered by the existing `ValidateGraphShape()` cycle/reachability check. No new code needed.

2. **Target must be preceding and on the same local path** — The rejection target must be a step that was already visited on the *current local `NextStepId` chain*, not merely in the `guaranteedAvailable` set. This prevents a review step inside a parallel branch from targeting a step in the parent scope, which would break owner-barrier semantics.

   Implementation: `ValidateLocalPath` tracks a separate `localPathSteps` set (in addition to the existing `availableAfterPath`). `ValidateReviewTarget` checks against `localPathSteps`.

3. **No immediate consecutive review steps** — A review step's `NextStepId` must not point directly to another review step. At least one non-review step must separate them. However, multiple review steps on the same local path with intervening steps (e.g., `Action → Review A → Action → Review B`) are valid. A `lastWasReview` flag in `ValidateLocalPath` enforces this.

### `StepType` Enum Change

Add `Review` to the existing enum:

```csharp
public enum StepType { Trigger, Action, Condition, Parallel, Loop, Review }
```

---

## Execution Side (`WorkflowExecution` bounded context)

### `ReviewStepInfo`

Execution-side mirror of the definition. Stored inside `WorkflowDefinitionSnapshot`.

| Property | Type |
|---|---|
| `RejectionTargetStepId` | `StepId` |
| `MaxRejections` | `int` |

### `ExecuteStep` Behavior

When execution reaches a review step:

1. Create a new `StepExecution` with status `Running`.
2. Emit `ReviewStepReachedEvent(WorkflowExecutionId, StepExecutionId, StepId)`.
3. Do **not** advance. The step stays `Running` as a user-facing gate.

### `ApproveReviewStep(StepExecutionId)`

1. Validate the step is a `ReviewStepInfo` and is `Running`.
2. Complete the step execution (no output).
3. Call `AdvanceOrComplete` to move to `NextStepId`.

### `RejectReviewStep(StepExecutionId, string reason)`

1. Validate the step is a `ReviewStepInfo` and is `Running`.
2. Find the local `NextStepId` path containing both the rejection step and its target.
3. Collect all step IDs from target to rejection step (inclusive) on that path.
4. Expand to include nested scope steps (steps inside Condition/Parallel/Loop bodies owned by any step in the invalidation range).
5. Snapshot invalidated step executions into `RejectionRecord` (preserving `Input`, `Output`).
6. Add record to `_rejectionHistory` and remove invalidated step executions.
7. **Mark superseded records:** For any other review step whose prior `RejectionRecord` entries fall within the invalidation range, set their `SupersededByReviewStepId` to the current review step's ID.
8. **Check max rejections** (count *non-superseded* records where `ReviewStepId` matches). If exceeded, call `FailWorkflow`. Otherwise, re-execute from target step.
9. Emit `ReviewStepRejectedEvent(WorkflowExecutionId, ReviewStepId, TargetStepId, Reason)`.

> **Important:** The max-rejections check must happen *after* recording the rejection in history. Checking before recording causes an off-by-one error (the count is always one behind).

### `RejectionRecord` (Value Object)

Stored in `WorkflowExecution._rejectionHistory`.

| Property | Type | Description |
|---|---|---|
| `ReviewStepId` | `StepId` | The review step that was rejected. |
| `TargetStepId` | `StepId` | The step execution rewound to. |
| `Reason` | `string` | User-provided rejection reason. |
| `OccurredOn` | `DateTime` | Timestamp (UTC). |
| `InvalidatedSteps` | `List<InvalidatedStepExecution>` | Snapshots of wiped step executions. |
| `SupersededByReviewStepId` | `StepId?` | If a downstream review step later rejects across this step's range, this record is marked superseded. `null` means active. |

`InvalidatedStepExecution`: `StepExecutionId`, `StepId`, `Input`, `Output`.

### Events

| Event | Fields | When |
|---|---|---|
| `ReviewStepReachedEvent` | `WorkflowExecutionId`, `StepExecutionId`, `StepId` | Execution reaches a review step. |
| `ReviewStepRejectedEvent` | `WorkflowExecutionId`, `ReviewStepId`, `TargetStepId`, `Reason` | A rejection is performed. |

---

## Invalidation Mechanics

When rejecting, the execution must invalidate the correct range of steps:

```
Local path: ... → [Target] → X → Y → [ReviewStep] → ...
                   ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                   All of these are invalidated (inclusive)
```

If any step in the invalidation range owns a nested scope (e.g., a Condition or Parallel step), all steps inside that scope are also invalidated. This uses a recursive `CollectScopeSteps` helper.

After invalidation, `ExecuteStep(targetStepId)` re-creates fresh step executions starting from the target.

---

## Rejection Counter Reset on Downstream Rejection

### Problem

When multiple review steps exist on the same path, a downstream rejection can rewind past an upstream review step. Should the upstream step's rejection counter carry over from the previous cycle?

### Decision: Reset (supersede prior records)

When Review B rejects back to a target that precedes Review A, A's prior rejection records are **superseded** — they no longer count toward A's max-rejections limit.

**Rationale:**

1. **The work is different.** B rejected for its own reasons. When Group 1 re-executes and produces new output, A is reviewing *fresh* work unrelated to its previous attempts. Counting old rejections against new work is arbitrary.

2. **Real-world parallel.** A purchase order: Finance Review (A) rejects twice for budget formatting, then approves. Director Review (B) sends it back because the vendor changed. Finance is now reviewing a completely different PO — their old rejection count is irrelevant.

3. **The counter's purpose is preventing infinite loops at a single gate**, not tracking lifetime rejection karma across the workflow.

### Mechanism

`RejectionRecord` has a `SupersededByReviewStepId` field (nullable). When B rejects and its invalidation range includes A:

1. All of A's existing `RejectionRecord` entries are marked with `SupersededByReviewStepId = B's StepId`.
2. The max-rejections count query filters to `r.SupersededByReviewStepId == null`.
3. Full history is preserved for audit — nothing is deleted.

### Example

```
Group 1 → [Review A (max: 3)] → Group 2 → [Review B (target: Group 1)] → Group 3
```

| Step | A active count | A superseded count | Outcome |
|---|---|---|---|
| A rejects (1st) | 1 | 0 | Rewind to Group 1 |
| A rejects (2nd) | 2 | 0 | Rewind to Group 1 |
| A approves | 2 | 0 | Advance to Group 2 |
| B rejects → back to Group 1 | 0 | 2 (superseded by B) | A starts fresh cycle |
| A rejects (1st in new cycle) | 1 | 2 | Rewind to Group 1 |
| A rejects (2nd in new cycle) | 2 | 2 | Rewind to Group 1 |
| A rejects (3rd in new cycle) | 3 | 2 | **Workflow fails** (3 >= max) |

---

## Review Steps Inside Loop Bodies

### Problem

A loop body shares a single `StepId` for the review step definition across all iterations. Each iteration spawns its own `WorkflowExecution` (the loop iteration execution). If rejection records are keyed only by `ReviewStepId`, counts leak across iterations — iteration 2 inherits iteration 1's rejection count.

### Decision: Rejection counts are scoped per `WorkflowExecutionId`

Each loop iteration runs as its own `WorkflowExecution` with a distinct `WorkflowExecutionId`. The `_rejectionHistory` list lives on the `WorkflowExecution` instance, so each iteration naturally gets its own rejection history. No additional scoping field is needed — the isolation is structural.

| Iteration | WorkflowExecutionId | Review rejection count | Independent? |
|---|---|---|---|
| Iteration 1 | `exec-aaa` | 0 → 1 → 2 (fails or approves) | Yes |
| Iteration 2 | `exec-bbb` | 0 (fresh) | Yes |
| Iteration 3 | `exec-ccc` | 0 (fresh) | Yes |

---

## Helper Methods Added to `WorkflowExecution`

- **`FindLocalPathContaining(StepId)`** — Searches all entry points (top-level, parallel branches, condition branches, loop bodies) for the local `NextStepId` chain containing the given step.
- **`CollectScopeSteps(StepInfo, HashSet<StepId>)`** — Recursively collects all steps owned by a control-flow step (Parallel branches, Condition branches, Loop body).
- **`CollectLocalPathAndNestedScopes(StepId, HashSet<StepId>)`** — Walks a local path and collects all steps including nested scopes.

---

## Helper Methods Added to `WorkflowDefinitionSnapshot`

- **`GetLocalPath(StepId entryStepId)`** — Returns the list of step IDs on the local `NextStepId` chain starting from the given entry.
- **`LocalPathContainsStep(StepId entryStepId, StepId targetStepId)`** — Checks if a step exists on the local path (no deep recursion into nested scopes). Used by `FindOwningParallelStep` and `FindOwningConditionStep` to find the *innermost* owner.
- **`AllSteps`** — Public property exposing the full step dictionary as read-only.

---

## Interaction with Owner-Barrier Model

The review step respects the owner-barrier model:

- A review step inside a parallel branch can only target steps within the same branch (same local path).
- It cannot target steps in the parent scope or in other branches.
- **If a review step inside a parallel branch rejects, the entire workflow execution is cancelled** — not just the branch. The rationale: a review rejection signals that the work product is unacceptable, and partial execution of other branches with invalidated context is unsafe.
- The `lastWasReview` flag is scoped per `ValidateLocalPath` call, so separate branches/bodies each allow their own review step.

---

## Test Scenarios Explored

### Execution Tests (11 tests)

| Test | Description |
|---|---|
| `ReviewStep_Approve_AdvancesToNextStep` | T → A → Review → B. Approve advances to B. |
| `ReviewStep_Reject_RewindsToTarget` | T → A → Review(target: A). Reject invalidates A, re-executes from A. |
| `ReviewStep_RejectBackMultipleSteps_InvalidatesRange` | T → A → B → Review(target: A). Reject invalidates A, B, and the review step. |
| `ReviewStep_ExceedsMaxRejections_FailsWorkflow` | maxRejections=2. After 2 rejection cycles, workflow status becomes Failed. |
| `ReviewStep_DownstreamRejection_ResetsUpstreamCounter` | A rejects twice, approves, B rejects past A. A's counter resets; A can reject again without hitting max. |
| `ReviewStep_RejectionHistory_Preserved` | History records contain input/output snapshots of invalidated steps. |
| `ReviewStep_RejectNonReviewStep_Throws` | Calling RejectReviewStep on an Action step throws. |
| `ApproveReviewStep_OnActionStep_Throws` | Calling ApproveReviewStep on an Action step throws. |
| `ReviewStep_BackToConditionOnSamePath_ReEvaluates` | T → Cond(→ A → Review(target: A)) → B. Rejection correctly re-executes within condition branch. |
| `ReviewStep_InsideParallelBranch_RejectCancelsWorkflow` | Parallel(Branch1: A → Review, Branch2: B). Review rejects → entire workflow is cancelled. |
| `ReviewStep_InsideLoop_EachIterationHasOwnCounter` | Loop with review in body. Iteration 1 rejects twice and approves. Iteration 2 starts with count=0. |

### Definition Validation Tests (9 tests)

| Test | Description |
|---|---|
| `Valid_Review_TargetOnSameLocalPath` | T → A → Review(target: A) → B. Valid. |
| `Valid_Review_TerminalStep` | T → A → Review(target: A). Review as last step. Valid. |
| `Valid_Review_InsideParallelBranch` | Review inside a parallel branch targeting a step in the same branch. Valid. |
| `Valid_Review_InsideConditionBranch` | Review inside a condition branch targeting a step in the same branch. Valid. |
| `Valid_Review_DifferentBranches` | Separate review steps on different parallel branches. Valid. |
| `Valid_Review_MultipleReviewsWithIntervening` | T → A → Review(target: A) → B → Review(target: B) → C. Valid (non-review step between). |
| `Invalid_Review_TargetIsTrigger_NotOnLocalPath` | Review targets the trigger step. Invalid. |
| `Invalid_Review_TargetsParentScopeStep` | Review inside parallel branch targets parent scope step. Invalid. |
| `Invalid_Review_TwoReviewsOnSameLocalPath` | Two review steps immediately adjacent (Review → Review with no step between). Invalid. |

---

## Open Questions

- Should review steps have an output schema (e.g., to capture reviewer comments)?
- Should the Application Layer snapshot mapper be implemented alongside (currently deferred)?
- Should rejection history be queryable as a first-class read model?
- Should there be a timeout on review steps (auto-approve/auto-reject after N hours)?
