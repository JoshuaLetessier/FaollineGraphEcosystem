# Feature Specification: GraphTest — Editor Authoring Gaps

**Feature Branch**: `008-graphtest-authoring`

**Created**: 2026-05-30

**Status**: Draft

**Input**: User description: "Combler les lacunes d'authoring de l'éditeur graphTest pour rendre testables dans l'éditeur des fonctionnalités graphcore aujourd'hui non câblées côté graphTest : exposer EndReason dans l'inspector, ajouter un nœud SubGraph, supporter les paramètres typés Int/Float/String."

## Overview

The `com.faolline.graphTest` package is the verification harness for `com.faolline.graphcore`. Three graphcore capabilities exist at runtime but cannot currently be exercised from the graphTest editor, so they cannot be validated end-to-end by a developer authoring a graph: the **end reason** of an End node, **sub-graph** nesting, and **non-boolean typed parameters**. This feature wires those three capabilities into the graphTest authoring surface (context menu, node views, inspector) so each can be configured, run, saved, and reloaded from the editor.

The "user" throughout is a **developer** authoring and verifying graphs in the graphTest editor window.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Set an End node's End Reason (Priority: P1)

A developer selects an End node on the canvas and, in the inspector, chooses its end reason (Completed, Cancelled, or Error). When the graph runs and reaches that End node, the console reports the chosen reason. The choice is saved with the graph and survives reload.

**Why this priority**: Smallest, self-contained gap and the only one that needs no new node type — it makes the existing `EndReason` field (already logged at run time as "Graph ended: {reason}") configurable, unblocking verification that different end reasons propagate correctly. Delivers value on its own and is the MVP.

**Independent Test**: Add an End node, set its reason to "Cancelled" in the inspector, save and reload the graph (reason still "Cancelled"), then Run a graph that reaches it — the console logs `Graph ended: Cancelled`.

**Acceptance Scenarios**:

1. **Given** an End node is selected, **When** the developer opens the inspector, **Then** an end-reason selector is shown with the node's current reason (default "Completed").
2. **Given** the developer sets the reason to "Error", **When** they save and reload the graph, **Then** the End node's reason is still "Error".
3. **Given** a graph whose Run reaches an End node set to "Cancelled", **When** execution ends at that node, **Then** the console reports the end reason as "Cancelled".
4. **Given** a non-End node is selected, **When** the developer opens the inspector, **Then** no end-reason selector is shown.

---

### User Story 2 — Author and run a Sub-Graph node (Priority: P2)

A developer adds a Sub-Graph node to the canvas, assigns it a target graph asset and chooses whether it inherits the parent context. When the graph runs and reaches the Sub-Graph node, execution descends into the target graph, runs it to its end, and resumes the parent graph afterward.

**Why this priority**: Sub-graphs are the composition mechanism in graphcore; without authoring support they can only be tested at the library unit level, never end-to-end in the editor. Larger than US1 (needs a new node view, menu entry, and inspector section) but independent of US1 and US3.

**Independent Test**: Build a small "child" graph (Start → Statement → End). In a parent graph, add a Sub-Graph node pointing at the child, connect Start → Sub-Graph → End, Run — the console shows the child's nodes visited between entering and leaving the Sub-Graph node, then the parent completes.

**Acceptance Scenarios**:

1. **Given** an open graph, **When** the developer adds a Sub-Graph node via the context menu, **Then** a Sub-Graph node appears on the canvas with one input and one output port.
2. **Given** a Sub-Graph node is selected, **When** the developer opens the inspector, **Then** they can assign a target graph asset and toggle "inherit parent context".
3. **Given** a Sub-Graph node with a valid target graph, **When** the graph is saved and reloaded, **Then** the target graph and the inherit-context choice are preserved and the node's edges are intact.
4. **Given** a parent graph whose Run reaches a Sub-Graph node, **When** execution enters it, **Then** the target graph runs to completion and execution resumes in the parent.
5. **Given** a Sub-Graph node whose target graph (directly or transitively) references the parent graph, **When** the developer assigns or connects it, **Then** the recursive reference is refused and reported, preventing a cycle.
6. **Given** a Sub-Graph node with no target graph assigned, **When** Run reaches it, **Then** execution halts gracefully with a clear console message rather than throwing.

---

### User Story 3 — Author typed Int / Float / String parameters and gate on them (Priority: P3)

A developer declares graph parameters of type Int, Float, or String (not just Bool) in the inspector parameter panel, with a default value. They attach conditions that test those parameters and actions that set them, then verify that execution routes according to non-boolean values.

**Why this priority**: Conditional routing is already proven for Bool; extending it to Int/Float/String broadens coverage of the parameter/condition/action machinery. It is the largest slice (new conditions, actions, and an extended parameter panel) and depends on nothing from US1/US2.

**Independent Test**: Declare an Int parameter `score = 0`. Build Start → Statement(action: set `score = 5`) → Choice with two choices, one gated by "score ≥ 3" and one with no condition → branches → End. Run: the gated choice is available; change the action to set `score = 1` and re-run: the gated choice is filtered out.

**Acceptance Scenarios**:

1. **Given** the parameter panel, **When** the developer adds a parameter, **Then** they can pick its type (Bool, Int, Float, String) and set a default value appropriate to that type.
2. **Given** an Int/Float/String parameter exists, **When** the developer saves and reloads the graph, **Then** the parameter's key, type, and default value are preserved.
3. **Given** a condition that compares an Int/Float/String parameter, **When** execution evaluates it against the live value, **Then** the branch is taken only when the comparison passes.
4. **Given** an action that sets an Int/Float/String parameter, **When** the node runs, **Then** the context value is updated and downstream conditions see the new value.
5. **Given** a choice gated by a non-boolean condition, **When** the condition fails at run time, **Then** that choice is excluded from the selectable list (consistent with existing Bool behavior).

