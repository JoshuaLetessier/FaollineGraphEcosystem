# Phase 1 Data Model: Quest library (com.faolline.graphquest)

All types live in namespace `Faolline.GraphQuest`. Domain state is stored in three graphcore string-set collections
on the shared `BaseContext`, keyed by `QuestContextKeys`.

## Enums

### `QuestState` (one enum for quests AND objectives)

| Value | Meaning |
|-------|---------|
| `Locked` | Prerequisites unmet (or the owning quest is `Locked`). |
| `Active` | Unlocked and in progress (maps to `ReactiveNodeState.Available`). |
| `Completed` | Completion condition has held and the id is recorded in the completed-set. |
| `Failed` | Fail condition has held and the id is recorded in the failed-set. |

## Entities

### `ObjectiveNodeData : Faolline.GraphCore.BaseNodeData`

A single goal within a quest; one node of a `QuestGraph`. Append-only serialized fields on the subclass (graphcore's
`BaseNodeData` is untouched).

| Field | Type | Notes |
|-------|------|-------|
| (inherited `Id`, `Title`, `Position`, …) | — | from `BaseNodeData`. |
| `NodeTypeId` | `const string` = `"graphquest.objective"` | dev-standard const id. |
| `CompletionCondition` | `BaseCondition` | when it holds against the context, the objective is recorded completed. Null ⇒ never auto-completes (authoring warning). |
| `FailCondition` | `BaseCondition` (optional) | when it holds, the objective is recorded failed. Checked before completion (fail > complete). |
| `Required` | `bool` (default `true`) | a required objective gates/decides its quest's completion; an optional one tracks state + rewards but does not block the quest. |
| `Reward` | `BaseAction` (optional) | executed once when the objective enters `Completed`. |

**Relationships**: belongs to one `QuestGraph`; prerequisites to other objectives are `BaseEdgeData` edges in that
graph (`From→To` = "To requires From"), consumed by `ReactiveEvaluator`.

### `QuestGraph : Faolline.GraphCore.BaseGraph`

One quest. Its nodes are `ObjectiveNodeData`; its edges are the prerequisite DAG.

| Field | Type | Notes |
|-------|------|-------|
| (inherited `Nodes`, `Edges`, `GraphId`, …) | — | from `BaseGraph`. |
| `UnlockCondition` | `BaseCondition` (optional) | when null the quest is unlockable immediately; otherwise the quest is `Locked` until it holds. |
| `CompletionReward` | `BaseAction` (optional) | executed once when the quest enters `Completed`. |
| `CompletionRule` | enum `QuestCompletionRule { AllRequired }` (v1: only `AllRequired`) | quest completes when all `Required` objectives are `Completed`. Enum leaves room for `AnyRequired`/threshold later without an API break. |

**Validation**: the objective edges MUST be acyclic — `QuestBuilder`/`QuestGraph` rejects a cycle at build time
(FR-006). A quest with no objectives is flagged at build time.

### `QuestContextKeys` (static)

The only place the collection-key string literals exist (Principle VI).

| Const | Default value | Holds |
|-------|---------------|-------|
| `Completed` | `"quest_completed"` | ids of completed objectives/quests. Also the `ReactiveEvaluator` completed-set. |
| `Failed` | `"quest_failed"` | ids of failed objectives/quests. |
| `Rewarded` | `"quest_rewarded"` | ids whose reward already fired (one-shot guard). |

> Keys are scoped per evaluator if a game runs several quests against one context (e.g. prefix by `GraphId`); the
> exact scoping (shared vs per-quest collections) is a tasks-phase detail, but the literals stay in this class.

### `QuestContext : Faolline.GraphCore.BaseContext`

Typed subclass for standalone use (a host may instead pass its own `BaseContext`). Overrides
`CreateCloneInstance()` ⇒ `new QuestContext()` (Principle VI — otherwise GoBack/history restore silently breaks).
Exposes typed helpers over the three collections (e.g. `IsCompleted(id)`), routing through `QuestContextKeys`.

