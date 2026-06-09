# Phase 0 — Research: P4 Generic threshold Join

Decisions respect the constitution (graphcore untouched / universal / TDD / simplicity). No NEEDS
CLARIFICATION remained.

## R1 — Threshold as evaluator config vs. a serialized node field / new Join node type

**Decision**: Supply per-node required counts as **evaluator configuration** — an optional
`IReadOnlyDictionary<string,int>` (node id → k) on a new optional `ReactiveEvaluator` constructor
parameter. No graphcore change, no new node type, no editor UI.

**Rationale**:
- Keeps graphcore untouched (a serialized threshold field would mean editing `BaseNodeData`).
- Minimal surface: an optional ctor parameter is source-compatible — every P3 caller keeps working.
- A dedicated authorable "Join node" with an inspector is a later, heavier feature; the config map delivers
  the full k-of-N capability now (YAGNI).

**Alternatives considered**:
- *Serialized `RequiredCount` on a node*: rejected for the MVP — touches graphcore and needs editor work.
- *Separate AND/OR/N-of-M node types*: rejected — the user chose one generic threshold; three types is
  proliferation.

## R2 — Default = N (AND) and the boundary semantics

**Decision**: When a node has no configured count, k defaults to its prerequisite count **N** (AND), making
P3 behavior the zero-config default. Boundaries: **k ≤ 0** ⇒ ungated (Available unless Completed); **k > N**
⇒ never auto-available from prerequisites (Locked until host-completed). No clamping, no error.

**Rationale**:
- Default-to-N guarantees the 602-test suite stays green unchanged (SC-002).
- k≤0 and k>N are meaningful authoring choices (an always-open node; a node only the host can complete);
  defining them explicitly avoids surprises and crashes (FR-005, FR-007).

**Alternatives considered**:
- *Clamp k to [0,N]*: rejected — would silently turn "never auto" (k>N) into AND, hiding author intent.

## R3 — Why no other code path changes

**Decision**: Only `DeriveState` changes. Cascade (`MarkCompleted`→`Reevaluate`), events, `Start`, and the
reversible re-evaluation all compute state **through `DeriveState`**, so the threshold is honored
everywhere automatically.

**Rationale**:
- P3 was built with a single derivation chokepoint precisely so the availability rule could evolve in one
  place. The threshold slots in there; transition detection, emission, and idempotency are unaffected.

**Alternatives considered**:
- *Touch each lifecycle method*: rejected — unnecessary and risk-prone; the chokepoint already centralizes
  the rule.
