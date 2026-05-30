# Feature Specification: starterGraph — Reusable Downstream-Lib Starter

**Feature Branch**: `009-starter-graph`

**Created**: 2026-05-30

**Status**: Draft

**Input**: User description: créer `com.faolline.starterGraph`, un package starter réutilisable bâti sur graphcore, version épurée et générique du package de vérification graphTest, embarquant tout ce qui est généralement nécessaire (runtime, éditeur, interface) pour qu'un dev démarre un nouveau lib downstream en n'ajoutant que son domaine.

## Overview

`com.faolline.starterGraph` is a **starter package**: the canonical, clean starting point for any new downstream library of the ecosystem (dialogue, quest, gameflow, …). It is the generic equivalent of the `graphTest` verification package — it bundles **everything generally needed** at the runtime, editor, and interface layers, validated patterns and all, so a developer copies/renames it and only has to add their own domain (nodes, conditions, actions, context properties).

The "user" is a **developer** building a downstream lib on graphcore. Success means they can produce a working, runnable, authorable graph lib in their own domain with minimal effort, because every reusable piece is already present and correct.

The package builds **only on `com.faolline.graphcore`** and changes nothing in it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Runtime foundation a downstream lib can build on (Priority: P1)

A developer has the runtime building blocks to author and execute graphs in code: a graph asset type, a typed context following the mandated contract, a choice type, and a full set of typed conditions and actions. They can construct a graph, run it through the runtime, and have conditions/actions read and write typed values.

**Why this priority**: The runtime layer is the foundation every other layer and every downstream lib depends on. It is independently valuable (a lib could ship headless logic on it alone) and is the MVP.

**Independent Test**: In code, create a starter graph with a statement node carrying a "set int" action and an edge gated by an "int comparison" condition; run it through the runtime and confirm the value is set and the branch is taken accordingly; confirm the typed context survives a history step-back.

**Acceptance Scenarios**:

1. **Given** a starter graph asset type, **When** a developer creates an instance, **Then** it behaves as a graph (holds nodes, edges, parameters) and is creatable from the asset menu.
2. **Given** the typed context type, **When** values are set and the context is cloned for history, **Then** the clone is of the same typed subtype and preserves all values (so step-back never breaks).
3. **Given** the typed condition set (always-true/false, bool, int, float, string), **When** evaluated against a context, **Then** each returns the correct result and returns false with a warning (never throws) on a missing or wrong-typed key.
4. **Given** the typed action set (log, set bool/int/float/string), **When** executed, **Then** each writes the correct typed value into the context.
5. **Given** a choice type with a label, **When** used on a choice node, **Then** it carries its label and optional condition.
6. **Given** all parameter keys, **When** referenced in code, **Then** they come from a single keys constants class (no raw string literals at call sites).

---

### User Story 2 — Full editor to author and run graphs (Priority: P2)

A developer opens the starter editor window, builds a graph on the canvas with every node type (Start, Statement, Choice, SubGraph, End), edits each node's properties in the inspector, and runs the graph with full execution navigation — pausing at choices and selecting branches, stepping back, and resuming.

**Why this priority**: The editor/interface layer is the headline value of the starter — it is precisely "everything generally useful" that a downstream lib would otherwise re-implement. It depends on US1's runtime but is otherwise self-contained.

**Independent Test**: Open the editor, add one of each node type via the context menu, wire them, configure a Choice and a SubGraph in the inspector, set an End reason, declare a typed parameter; click Run, pause at the Choice, select a branch via Choose, step back, and resume — all from the editor.

**Acceptance Scenarios**:

1. **Given** an open starter editor, **When** the developer uses the canvas context menu, **Then** they can add a Start, Statement, Choice, SubGraph, and End node, each rendered with its ports (Choice has one output per choice; SubGraph has one input and one output).
2. **Given** a selected node, **When** the developer opens the inspector, **Then** the appropriate section is shown: label editing (Statement), End reason selector (End), choice management with add/remove/label/condition (Choice), target-graph + inherit-context with cycle refusal (SubGraph), and the shared base-node section (checkpoint, color, conditions, actions).
3. **Given** the inspector with no node selected, **When** the developer views the parameter panel, **Then** they can add/remove parameters of any type (bool/int/float/string) with a type selector and a default value.
4. **Given** a built graph, **When** the developer clicks Run, **Then** execution proceeds and logs each visited node; on reaching a Choice it pauses and the Choose control offers the choices whose condition passes.
5. **Given** execution paused at a Choice, **When** the developer selects a choice, **Then** execution resumes on that branch; **and** GoBack / GoBackToCheckpoint / Continue navigate the run as expected.
6. **Given** a Choice node, **When** the developer adds or removes a choice, **Then** the output ports update live and edges bound to surviving choices stay connected.

