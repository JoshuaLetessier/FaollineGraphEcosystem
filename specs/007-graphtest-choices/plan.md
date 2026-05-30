# Implementation Plan: GraphTest — Choices & ChooseById

**Branch**: `007-graphtest-choices` | **Date**: 2026-05-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-graphtest-choices/spec.md`

## Summary

Extend `com.faolline.graphTest` to author and execute choice nodes. Add a `TestChoice : BaseChoice`
(adds a `Label`), a `ChoiceNodeView` with dynamic output ports (one per choice, routed by choice Id),
an inspector section to add/remove/edit choices, and a "waiting for choice" pause in the editor's
execution loop with a Choose toolbar button that calls `runner.ChooseById(id)`. **No graphcore change
is required** — `ChoiceNodeData`, `BaseChoice`, and `BaseRunner.ChooseById` already exist.

---

## Technical Context

**Language/Version**: C# 9 (Unity 6000.x)

**Primary Dependencies**: `com.faolline.graphTest` (features 005, 006), `com.faolline.graphcore`

**Storage**: Unity `ScriptableObject` assets; choices serialized via `[SerializeReference]` on `ChoiceNodeData`

**Testing**: NUnit via Unity Test Runner (EditMode only)

**Target Platform**: Unity Editor

**Project Type**: Extension of an existing downstream lib — no new assembly definitions required

**Performance Goals**: Choose response in under 1 second; port add/remove reflects in the canvas in under one frame

**Constraints**: No `MonoBehaviour`; no `UnityEvent`; EditMode tests only; synchronous execution loop (no async); no graphcore modification

---

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I – Foundation Stability | ✅ Pass | Zero graphcore changes — uses existing `ChoiceNodeData`, `BaseChoice`, `ChooseById`. |
| II – Universal Abstractions Only | ✅ Pass | `TestChoice` (with `Label`) is a domain-specific subclass, stays in graphTest. |
| III – Specification-First | ✅ Pass | `spec.md` written and approved. |
| IV – Test-Driven Development | ✅ Pass | Red-Green-Refactor mandatory; tests confirmed failing via Coplay `run_tests` first. |
| V – Simplicity (YAGNI) | ✅ Pass | `TestChoice` has only a `Label`. Pause/resume reuses the existing synchronous loop with a flag. |
| VI – Typed Context Contract | ✅ Pass | Reuses `TestGameContext` from feature 006. No new context type. |
| VII – Cross-lib Compatibility | ✅ Pass | No dependency on other ecosystem libs. |

---

## Phase 0: Research

### R-001 — Pausing execution at a Choice node

- **Decision**: In `ExecuteGraph`'s loop, after each node is entered (state `NodeReady`), check `runner.CurrentNode is ChoiceNodeData`. If so, set `_waitingForChoice = true`, log `[GraphTest] Waiting for choice at node: {id}`, and break out of the loop without calling `Proceed()`. The Choose button later calls `runner.ChooseById(id)` and re-enters the same drain loop.
- **Rationale**: `BaseRunner` does not special-case choice nodes — calling `Proceed()` on a choice node auto-selects the first valid edge, which defeats explicit selection. The pause must therefore live in the editor loop, detecting the choice node via the existing `CurrentNode` property (added in feature 006).
- **Alternatives considered**: Adding a "pause at choice" flag inside `BaseRunner` — rejected (graphcore change, and the runner is intentionally agnostic about who decides the branch).

### R-002 — Port display label vs. routing key

- **Decision**: Each choice output port sets `port.portName = choice.Id` (the GUID), so graphcore's `HandleEdgeCreation` records `edgeData.PortName = choice.Id` and `ChooseById(choice.Id)` matches. The human-readable label is shown by overriding the port's connector `Label` text after creation (`port.Q<Label>(...)` → set text to `choice.Label`), leaving `portName` (routing) untouched.
- **Rationale**: `ChooseById` matches on `edge.Id || edge.PortName`. Routing by the choice GUID is collision-free; the label is presentation only. This needs no graphcore change because graphcore already reads `port.portName` for the edge's PortName.
- **Alternatives considered**: portName = label (rejected: labels collide, and FR-005 mandates Id routing); rewriting `edge.PortName` in `OnEdgeConnected` (rejected: the hook lacks the source-port reference).

### R-003 — Filtering choices by condition at runtime

- **Decision**: When paused, the Choose dropdown lists only choices where `choice.Condition == null || choice.Condition.Evaluate(_activeContext)`. If the filtered list is empty, log the stuck warning and halt (do not call ChooseById).
- **Rationale**: Mirrors `BaseRunner.SelectEdge` semantics (null condition = always available). Evaluating against the live `_activeContext` keeps the editor consistent with what the runner would do.

### R-004 — Dynamic ports on add/remove

- **Decision**: `ChoiceNodeView` exposes `RebuildPorts()` which clears `outputContainer` and recreates one port per choice. The inspector calls back into the view (via an event or a direct reference held by `TestGraphView`) when a choice is added/removed so the canvas updates without a full graph reload.
- **Rationale**: Choices live on the node data; the view must re-derive its ports from that list. A full `LoadGraph` would lose unsaved canvas state, so a targeted rebuild is preferred.

---

## Phase 1: Design & Contracts

### Data Model

See [data-model.md](data-model.md).

---

### Project Structure

#### Source Code

```text
com.faolline.graphTest/
├── Runtime/
│   └── Choices/
│       └── TestChoice.cs                 ← new; BaseChoice + Label
└── Editor/
    ├── Nodes/
    │   └── ChoiceNodeView.cs             ← new; dynamic output ports
    ├── Inspector/
    │   └── TestNodeInspectorView.cs      ← update: ChoiceNodeData section (add/remove/label/condition)
    ├── Graph/
    │   └── TestGraphView.cs              ← update: context menu "Add Choice Node" + CreateNodeView dispatch + port rebuild wiring
    └── Window/
        └── TestGraphEditorWindow.cs      ← update: _waitingForChoice flag, pause in ExecuteGraph, Choose toolbar button
