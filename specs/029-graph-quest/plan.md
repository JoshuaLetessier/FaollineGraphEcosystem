# Implementation Plan: Quest library (com.faolline.graphquest) — v1

**Branch**: `029-graph-quest` | **Date**: 2026-06-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/029-graph-quest/spec.md`

## Summary

A new domain library `com.faolline.graphquest` above graphcore + graphstandard that models **quests** and
**objectives** whose states (`Locked`/`Active`/`Completed`/`Failed`) are **derived from the shared `BaseContext`**.
The progression DAG is delegated to graphstandard's existing `ReactiveEvaluator` (edges = prerequisites; a
context string-set tracks completion; re-derivation is idempotent and history/save-safe). graphquest adds the thin
domain overlay the evaluator lacks: a **completion condition** per objective (a graphcore `BaseCondition` that,
when it holds, records the objective into the completed-set), an optional **fail condition** (a fourth `Failed`
state), **quest-level aggregation** (a quest completes when all *required* objectives complete), and **one-shot
reward hooks** (a graphcore `BaseAction` fired once on the completed transition, guarded by a "rewarded" context
set so it never re-fires across re-evaluation or restore). Authoring is a **code-first fluent builder**
(`QuestBuilder`) that emits a `QuestGraph` (objectives as `ObjectiveNodeData` nodes, prerequisites as edges).
Because all quest state lives in context collections, **persistence is automatic through graphsave's existing
context snapshot** (no hard graphsave dependency). The evaluator runs against any `BaseContext`, so a gameflow host
can drive it on its own `GameFlowContext` with **no dependency on gameflow**. The visual editor is deferred.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0 (matches the ecosystem).

**Primary Dependencies**: `com.faolline.graphcore` 0.14.0 (`BaseGraph`, `BaseNodeData`, `BaseEdgeData`,
`BaseContext` collections, `BaseCondition`, `BaseAction`), `com.faolline.graphstandard` 0.10.1
(`ReactiveEvaluator`, `ReactiveNodeState`, and its standard `BaseCondition`/`BaseAction` SOs for code-first use).
No hard dependency on graphsave or gameflow.

**Storage**: None of its own. All quest state is held in three graphcore string-set collections on the shared
`BaseContext` (completed / failed / rewarded). Persistence piggybacks on graphsave's `GraphRunSnapshot`, which
already serializes context collections — verified by a test that references graphsave (test-only).

**Testing**: NUnit EditMode only (the model + evaluator are headless; constitution forbids requiring PlayMode for
this layer). TDD: failing tests first.

**Target Platform**: Unity Editor + players (runtime is pure C#, no MonoBehaviour in core).

**Project Type**: Unity package (library) — single runtime assembly + EditMode test assembly. No editor assembly in
v1 (visual editor deferred).

**Performance Goals**: Evaluation is O(nodes + edges) per pass (the ReactiveEvaluator's derivation); a quest is
small (tens of objectives). No special perf target beyond "instant for hand-authored quests."

**Constraints**: graphcore + graphstandard UNTOUCHED. Additive new package. `[GraphQuest]` log prefix; one class
per file; `Action<T>` (no UnityEvent); no MonoBehaviour in Runtime; XML docs; README + CHANGELOG.

**Scale/Scope**: New package, ~8–10 runtime files + test files. v1 surface: model (`ObjectiveNodeData`,
`QuestGraph`, `QuestState`), `QuestEvaluator`, `QuestBuilder`, typed context keys, and (optional) `QuestContext`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ PASS | graphcore untouched. New downstream package; nothing depends on it yet, so no break risk. |
| II. Universal Abstractions Only | ✅ PASS | Quest/objective/reward semantics are domain concepts and live in graphquest, never graphcore. graphcore stays domain-free. |
| III. Specification-First | ✅ PASS | spec.md approved (checklist all green) before this plan. |
| IV. Test-Driven Development | ✅ PASS | Tests-first per user story (states from context; prereq gating chain+DAG; reward fires once; builder topology; graphsave round-trip). EditMode only. |
| V. Simplicity (YAGNI) | ✅ PASS | Reuses `ReactiveEvaluator` and context collections instead of a new engine/store (the constitution's "if it exists, use it"). The only new types are the domain overlay the engine genuinely lacks (completion/fail conditions, Failed state, quest aggregation, one-shot rewards, builder). See Complexity Tracking for the `ObjectiveNodeData`/`QuestGraph` justification. |
| VI. Typed Context Contract | ✅ PASS (with required types) | graphquest uses `BaseContext` collections at runtime, so it MUST ship `QuestContextKeys` (the completed/failed/rewarded set-key consts — no raw literals at call sites) and a `QuestContext : BaseContext` subclass overriding `CreateCloneInstance()` for standalone use. Conditions/actions stay generic (`BaseContext` param). The evaluator also accepts any `BaseContext` (a host's context) so quests can run on the host's blackboard. |
| VII. Cross-lib via SubGraph only | ✅ PASS | graphquest depends on graphcore and graphstandard only. graphstandard is the foundational *buffer* lib (shared engines), not a sibling *domain* lib — building on it is the intended path (graphTest already does), not a cross-domain coupling. No dependency on dialoguesystem/gameflow/etc. The gameflow seam is "accept any BaseContext", needing no gameflow reference. |
| Dev standards | ✅ PASS | `[GraphQuest]` prefix; one class per file; XML docs; `Action<T>`; no MonoBehaviour in core; README + CHANGELOG; `###-feature` branch. |