---

### User Story 3 — Reusable robustness and ergonomics out of the box (Priority: P3)

A developer benefits from the already-validated robustness without writing it: graphs reload with their edges intact, switching or reloading a graph never loses data, multiple graphs can be open side by side (one window per graph), recursive sub-graphs are refused, and a generated sample graph demonstrates the whole package.

**Why this priority**: These are the "paper-cut" behaviors that make the difference between a toy and a real starter. They depend on US2's editor but each is independently observable.

**Independent Test**: Save a graph and reopen it — edges render. Open two graphs — two windows. Try to point a sub-graph at an ancestor — refused. Generate the sample graph from a menu — it runs end to end.

**Acceptance Scenarios**:

1. **Given** a saved graph, **When** it is reloaded, **Then** all edges are drawn (connected to their ports), including choice and sub-graph edges.
2. **Given** a loaded graph, **When** another graph is loaded or the same graph is reloaded, **Then** the first graph's data is never deleted.
3. **Given** two different graph assets, **When** both are opened, **Then** each opens in its own window titled by the asset name.
4. **Given** a sub-graph node, **When** the developer assigns a target graph that references the current graph directly or transitively, **Then** the assignment is refused with a clear message and no cycle is created; **and** a runtime cycle (if one is somehow formed) is detected and stops execution rather than looping forever.
5. **Given** the package, **When** the developer runs the "create sample graph" menu, **Then** a self-contained sample graph is generated that exercises choices, conditions, actions, a checkpoint, a sub-graph, and typed parameters, and runs to completion.

---

### Edge Cases

- A condition reads a missing or wrong-typed parameter key → returns false with a warning, never throws.
- A sub-graph node with no target graph → execution halts gracefully (no exception).
- A graph with no entry node, or a node with no valid outgoing edge → clear console message / stuck handling, not a crash.
- A pure loop with no exit in a graph → execution is bounded by a step cap rather than hanging forever.
- Removing a choice that has a connected edge → the choice, its port, and its edge are all removed; other choices' edges remain connected.
- The default value entered for a typed parameter is malformed → it falls back to a sensible default with a warning.

## Requirements *(mandatory)*

### Functional Requirements

**US1 — Runtime foundation**

- **FR-001**: The package MUST provide a starter graph asset type creatable from the asset menu that holds nodes, edges, and parameters.
- **FR-002**: The package MUST provide a typed context type that follows the typed-context contract: typed accessor properties for at least one bool, int, float, and string value; all keys sourced from a companion keys constants class; and a clone operation that returns the same subtype with all values preserved.
- **FR-003**: The package MUST provide a choice type carrying a human-readable label and an optional condition.
- **FR-004**: The package MUST provide typed conditions covering: always-true, always-false, bool, int (with comparison operator), float (with comparison operator), and string (equality with optional negation). Each MUST return false with a warning — never throw — when its key is absent or holds the wrong type.
- **FR-005**: The package MUST provide typed actions covering: log a message, and set a bool, int, float, and string value into the context.
- **FR-006**: The package MUST provide an example domain statement node (with an editable label) demonstrating how to extend a graphcore node.
- **FR-007**: No raw parameter-key string literal MUST appear at a code call site; all key references go through the keys constants class.

**US2 — Editor & interface**

