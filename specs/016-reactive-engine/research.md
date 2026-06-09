# Phase 0 — Research: P3 Reactive engine

Decisions respect the constitution (graphcore untouched / universal abstractions / TDD / simplicity). No
NEEDS CLARIFICATION remained from the spec.

## R1 — Where the engine lives: new `graphstandard` lib vs. graphcore/graphTest

**Decision**: A new package **`com.faolline.graphstandard` (0.1.0)** depending on graphcore.

**Rationale**:
- The roadmap reserves graphstandard as the buffer lib for non-Linear engines + standard nodes
  (memory/graphstandard-buffer-lib.md). The Reactive engine is its first inhabitant.
- graphcore is the substrate and must stay paradigm-neutral (Principle I/II) — a concrete engine with a
  specific traversal-replacement belongs above it, not inside it.
- graphTest is a throwaway sandbox, not a shipping home for a real engine.

**Alternatives considered**:
- *Engine in graphcore*: rejected — graphcore holds only the substrate + the Linear reference runner;
  adding a second engine would bloat the foundation and blur Principle II.
- *Engine in graphTest*: rejected — graphTest is disposable; the engine is a real, depended-upon component.

## R2 — Edges as prerequisites + AND semantics

**Decision**: The Reactive engine reads an edge `A→C` as "**C requires A**". A node's prerequisites are
the **source nodes of its incoming edges**; a node is Available when it has no prerequisites or **all**
prerequisites are Completed (AND).

**Rationale**:
- Same substrate, different engine interpretation — the core insight of the substrate+engines split. The
  Linear runner reads `A→C` as flow; the Reactive engine reads it as dependency. No new data needed.
- AND-of-incoming matches the example game's `ArePrerequisitesCompleted` (all prerequisites solved).
- The generic threshold (OR / N-of-M) is the P4 Join — deliberately deferred; AND covers the evidenced DAG.

**Alternatives considered**:
- *A dedicated dependency-edge type*: rejected — `BaseEdgeData` already expresses a directed relation;
  reusing it keeps authoring uniform and the substrate untouched.
- *Threshold semantics now*: rejected — that is P4; YAGNI for the MVP.

## R3 — Completion via the P2 completed-set vs. an engine-private store

**Decision**: Completion is recorded in a **graphcore P2 string-set collection** (the completed-set) on the
shared `BaseContext`, named at evaluator initialization. The engine holds only a derived in-memory state
cache for transition detection.

**Rationale**:
- "The collection IS the save": the completed-set persists (save) and history-restores (step-back) for
  free via P2's DeepClone/GetAllCollections — no separate persistence (the research's key convergence).
- Deriving node state from the set (rather than storing per-node state) makes re-evaluation **idempotent**
  and **reversible**: shrink the set → recompute a smaller satisfied set, with no undo side-effects (FR-008).

**Alternatives considered**:
- *Engine-private `Dictionary<nodeId,state>` as source of truth*: rejected — it would need its own
  persistence and could desync from saved progress; the set-derived model gets durability for free.

## R4 — Transition detection + event rules

**Decision**: The evaluator keeps a cached `Dictionary<string, ReactiveNodeState>`. Re-evaluation
recomputes every node's state; for each node whose state **changed**, it emits `OnNodeAvailable` (on
entering Available) or `OnNodeCompleted` (on entering Completed). Transitions to Locked emit nothing in the
MVP. Initialization emits the entry event for each node's initial non-Locked state. No change ⇒ no event.

**Rationale**:
- Comparing against the cache gives exactly-once, no-spurious, idempotent events (FR-003/FR-004, SC-003).
- Emitting on init for available/completed handles a loaded save (some nodes already Completed/Available).

**Alternatives considered**:
- *Emit on every evaluation regardless of change*: rejected — spurious events; consumers would double-fire.
- *A combined `OnNodeStateChanged(node, state)` event*: viable but two explicit events
  (`OnNodeAvailable`/`OnNodeCompleted`) read better for the unlock/complete use; a Locked event can be
  added later (append-only) if needed.

## R5 — Re-evaluation trigger: explicit (MVP) vs. auto-subscribe to the P2 change

**Decision**: MVP triggers re-evaluation **explicitly**: `MarkCompleted` re-evaluates, and a public
`Reevaluate()` lets the host re-evaluate after a context restore (step-back). The evaluator does **not**
auto-subscribe to the completed-set's P2 `OnCollectionChanged` in the MVP.

**Rationale**:
- Deterministic and simple. Note: graphcore's `CopyValuesFrom` (history restore) rebuilds collections
  **without** firing `OnCollectionChanged`, so an auto-subscription would NOT catch a step-back anyway —
  the host must call `Reevaluate()` after `GoBack`. Making re-evaluation an explicit, public operation is
  therefore both simpler and necessary.
- Auto-subscription (re-evaluate on any live collection change) is a clean future enhancement for
  externally-driven sets, layered on top without changing the MVP contract.

**Alternatives considered**:
- *Auto-subscribe only*: rejected — wouldn't cover step-back (no notification on restore) and hides when
  re-evaluation happens.

## R6 — Package / asmdef wiring and conventions

**Decision**: `com.faolline.graphstandard/package.json` declares a dependency on `com.faolline.graphcore`.
The Runtime asmdef (`com.faolline.graphstandard.Runtime`, rootNamespace `Faolline.GraphStandard`)
references `com.faolline.graphcore.Runtime` **by assembly name**. The EditMode test asmdef references the
Runtime + graphcore + `UnityEngine.TestRunner`/`UnityEditor.TestRunner` (and nunit), `autoReferenced:false`,
`testPlatforms:["EditMode"]`. Unity generates the `.meta` GUIDs on first import. Log prefix `[GraphStandard]`.

**Rationale**:
- Name references avoid the chicken-and-egg of needing the new asmdef's own GUID before it exists, and are
  resolved by Unity. Mirrors the existing graphcore/graphTest layout and naming discipline.

**Alternatives considered**:
- *Hand-author `.meta` GUIDs*: rejected — error-prone; Unity assigns them deterministically on import.
