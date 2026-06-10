# Feature Specification: code-first graph ergonomics (slice 4)

**Feature Branch**: `023-graph-builder`

**Created**: 2026-06-10

**Status**: Draft

**Input**: User description: "A fresh naive consumer rebuilt the escape-room over the now-fixed libraries; the
round-1 critical gaps were gone. Three finer ergonomics gaps remain: no public way to build a graph in code
(had to copy an internal sample), no time-wait query symmetric with the signal one, and no doc blessing the
looping game-shell graph. Headline: a public fluent graph builder in graphstandard."

## Overview

A second dogfooding pass (a fresh consumer building the same escape-room over the **fixed** libraries)
confirmed the round-1 fixes landed — the driver-persistence, README, and time-event gaps were gone — and the
feedback **moved up a layer** to three finer ergonomics gaps:

1. **No public way to build a graph in code.** Constructing a graph for a reproducible builder + headless
   tests still means hand-writing GUID node ids, `AddNode`/`AddEdge`, and the sub-asset action wiring —
   knowledge that lives only inside an *internal* sample file, not in the public surface. This was hit in
   **both** rounds: code-first graph construction is a foundational ergonomic the ecosystem lacks.
2. **No time-wait query** on the driver, asymmetric with the signal query added in slice 3 — a scene can't
   read "we are time-waiting, X seconds remain" to drive a synced countdown.
3. **The looping game-shell pattern is undocumented** — a consumer modeled the whole menu→play→win→menu shell
   as a Linear graph that loops with no End node; it works, but the docs never said it was intended.

This slice adds a **public fluent graph builder** (in `graphstandard`, the buffer/helper lib — option B) plus
a small **editor persist utility**, the **time-wait query** on the gameflow driver, and the **doc** blessing
the cyclic shell. graphcore is untouched.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Code-first author / lib developer** — builds a graph in C# (a reproducible factory + headless tests)
  instead of by hand in the editor.
- **Game integrator** — drives a timed beat (an intro) and wants the scene to show a synced countdown.

### User Story 1 - Build a graph in code with a fluent builder (Priority: P1) 🎯 MVP

A developer builds a complete graph — nodes (Start/Statement/Choice/SubGraph/End) with titles, edges, attached
enter/exit actions and entry conditions, await-signal names, wait durations, checkpoints, choices, and the
entry node — through a readable fluent API, and gets back a ready graph of the desired type (e.g. a
`GameFlowGraph`). No GUID/`AddNode`/`AddEdge` boilerplate, no copying an internal sample.

**Why this priority**: Code-first construction is the headline gap, hit in both dogfooding rounds; it unblocks
reproducible builders and headless tests for every graph lib.

**Independent Test**: build a small flow (start → load-action statement → await node → end) with the builder;
the returned graph has exactly the intended nodes, edges, entry node, the await name, and the attached action.

**Acceptance Scenarios**:

1. **Given** the builder, **When** a developer adds nodes of each universal type and connects edges, **Then**
   the built graph contains those nodes and edges with the configured titles/positions and a designated entry
   node.
2. **Given** a node in the builder, **When** the developer attaches an enter/exit action, an entry condition,
   an await-signal name, a wait duration, or a checkpoint, **Then** the built node carries exactly those.
3. **Given** a Choice node, **When** the developer adds choices (each optionally condition-gated), **Then** the
   built node carries those choices.
4. **Given** a requested graph type (any `BaseGraph` subclass), **When** the graph is built, **Then** the
   returned instance is of that type.
5. **Given** a graph built by the builder, **When** it is driven by a runner/driver, **Then** it runs exactly
   as the same graph hand-assembled would (the builder adds no behavior, only construction).

### User Story 2 - Persist a built graph as an asset with its actions as sub-assets (Priority: P2)

A developer persists an in-memory graph to a `.asset` file with its attached actions/conditions stored as
**sub-assets** (so the asset is self-contained and portable), via a documented editor utility — instead of
reverse-engineering the `AddObjectToAsset` scan from an internal sample.