**Result**: PASS — no unjustified violations. The one deviation from "pure data" (introducing `ObjectiveNodeData` +
`QuestGraph` rather than a side-table) is justified below (it is what `ReactiveEvaluator` consumes and what the
deferred editor will author).

## Project Structure

### Documentation (this feature)

```text
specs/029-graph-quest/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — entities + state transitions
├── quickstart.md        # Phase 1 — consumer walkthrough
├── contracts/
│   └── public-api.md     # Phase 1 — the public API surface
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
com.faolline.graphquest/
├── package.json                                  # 0.1.0; deps: graphcore 0.14.0, graphstandard 0.10.1
├── README.md
├── CHANGELOG.md
├── Runtime/
│   ├── com.faolline.graphquest.Runtime.asmdef    # refs: graphcore.Runtime, graphstandard.Runtime
│   ├── Model/
│   │   ├── ObjectiveNodeData.cs                   # : BaseNodeData — completion/fail condition, required flag, reward
│   │   ├── QuestGraph.cs                          # : BaseGraph — objectives(nodes)+prereqs(edges)+unlock+reward+rule
│   │   └── QuestState.cs                          # enum: Locked | Active | Completed | Failed (quest AND objective)
│   ├── QuestEvaluator.cs                          # wraps ReactiveEvaluator + condition/fail/reward overlay + events
│   ├── Context/
│   │   ├── QuestContext.cs                        # : BaseContext (CreateCloneInstance override) — standalone use
│   │   └── QuestContextKeys.cs                    # const set-keys: completed / failed / rewarded (no raw literals)
│   └── Builder/
│       └── QuestBuilder.cs                        # fluent code-first authoring → QuestGraph
└── Tests/
    └── EditMode/
        ├── com.faolline.graphquest.Tests.EditMode.asmdef  # refs Runtime, graphcore, graphstandard, graphsave, TestRunner
        ├── QuestStateDerivationTests.cs           # US1
        ├── QuestPrerequisiteGatingTests.cs        # US2 (chain + diamond DAG + cycle rejection)
        ├── QuestRewardHookTests.cs                # US3 (fires once)
        ├── QuestPersistenceTests.cs               # US4 (graphsave round-trip)
        ├── QuestHostContextTests.cs               # US5 (runs on an external context; no gameflow ref)
        └── QuestBuilderTests.cs                   # builder produces the declared topology
```

**Structure Decision**: A standard Faolline package: one Runtime assembly + one EditMode test assembly, no editor
assembly in v1. Mirrors graphstandard/dialoguesystem layout. The runtime is split into Model (data), the evaluator,
the typed-context companions, and the builder — one class per file per dev standards.

## Complexity Tracking

| Decision | Why needed | Simpler alternative rejected because |
|----------|------------|--------------------------------------|
| Introduce `ObjectiveNodeData : BaseNodeData` + `QuestGraph : BaseGraph` (rather than a plain side-table of objective→condition) | `ReactiveEvaluator` consumes a `BaseGraph` (nodes + edges) for its DAG derivation; representing objectives as nodes and prerequisites as edges lets graphquest reuse the engine verbatim and sets up the deferred visual editor (which authors a graph). It is the same per-lib pattern as dialogue/gameflow/starter (own graph + node types). | A side-table would still need a `BaseGraph` built for the evaluator, so it adds a parallel structure to keep in sync instead of removing one — more complexity, not less, and no editor path. |
| Hold quest state in three context **collections** (completed/failed/rewarded) | Reuses graphcore collections + the ReactiveEvaluator's completed-set; makes state replay-safe (re-derived from the set) and **persisted for free** by graphsave's existing context snapshot — satisfying FR-012 with no graphsave dependency. | A bespoke quest-progress store would duplicate what the context + graphsave already do and would need its own serialization + a graphsave coupling. |
