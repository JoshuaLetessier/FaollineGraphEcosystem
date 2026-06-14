# Tasks: Quest library (com.faolline.graphquest) — v1

**Feature**: `029-graph-quest` | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

**Approach**: TDD is mandatory (constitution Principle IV + spec). For every user story, the test task(s) are
written and confirmed **failing for the right reason** before the implementation tasks of that story. EditMode only.

**Conventions**: all paths are under `com.faolline.graphquest/`. One class per file. `[GraphQuest]` log prefix.
`Action<T>` (no UnityEvent). No `MonoBehaviour` in Runtime. XML docs on every public type.

**Package root**: `com.faolline.graphquest/`
**Namespaces**: `Faolline.GraphQuest` (runtime), `Faolline.GraphQuest.Tests` (tests).

---

## Phase 1: Setup (package scaffolding)

- [ ] T001 Create `com.faolline.graphquest/package.json` — name `com.faolline.graphquest`, version `0.1.0`, displayName "Faolline GraphQuest", description (quest/objective domain lib above graphcore + graphstandard), unity `6000.0`, dependencies `com.faolline.graphcore` `0.14.0` and `com.faolline.graphstandard` `0.10.1`.
- [ ] T002 Create `com.faolline.graphquest/Runtime/com.faolline.graphquest.Runtime.asmdef` — name `com.faolline.graphquest.Runtime`, references `com.faolline.graphcore.Runtime` and `com.faolline.graphstandard.Runtime`, no Editor-only platform restriction, `autoReferenced: true`.
- [ ] T003 Create `com.faolline.graphquest/Tests/EditMode/com.faolline.graphquest.Tests.EditMode.asmdef` — references `com.faolline.graphquest.Runtime`, `com.faolline.graphcore.Runtime`, `com.faolline.graphstandard.Runtime`, `com.faolline.graphsave.Runtime` (for the round-trip test only), `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `includePlatforms: ["Editor"]`; `precompiledReferences: ["nunit.framework.dll"]`, `overrideReferences: true`, `autoReferenced: false`.
- [ ] T004 [P] Create `com.faolline.graphquest/README.md` and `com.faolline.graphquest/CHANGELOG.md` skeletons (Keep-a-Changelog header; `[0.1.0]` section to be filled in Polish).

**Checkpoint**: the empty package compiles and resolves its two dependencies (manage_packages / Unity import clean).

---

## Phase 2: Foundational (blocking prerequisites — required by ALL user stories)

These are the shared data types and typed-context companions every story builds on. No story-specific behavior yet.

- [ ] T005 [P] Create `com.faolline.graphquest/Runtime/Model/QuestState.cs` — `public enum QuestState { Locked, Active, Completed, Failed }` (used for both quests and objectives), with XML docs per value.
- [ ] T006 [P] Create `com.faolline.graphquest/Runtime/Model/QuestCompletionRule.cs` — `public enum QuestCompletionRule { AllRequired }` (v1 single value; reserves room for more without an API break).
- [ ] T007 [P] Create `com.faolline.graphquest/Runtime/Context/QuestContextKeys.cs` — `public static class QuestContextKeys` with `const string Completed = "quest_completed"`, `Failed = "quest_failed"`, `Rewarded = "quest_rewarded"`. The only place these literals exist (Principle VI).
- [ ] T008 Create `com.faolline.graphquest/Runtime/Model/ObjectiveNodeData.cs` — `public sealed class ObjectiveNodeData : Faolline.GraphCore.BaseNodeData` with `const string NodeTypeId = "graphquest.objective"`, and serialized fields/properties `CompletionCondition` (`BaseCondition`), `FailCondition` (`BaseCondition`), `Required` (`bool`, default `true`), `Reward` (`BaseAction`). Append-only on the subclass; graphcore untouched. Depends on nothing but graphcore.
- [ ] T009 Create `com.faolline.graphquest/Runtime/Model/QuestGraph.cs` — `public sealed class QuestGraph : Faolline.GraphCore.BaseGraph` with `UnlockCondition` (`BaseCondition`), `CompletionReward` (`BaseAction`), `CompletionRule` (`QuestCompletionRule`, default `AllRequired`). Depends on T006.
- [ ] T010 Create `com.faolline.graphquest/Runtime/Context/QuestContext.cs` — `public sealed class QuestContext : Faolline.GraphCore.BaseContext` overriding `CreateCloneInstance()` ⇒ `new QuestContext()` (Principle VI — required for GoBack/history restore), plus `bool IsCompleted(string id)` / `bool IsFailed(string id)` routing through `QuestContextKeys`. Depends on T007.

**Checkpoint**: model + context types compile; no behavior yet. (Optional: a trivial EditMode test that `QuestContext.CreateCloneInstance()` returns a `QuestContext` — guards the Principle-VI footgun.)

---

## Phase 3: User Story 1 — Declare a quest and read its progress from the context (Priority: P1) 🎯 MVP

**Goal**: Author a flat quest (objectives with completion/fail conditions, no prerequisites) and derive
`Locked`/`Active`/`Completed`/`Failed` per objective and the aggregated quest state from a seeded `BaseContext`,
deterministically.

**Independent Test**: build a 2–3 objective quest, seed a context so some completion conditions hold, evaluate, and
assert each objective and the quest report the expected state; re-evaluate unchanged → identical states.

### Tests (write first, confirm failing)

- [ ] T011 [P] [US1] Create `com.faolline.graphquest/Tests/EditMode/QuestStateDerivationTests.cs` — failing tests: an objective whose completion condition is unmet is `Active`; once the context satisfies it (and after `Evaluate()`) it is `Completed`; an objective whose fail condition holds is `Failed`; **fail precedes complete** when both hold; a quest with all required objectives completed is `Completed`, with a required objective failed is `Failed`, otherwise `Active`; re-`Evaluate()` with an unchanged context yields identical states and raises no duplicate change events (determinism / SC-002).
- [ ] T012 [P] [US1] Create `com.faolline.graphquest/Tests/EditMode/QuestBuilderFlatTests.cs` — failing tests: `QuestBuilder.Create(id).AddObjective(...).CompleteWhen(...).FailWhen(...).Optional().Build()` produces a `QuestGraph` whose nodes are `ObjectiveNodeData` carrying the declared conditions/flags; an optional objective does not block quest completion; a quest built with zero objectives is rejected with a `[GraphQuest]` diagnostic.

### Implementation

- [ ] T013 [US1] Create `com.faolline.graphquest/Runtime/Builder/QuestBuilder.cs` and `ObjectiveBuilder` (one class per file → also `com.faolline.graphquest/Runtime/Builder/ObjectiveBuilder.cs`): fluent `Create`/`AddObjective`/`CompleteWhen`/`FailWhen`/`Optional`/`Build`. `Build()` instantiates a `QuestGraph` (`ScriptableObject.CreateInstance`), adds an `ObjectiveNodeData` per objective, and rejects an empty quest. (Prerequisites/rewards added in later stories.)
- [ ] T014 [US1] Create `com.faolline.graphquest/Runtime/QuestEvaluator.cs` — ctor `(QuestGraph, BaseContext)` builds an inner `Faolline.GraphStandard.ReactiveEvaluator(quest, context, QuestContextKeys.Completed)`; `Evaluate()` performs one pass: for each `Available` objective, if its `FailCondition` holds → record into the `Failed` set and report `Failed`; else if its `CompletionCondition` holds → `ReactiveEvaluator.MarkCompleted(id)`; `GetObjectiveState(id)` maps `ReactiveNodeState`→`QuestState` and overlays `Failed`; `State` aggregates per `QuestCompletionRule.AllRequired`.
- [ ] T015 [US1] Add change events to `QuestEvaluator`: `event Action<string,QuestState> OnObjectiveStateChanged` and `event Action<QuestState> OnQuestStateChanged`, raised only on an actual transition during `Evaluate()` (no duplicate emissions — wire off the inner evaluator's `OnNodeAvailable/Completed/Locked` plus the Failed overlay).
- [ ] T016 [US1] Run `QuestStateDerivationTests` + `QuestBuilderFlatTests` green; fix until all pass. Confirm no `[GraphQuest]` console errors.

**Checkpoint**: a flat quest is fully authorable in code and its states derive correctly from a context — MVP usable.

---

## Phase 4: User Story 2 — Gate objectives and quests with prerequisites (Priority: P2)

**Goal**: Prerequisites (linear chains AND DAGs) keep objectives/quests `Locked` until satisfied; cyclic
prerequisites are rejected at build time; a quest unlock condition gates the whole quest.

**Independent Test**: `B.Requires("A")` → `B` `Locked` until `A` is `Completed`, then `Active`; a diamond
(`D.Requires("B","C")`, both require `A`) gates `D` until both complete; a cyclic topology is rejected by `Build()`.

### Tests (write first, confirm failing)

- [ ] T017 [P] [US2] Create `com.faolline.graphquest/Tests/EditMode/QuestPrerequisiteGatingTests.cs` — failing tests: chain gating (`B` Locked until `A` Completed, then Active); diamond DAG (`D` Locked until both `B` and `C` Completed); a quest with an unmet `UnlockCondition` is `Locked` and surfaces no `Active` objectives; completing prerequisites in the context then `Evaluate()` cascades unlocks (SC-003).
- [ ] T018 [P] [US2] Create `com.faolline.graphquest/Tests/EditMode/QuestCycleRejectionTests.cs` — failing test: a `QuestBuilder` whose `Requires` edges form a cycle is rejected by `Build()` with a `[GraphQuest]` diagnostic naming the cycle (SC-007). (Use `LogAssert`/exception assertion.)

### Implementation

- [ ] T019 [US2] Extend `ObjectiveBuilder` with `Requires(params string[] prerequisiteObjectiveIds)` — records prerequisite ids; `QuestBuilder.Build()` adds a `BaseEdgeData` `From→To` per prerequisite (From = prerequisite, To = the objective) so `ReactiveEvaluator` reads them as gates.
- [ ] T020 [US2] Add acyclicity validation in `QuestBuilder.Build()` (DFS over the prerequisite edges; reuse graphcore's cycle-detection approach if directly applicable) — throw with a `[GraphQuest]` cycle-naming message before any `QuestGraph`/evaluator is returned.
- [ ] T021 [US2] Apply the quest `UnlockCondition` in `QuestEvaluator`: while the unlock condition is non-null and unmet, `State` is `Locked` and `GetObjectiveState` reports `Locked` for all objectives (do not surface `Active`); once met, normal derivation resumes.
- [ ] T022 [US2] Run `QuestPrerequisiteGatingTests` + `QuestCycleRejectionTests` green; fix until all pass.

**Checkpoint**: staged (chain) and reactive (DAG) progression both work from the one model; cycles can't be built.

---

## Phase 5: User Story 3 — Fire reward hooks exactly once on completion (Priority: P3)

**Goal**: An objective/quest reward (`BaseAction`) executes exactly once on the transition into `Completed`, never
again on re-evaluation.

**Independent Test**: attach a counting `BaseAction`; it stays 0 while incomplete, 1 after completion, and 1 across
repeated `Evaluate()` calls.

### Tests (write first, confirm failing)

- [ ] T023 [P] [US3] Create `com.faolline.graphquest/Tests/EditMode/QuestRewardHookTests.cs` — failing tests: an objective reward fires exactly once on `Active→Completed` and not again on repeated `Evaluate()`; a quest `CompletionReward` fires once when the last required objective completes; `OnRewardFired` is raised once with the rewarded id; an objective with no reward completes without error.

### Implementation

- [ ] T024 [US3] Extend `ObjectiveBuilder.RewardWith(BaseAction)` and `QuestBuilder.RewardQuestWith(BaseAction)` — store on `ObjectiveNodeData.Reward` / `QuestGraph.CompletionReward`.
- [ ] T025 [US3] In `QuestEvaluator`, fire rewards guarded by the `QuestContextKeys.Rewarded` set: when an entity (objective or quest) enters `Completed`, if its id is absent from the rewarded set, `Reward.Execute(context)`, add the id to the set, and raise `event Action<string> OnRewardFired`. Skip if already present (one-shot across re-eval + restore).
- [ ] T026 [US3] Run `QuestRewardHookTests` green; fix until all pass.

**Checkpoint**: rewards are a reliable one-shot seam; the consumer supplies the effect.

---

## Phase 6: User Story 4 — Persist and restore quest progress (Priority: P4)

**Goal**: Quest/objective progress (incl. already-fired rewards) round-trips through graphsave with no quest-specific
snapshot type and no runtime graphsave dependency.

**Independent Test**: evaluate to a partial state with one reward fired, `GraphRunSnapshot.Capture` the context,
`ApplyTo` a fresh context, re-evaluate → states match and the fired reward does not re-fire.

### Tests (write first, confirm failing)

- [ ] T027 [P] [US4] Create `com.faolline.graphquest/Tests/EditMode/QuestPersistenceTests.cs` — failing tests: capture+restore via `Faolline.GraphSave.GraphRunSnapshot` reproduces every objective/quest state (completed + failed) after a fresh `Evaluate()`; a reward that fired before capture does not fire again after restore (rewarded-set persisted); progress derivable from the context alone recomputes correctly. (Test asmdef already references graphsave — T003.)

### Implementation

- [ ] T028 [US4] Verify no runtime code change is needed (all state already in context collections); if a gap surfaces (e.g. a collection not captured), add the **minimal** quest-side helper rather than touching graphsave/graphcore, and document why in the test. Make `QuestPersistenceTests` green.

**Checkpoint**: save/load works for free via the context snapshot; rewards never double-grant.

---

## Phase 7: User Story 5 — Drive quests from a host's shared context (Priority: P5)

**Goal**: A `QuestEvaluator` runs against any `BaseContext` (e.g. a host's `GameFlowContext`) and the quest core
references neither gameflow nor graphsave at runtime.

**Independent Test**: evaluate a quest against an externally-owned `BaseContext` that a stand-in host mutates;
states track it. Assert the Runtime asmdef declares no gameflow/graphsave reference.

### Tests (write first, confirm failing)

- [ ] T029 [P] [US5] Create `com.faolline.graphquest/Tests/EditMode/QuestHostContextTests.cs` — failing tests: a quest evaluated against a plain `BaseContext` (not `QuestContext`) updated by external code reflects the changes after `Evaluate()`; the runtime assembly's references do not include `com.faolline.graphgameflow` or `com.faolline.graphsave` (read `com.faolline.graphquest.Runtime.asmdef` and assert).

### Implementation

- [ ] T030 [US5] Confirm `QuestEvaluator` already accepts any `BaseContext` and uses only `QuestContextKeys` collection access (no `QuestContext`-typed requirement); adjust if a `QuestContext` cast leaked in. Make `QuestHostContextTests` green.

**Checkpoint**: quests participate in a running host's blackboard with zero host coupling.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T031 [P] Fill `com.faolline.graphquest/README.md` (overview, the two quest shapes under one model, the code-first quickstart, boundaries: no editor/UI in v1) and `CHANGELOG.md` `[0.1.0]` (initial release; deps; reuses graphstandard ReactiveEvaluator; state in context collections; persistence via graphsave snapshot).
- [ ] T032 [P] Register `com.faolline.graphquest` in the module selector whitelist `com.faolline.graphcore/Editor/Resources/.../GraphEcosystemModules.json` as an optional installable lib (like graphsave/gameflow), with the `?path=/com.faolline.graphquest#master` git URL convention (basePath `""`).
- [ ] T033 [P] XML-doc pass: every public type/member has a `<summary>`; `[GraphQuest]` prefix on all `Debug.Log*`; confirm no `MonoBehaviour`/`UnityEvent` in Runtime; one class per file.
- [ ] T034 Author a small sample in `quickstart.md` form as an EditMode integration test `com.faolline.graphquest/Tests/EditMode/QuestQuickstartSampleTests.cs` — the "rescue" quest from quickstart (chain + optional + DAG-free) authored and walked end-to-end (mirrors the dogfood "one genuine end-to-end test" practice).
- [ ] T035 Full EditMode cert: run the complete suite (graphquest + the existing ecosystem suites) — all green, zero console errors; confirm graphcore/graphstandard untouched (their suites unchanged). Bump nothing else; new package only.