**Why this priority**: The companion to US1 — building in code is most useful when you can also save the
result as a first-class asset. It's editor-only and small, but today it's undocumented tribal knowledge.

**Independent Test**: build a graph whose nodes carry actions, persist it via the utility, reload the asset,
and confirm the actions are sub-assets of the saved graph.

**Acceptance Scenarios**:

1. **Given** an in-memory graph with attached actions/conditions, **When** the developer persists it via the
   utility, **Then** a graph asset is written and each attached action/condition is a sub-asset of it.
2. **Given** the persisted asset, **When** it is reloaded, **Then** the graph and its sub-asset actions load
   intact (the asset is self-contained).

### User Story 3 - Read an in-progress timed wait from the driver (Priority: P2)

A scene that loads while the flow is parked on a **timed** node reads, from the driver's public surface, that
a timed wait is in progress and how much time remains, so it can render a synced countdown — symmetric with
the existing signal query.

**Why this priority**: The exact time-mirror of the signal-wait query added in slice 3; without it, timed
beats are opaque to a late-loading scene.

**Independent Test**: drive a graph with a wait node, feed partial time; the driver reports it is time-waiting
with the remaining seconds, which reaches zero as time is fed.

**Acceptance Scenarios**:

1. **Given** the flow parked on a timed node, **When** a script queries the driver, **Then** it reports a
   timed wait in progress with the remaining seconds (and the total duration).
2. **Given** time fed toward the wait, **When** the script queries again, **Then** the remaining seconds have
   decreased; once the wait resolves, the driver reports it is no longer time-waiting.
3. **Given** the flow is not on a timed node (before boot, after end, or on another node), **When** queried,
   **Then** the driver reports it is not time-waiting and zero remaining.

### User Story 4 - A looping game-shell graph is a documented, supported pattern (Priority: P3)

A developer learns from the docs that a **cyclic Linear graph with no End node** (menu → play → win → back to
menu → replay) is an intended way to model a game shell — it never ends (the flow stays running), and for a
forever-looping shell a small history depth is appropriate.

**Why this priority**: Pure documentation; it removes the "is this an abuse?" doubt a consumer hit, at no code
cost.

**Independent Test**: the gameflow/builder docs state that a no-End cyclic graph is a supported shell pattern,
note that it never raises the end event, and advise a small history depth for a looping shell.

**Acceptance Scenarios**:

1. **Given** the docs, **When** a developer looks for how to loop a game shell, **Then** they find that a
   cyclic Linear graph with no End node is supported and how it behaves (never ends; history is bounded).

### Edge Cases

- **Builder: an edge referencing an unknown node** → surfaced clearly (an explicit error/exception), not a
  silently broken graph.
- **Builder: no start/entry designated** → the builder either requires it or the resulting graph fails
  validation the same way a hand-built one would; not a silent half-graph.
- **Persist utility: a target path already in use** → does not silently corrupt; overwrites or fails clearly.
- **Persist utility: a node action that is already an asset (not an in-memory instance)** → not double-added
  as a sub-asset.
- **Time query before boot / after end** → reports "not time-waiting", zero remaining, no exception.
- **Time-wait remaining never goes negative** → clamped at zero.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `graphstandard` MUST provide a public fluent builder that constructs any `BaseGraph` subclass
  over graphcore's universal node/edge/action/condition model, returning the typed graph instance.
- **FR-002**: The builder MUST support the universal node types (Start/Statement/Choice/SubGraph/End) with
  title and position; edges between nodes; attaching enter/exit actions and entry conditions; setting
  await-signal name, wait duration, and checkpoint; adding choices (optionally condition-gated) to a Choice
  node; and designating the entry node.
- **FR-003**: A graph produced by the builder MUST behave identically to the same graph assembled by hand —
  the builder adds construction convenience only, no runtime semantics.