### `QuestEvaluator`

The runtime engine. Wraps a `ReactiveEvaluator` and applies the condition/fail/reward overlay.

| Member | Shape | Notes |
|--------|-------|-------|
| ctor | `QuestEvaluator(QuestGraph quest, BaseContext context)` | builds the inner `ReactiveEvaluator(quest, context, QuestContextKeys.Completed)`. |
| `Evaluate()` | `void` | one pass: per `Available` objective, check fail→record failed; else check completion→`MarkCompleted`; fire one-shot rewards on completed transitions; raise change events. |
| `GetObjectiveState(string id)` | `QuestState` | Locked/Active/Completed/Failed. |
| `QuestState` | `QuestState` (property) | aggregated quest state (Locked if unlock unmet; Failed if a required objective failed; Completed if all required completed; else Active). |
| `OnObjectiveStateChanged` | `event Action<string, QuestState>` | id + new state. |
| `OnQuestStateChanged` | `event Action<QuestState>` | quest aggregate changed. |
| `OnRewardFired` | `event Action<string>` | id whose reward just fired (diagnostics/UX). |

**Notes**: the evaluator subscribes to the inner `ReactiveEvaluator`'s `OnNodeAvailable/Completed/Locked` to map
`ReactiveNodeState` → `QuestState` and to drive reward firing. It does NOT auto-watch arbitrary context mutations;
the host calls `Evaluate()` after it changes the context (same contract as `ReactiveEvaluator.MarkCompleted`).

### `QuestBuilder` (fluent, code-first)

Produces a validated `QuestGraph`.

| Member (illustrative) | Returns | Notes |
|-----------------------|---------|-------|
| `QuestBuilder.Create(string questId)` | `QuestBuilder` | start. |
| `.UnlockWhen(BaseCondition)` | `QuestBuilder` | quest-level gate. |
| `.AddObjective(string id)` | objective sub-builder | declare an objective. |
| `…​.CompleteWhen(BaseCondition)` | objective sub-builder | completion rule. |
| `…​.FailWhen(BaseCondition)` | objective sub-builder | optional fail rule. |
| `…​.Requires(params string[] objectiveIds)` | objective sub-builder | prerequisites (chain via single id; DAG via several). |
| `…​.Optional()` | objective sub-builder | mark non-required. |
| `…​.RewardWith(BaseAction)` | objective sub-builder | objective reward. |
| `.RewardQuestWith(BaseAction)` | `QuestBuilder` | quest completion reward. |
| `.Build()` | `QuestGraph` | validates acyclicity + non-empty; throws/logs `[GraphQuest]` on a cycle. |

## State Transitions

Per objective (derived each `Evaluate()` pass from the context):

```
            prereqs unmet
   ┌────────────────────────────┐
   ▼                            │
 Locked ──prereqs met──▶ Active ──completion cond holds──▶ Completed
   ▲                       │
   │ (context reverted/    │ fail cond holds (checked first)
   │  step-back: set       ▼
   │  shrinks)           Failed
   └───────────────────────┘
```

- `Active → Completed`: completion condition holds → id recorded in completed-set → cascades to dependents
  (re-lock/unlock via the engine). Reward fires once (guard: rewarded-set).
- `Active → Failed`: fail condition holds (precedence over completion) → id recorded in failed-set.
- `Completed/Active → Locked`: only via a **context revert** (history/save restore to a smaller completed-set) —
  re-derivation is idempotent; already-fired rewards stay fired (rewarded-set is not auto-cleared).
- Quest aggregate: `Locked` (unlock unmet) → `Active` → `Completed` (all required completed) / `Failed` (any
  required failed).

## Persistence

No quest-specific snapshot type. The completed/failed/rewarded collections are part of the `BaseContext`, which
`graphsave.GraphRunSnapshot` already captures and restores. After restore, a single `Evaluate()` re-derives all
states; the rewarded-set prevents reward re-fire. (FR-012.)