---

## Dependencies & Story Completion Order

- **Setup (P1tasks T001–T004)** → **Foundational (T005–T010)** block everything.
- **US1 (T011–T016)** is the MVP and underpins all later stories (the evaluator + builder originate here).
- **US2 (T017–T022)** depends on US1 (extends the builder + evaluator).
- **US3 (T023–T026)** depends on US1 (rewards fire off the completion path); independent of US2.
- **US4 (T027–T028)** depends on US1 (needs state to persist); independent of US2/US3 (though richer with them).
- **US5 (T029–T030)** depends on US1 (evaluator); independent of US2–US4.
- **Polish (T031–T035)** last.

Story order by priority: US1 → US2 → US3 → US4 → US5. US3/US4/US5 are mutually independent once US1 exists.

## Parallel Execution Examples

- **Setup**: T004 (README/CHANGELOG) ∥ T002/T003 (asmdefs) after T001.
- **Foundational**: T005 ∥ T006 ∥ T007 (enums/keys, different files), then T008/T009/T010.
- **Per story, tests are [P]** with each other (different files): e.g. T011 ∥ T012; T017 ∥ T018; implementation
  tasks within a story are sequential (same `QuestEvaluator.cs` / `QuestBuilder.cs`).
- **Across independent stories** (once US1 is done): US3, US4, US5 test files (T023, T027, T029) can be written in
  parallel.

## Implementation Strategy

**MVP = Phase 1 + Phase 2 + US1 (Phase 3).** That delivers an authorable, context-derived flat quest — demoable on
its own. Then layer US2 (gating, the unifier of the two quest shapes), US3 (rewards), US4 (save), US5 (host seam),
and Polish. Each story is a complete, independently testable increment; commit per story after its checkpoint goes
green (TDD: failing tests first each time).