- **FR-004**: `graphstandard` MUST provide an editor-only utility that persists an in-memory graph as an asset
  with its attached actions/conditions stored as sub-assets, producing a self-contained, reloadable asset.
- **FR-005**: gameflow's driver MUST expose, symmetric with the signal query, whether a **timed** wait is in
  progress and the remaining (and total) seconds, computed without any graphcore change; it MUST report
  "not waiting"/zero before boot, after end, and off a timed node, and MUST never report negative remaining.
- **FR-006**: The documentation MUST state that a cyclic Linear graph with no End node is a supported
  game-shell pattern (never ends; history bounded by history depth; advise a small depth for a looping shell).
- **FR-007**: graphcore MUST be unchanged (code and README). `graphstandard` bumps `0.3.0 → 0.4.0` (Runtime
  builder + a new Editor assembly for the persist utility); gameflow bumps `0.3.0 → 0.4.0` (the driver time
  query). Both additive/append-only; the existing 661 EditMode + 9 PlayMode tests stay green.
- **FR-008**: Universal Abstractions Only — the builder MUST encode only construction over graphcore's
  universal types; zero domain vocabulary.
- **FR-009**: Dev standards — `[GraphStandard]` / `[GraphGameFlow]` log prefixes; one class per file; C#
  `Action<T>` (no `UnityEvent`); XML docs; READMEs + CHANGELOGs updated.

### Key Entities

- **Graph builder**: the fluent constructor over the universal types, producing a typed `BaseGraph` subclass.
- **Graph-asset persist utility**: the editor helper that saves a graph with its actions/conditions as
  sub-assets.
- **Driver time-wait query**: the driver's report of an in-progress timed wait (in-progress flag + remaining +
  total).
- **Cyclic shell pattern**: a documented no-End looping Linear graph for a game shell.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer builds a multi-node flow (nodes, edges, await/wait/checkpoint, attached actions,
  choices, entry node) with the fluent builder in a few readable lines, with no GUID/`AddNode`/`AddEdge`
  boilerplate, and the built graph matches the intended structure exactly.
- **SC-002**: A graph built by the builder runs identically to the hand-assembled equivalent under a driver.
- **SC-003**: A built graph can be persisted to an asset whose attached actions are sub-assets, and reloads
  intact.
- **SC-004**: A scene can read, during a timed node, that a timed wait is in progress and the remaining
  seconds, which reach zero as time is fed.
- **SC-005**: The docs state the cyclic-shell pattern is supported and how it behaves.
- **SC-006**: graphcore untouched; the prior 661 EditMode + 9 PlayMode tests stay green; `graphstandard`
  ships `0.4.0` and gameflow ships `0.4.0`, both with README + CHANGELOG updated.

## Assumptions

- **Builder lives in graphstandard** (option B, user-chosen) — the buffer/helper lib above graphcore; it
  keeps graphcore minimal while giving every graph lib (dialogue, gameflow, …) a code-first construction
  helper. It produces any `BaseGraph` subclass (generic over the graph type).
- **No gameflow-specific builder sugar** (`.LoadScene()/.Await()/.Wait()`) in this slice — the universal
  builder + `LoadSceneAction` cover the need; a thin gameflow layer can come later if a concrete need appears.
- **The persist utility is editor-only** (it uses asset APIs) and lives in a new graphstandard Editor
  assembly; graphcore's editor is untouched.
- **The time-wait remaining is computed on the driver** from the wait duration (received with the time-wait
  event) minus the driver's own accumulated ticks — graphcore exposes no remaining-time API and is not
  changed.
- **The cyclic-shell item is documentation only** — the Linear runner already follows cycles; nothing in the
  runtime changes.

## Out of Scope *(deferred)*

- A gameflow-specific builder subclass with `.LoadScene()/.Await()/.Wait()` sugar.
- Any visual/editor surface for the builder.
- A restart / goto-node runtime affordance (the cyclic-edge pattern works and is documented here).
- Reactive / Flow hosting; save/load.
- Any change to graphcore (code or README).