- **FR-008**: The editor MUST render canvas views for every node type — Start, Statement, Choice (one output port per choice, routed by the choice's id), SubGraph (one input, one output), and End — with the correct dispatch.
- **FR-009**: The canvas context menu MUST allow adding each node type (Start, Statement, Choice, SubGraph, End).
- **FR-010**: The inspector MUST provide, per selected node type, all the generally-useful sections: label editing, End-reason selection, choice management (add/remove, label, condition, live ports), sub-graph configuration (target graph + inherit-context), and the shared base-node section (checkpoint, color, entry conditions, enter/exit actions).
- **FR-011**: The inspector MUST provide a parameter panel that adds and removes parameters of any supported type (bool/int/float/string) via a type selector and a default value, and lists existing parameters with their type and default.
- **FR-012**: The editor window MUST provide the full execution loop and navigation: Run, Choose (offering only condition-passing choices), Continue, GoBack, and GoBackToCheckpoint; it MUST pause at a Choice node and resume on the selected branch; and it MUST log visited nodes and end reason.

**US3 — Robustness & ergonomics**

- **FR-013**: Reloading a graph MUST render all of its edges connected to their ports (including choice and sub-graph edges).
- **FR-014**: Loading or reloading a graph MUST NOT delete any graph's data.
- **FR-015**: Adding or removing a choice MUST update the node's output ports live and keep surviving choices' edges connected.
- **FR-016**: Assigning a sub-graph target that would create an inter-graph cycle MUST be refused at edit time with a clear message; a runtime cycle MUST be detected and stop execution rather than loop indefinitely.
- **FR-017**: Each opened graph asset MUST appear in its own editor window titled by the asset name (multiple graphs open simultaneously).
- **FR-018**: The package MUST provide a menu command that generates a self-contained sample graph exercising choices, conditions, actions, a checkpoint, a sub-graph, and typed parameters, runnable to completion.

**Cross-cutting**

- **FR-019**: The package MUST build only on `com.faolline.graphcore` and MUST NOT modify it.
- **FR-020**: The package MUST be structured for reuse: a developer can copy/rename it and only add domain nodes/conditions/actions/context properties to get a working lib.
- **FR-021**: Every behavior MUST be covered by EditMode tests written before its implementation (TDD).

### Key Entities

- **StarterGraph**: the graph asset type for the starter package.
- **StarterContext / StarterContextKeys**: the typed context subtype (with typed bool/int/float/string properties and clone override) and its companion keys constants.
- **StarterChoice**: a choice with a label and optional condition.
- **Typed conditions**: always-true, always-false, bool, int, float, string.
- **Typed actions**: log, set bool, set int, set float, set string.
- **Node views**: Start, Statement, Choice, SubGraph, End canvas views; plus the domain edge view.
- **Inspector sections**: label, end-reason, choice, sub-graph, parameter panel, base-node.
- **Editor window**: hosts the canvas + inspector and the execution/navigation controls.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can author a graph using all five node types and run it to completion entirely from the editor, pausing at a choice and resuming on a selected branch.
- **SC-002**: 100% of the typed conditions and actions (bool/int/float/string) read and write the correct values, and every condition returns false (never throws) on a missing or wrong-typed key.
- **SC-003**: The typed context survives a history step-back with all bool/int/float/string values restored and the correct subtype preserved.
- **SC-004**: Graphs reload with 100% of their edges drawn, and no data-loss occurs across reload/switch — verified over repeated reloads and graph switches.
- **SC-005**: Two graphs can be open in two windows simultaneously, and a recursive sub-graph assignment is refused 100% of the time.
- **SC-006**: A sub-graph runs its child to completion and resumes the parent, including at least two levels of nesting, and a runtime cycle is detected rather than hanging.
- **SC-007**: The generated sample graph runs end to end, demonstrating choices, conditions, actions, a checkpoint, a sub-graph, and typed parameters.
- **SC-008**: A developer can stand up a new domain lib by copying the starter and adding only domain types — no editor/runtime plumbing rewritten — and the full EditMode suite stays green.

## Assumptions

- The "user" is a developer using Unity; this is a foundation/starter package, not an end-user product.
- `com.faolline.graphcore` already provides the underlying runtime (runner, history/checkpoints, sub-graph entry/exit, cycle detection, typed `BaseContext` for bool/int/float/string, parameter defaults parsing) and needs no changes — the starter wires and demonstrates these.
- The starter mirrors the proven structure and behaviors already validated in `com.faolline.graphTest` (which serves as the working reference), renamed and generalized; it does not need to invent new runtime capabilities.
- Typed conditions use a simple comparison model (operators for numbers, equality for strings) — no expression language.
- Copy/paste and undo/redo of nodes are out of scope (consistent with prior features).
- EditMode (NUnit / Unity Test Runner) tests only; no MonoBehaviour, no UnityEvent.
- The example domain types (one statement node, sample context properties) are illustrative seeds for the developer to replace/extend, not a real domain.