```

No new assembly definitions. No graphcore changes.

---

### Key Design Decisions

**D-001 — `TestChoice : BaseChoice`**
Adds a single `[SerializeField] private string _label` with a public `Label`. Choices are created with a fresh GUID `Id` on add. This is the only domain-specific choice field.

**D-002 — `ChoiceNodeView` ports**
One input port `"in"` (`Port.Capacity.Multi`, typed `TestEdgeView`). One output port per choice (`Port.Capacity.Single`), `portName = choice.Id`, displayed label overridden to `choice.Label` (R-002). `RebuildPorts()` regenerates them; called after add/remove and on initial build.

**D-003 — Choice node creation & dispatch**
`TestGraphView.BuildContextualMenu` gains "Add Choice Node" (creates `ChoiceNodeData`). `CreateNodeView` adds a case: `ChoiceNodeData.NodeTypeId → new ChoiceNodeView(...)`. The view is held so the inspector can trigger `RebuildPorts()`.

**D-004 — Inspector choice section**
When the selected node is a `ChoiceNodeData`, `TestNodeInspectorView` renders: an "Add Choice" button (appends a `TestChoice` with default label, refreshes the view's ports), and per-choice a row with an editable label `TextField`, a condition `ObjectField` (`BaseCondition`), and a Remove button.

**D-005 — Execution pause/resume**
```
ExecuteGraph: after Start (and after each ChooseById), run DrainLoop().
DrainLoop:
  while State == NodeReady && !stuck && steps < max:
      if CurrentNode is ChoiceNodeData:
          _waitingForChoice = true
          _waitingChoiceNode = (ChoiceNodeData)CurrentNode
          Log "Waiting for choice at node: {id}"
          return                       ← pause, keep session alive
      Proceed(); steps++
  (existing stuck/ended logging)

Choose(choiceId):
  if !_waitingForChoice → Log "No active choice — click Run first."; return
  _waitingForChoice = false
  runner.ChooseById(choiceId)
  DrainLoop()                          ← resume
```

**D-006 — Choose button UX**
A "Choose" `ToolbarButton` opens a dropdown (`GenericMenu` or a small popup) listing the condition-passing choices' labels. Selecting one calls `Choose(choice.Id)`. When `!_waitingForChoice`, clicking logs the no-op message. GoBack clears `_waitingForChoice` (D-005 invariant from spec FR-012).

---

## Complexity Tracking

No constitution violations. No entries required.