---

### Edge Cases

- **End reason on a non-root End**: an End node inside a sub-graph terminates that sub-graph (pops to parent); its end reason applies to the sub-graph path, not the root run. (Out of scope to surface separately — behavior follows existing runner semantics.)
- **Sub-Graph self-reference**: assigning a graph as its own sub-graph, or any cycle in the sub-graph dependency chain, must be refused (reuses existing inter-graph cycle detection).
- **Sub-Graph with missing/null target** at run time: halts gracefully with a console message, no exception.
- **Typed parameter default parsing**: a malformed default value (e.g. non-numeric text for an Int) must fall back to a sensible default and warn, never crash the panel.
- **Condition reads a missing/wrong-typed key**: evaluates to false with a warning (consistent with existing Bool condition behavior), never throws.

## Requirements *(mandatory)*

### Functional Requirements

**US1 — End Reason**

- **FR-001**: When the selected node is an End node, the inspector MUST present an editable selector listing the available end reasons (Completed, Cancelled, Error) initialized to the node's current reason.
- **FR-002**: Changing the selector MUST update the End node's stored reason and mark the graph dirty.
- **FR-003**: The chosen end reason MUST persist across save and reload.
- **FR-004**: At run time, reaching an End node MUST report that node's configured end reason in the console (existing "Graph ended: {reason}" behavior).

**US2 — Sub-Graph node**

- **FR-005**: The canvas context menu MUST include an entry that creates a Sub-Graph node.
- **FR-006**: A Sub-Graph node MUST render with one input port and one output port and be connectable like any other node.
- **FR-007**: When a Sub-Graph node is selected, the inspector MUST expose an editable target-graph reference and an "inherit parent context" toggle.
- **FR-008**: The target-graph reference and inherit-context toggle MUST persist across save and reload, and the node's edges MUST remain intact on reload.
- **FR-009**: At run time, reaching a Sub-Graph node MUST run its target graph to completion and then resume the parent graph.
- **FR-010**: Assigning or connecting a Sub-Graph node such that its target graph references the parent (directly or transitively) MUST be refused and reported, preventing a cycle.
- **FR-011**: A Sub-Graph node with no target graph MUST halt execution gracefully with a console message, not an exception.

**US3 — Typed parameters**

- **FR-012**: The inspector parameter panel MUST allow declaring parameters of type Bool, Int, Float, and String, each with a key and a default value entered in a form appropriate to the type.
- **FR-013**: A parameter's key, type, and default value MUST persist across save and reload.
- **FR-014**: Conditions MUST be available that evaluate Int, Float, and String parameters against a configured comparison, returning false (with a warning) when the key is absent or mistyped.
- **FR-015**: Actions MUST be available that set an Int, Float, or String parameter value into the context.
- **FR-016**: Non-boolean conditions MUST integrate with existing routing and choice filtering identically to boolean conditions (failing conditions exclude their edge/choice).

**Cross-cutting**

- **FR-017**: All three capabilities MUST be implemented within the `com.faolline.graphTest` package without modifying `com.faolline.graphcore`.
- **FR-018**: Each capability MUST be covered by EditMode tests written before its implementation (TDD).

### Key Entities

- **End Reason selector**: inspector control bound to an End node's end-reason field (Completed / Cancelled / Error).
- **Sub-Graph node**: a graphTest node type wrapping graphcore's sub-graph node — holds a target graph reference and an inherit-context flag; one input and one output port.
- **Typed parameter**: a graph parameter with a key, a type (Bool / Int / Float / String), and a default value.
- **Typed conditions**: condition types that compare an Int / Float / String parameter from the context.
- **Typed actions**: action types that write an Int / Float / String value into the context.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can set an End node's reason to any of the three values, and a Run that reaches it reports exactly that reason — verified for all three reasons.
- **SC-002**: An End node's reason survives save/reload with 100% fidelity.
- **SC-003**: A parent graph with a Sub-Graph node runs the child graph end-to-end and resumes the parent, visible in the console as the child's nodes appearing between entering and leaving the Sub-Graph node.
- **SC-004**: A Sub-Graph node's target graph, inherit-context flag, and edges survive save/reload with 100% fidelity.
- **SC-005**: Assigning a recursive sub-graph reference is refused 100% of the time with a clear message and no cycle is created.
- **SC-006**: A developer can declare Int, Float, and String parameters and route execution conditionally on each, verified by a branch taken/not-taken according to the parameter value.
- **SC-007**: Int/Float/String parameters and their conditions/actions survive save/reload with 100% fidelity.
- **SC-008**: No regression: the full EditMode suite (including all prior features) passes after each user story is implemented.

## Assumptions

- The "user" is a developer using the graphTest editor window; this is a verification/test package, not an end-user product.
- `com.faolline.graphcore` already implements the underlying runtime (`EndReason` on End nodes, sub-graph entry/exit in the runner, inter-graph cycle detection, and generic `Set<T>`/`TryGet<T>` on the context) and requires no changes.
- Typed conditions use a simple, sufficient comparison model (e.g. equality and ordered comparison for numbers, equality for strings); a full expression language is out of scope.
- Default values are stored and edited as strings and parsed per type, consistent with the existing parameter model.
- Copy/paste and undo/redo of the new node/parameter types are out of scope, consistent with prior features.
- EditMode (NUnit / Unity Test Runner) tests only; no PlayMode tests, no MonoBehaviour, no UnityEvent.
- Sub-Graph authoring targets `TestGraph`/`BaseGraph` assets already present in the project.
