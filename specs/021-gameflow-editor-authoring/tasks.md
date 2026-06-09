---
description: "Task list for 021-gameflow-editor-authoring (editor authoring for com.faolline.graphgameflow)"
---

# Tasks: gameflow editor authoring (slice 2)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD) for the testable surface — the creatable-asset attributes and the sample builder
(structure + run under the driver). Editor **views** (window / graph view / node views / inspector) are
validated by compiling and by the sample opening, mirroring the sibling package editors (research R5).
Batchmode (no `-quit`; re-run after source change; verify XML). Branch `021-gameflow-editor-authoring`
(stacks on 020). **graphcore + graphstandard UNTOUCHED; slice-1 gameflow runtime UNCHANGED.**

## Phase 1: Setup

- [ ] T001 Create `com.faolline.graphgameflow/Editor/com.faolline.graphgameflow.Editor.asmdef` (name `com.faolline.graphgameflow.Editor`; references `com.faolline.graphcore.Runtime` + `com.faolline.graphcore.Editor` + `com.faolline.graphgameflow.Runtime`; `includePlatforms: [Editor]`; `autoReferenced: false`).
- [ ] T002 Update `com.faolline.graphgameflow/Tests/EditMode/com.faolline.graphgameflow.Tests.EditMode.asmdef`: add references `com.faolline.graphgameflow.Editor` + `com.faolline.graphcore.Editor` (so the attribute + sample-builder tests can see the editor + graph types).

## Phase 2: Foundational (BLOCKS all user stories)

- [ ] T003 [P] Write `GameFlowGraphTests` in `com.faolline.graphgameflow/Tests/EditMode/GameFlowGraphTests.cs`: `GameFlowGraph` is assignable to `BaseGraph` (INV-1); `typeof(GameFlowGraph)` carries a `[CreateAssetMenu]` whose `menuName` is `"GraphGameFlow/Game Flow Graph"` (INV-2). Confirm RED.
- [ ] T004 [P] Add a `[CreateAssetMenu]` assertion to `com.faolline.graphgameflow/Tests/EditMode/LoadSceneActionTests.cs`: `typeof(LoadSceneAction)` carries a `[CreateAssetMenu]` whose `menuName` is `"GraphGameFlow/Actions/Load Scene"` (INV-2). Confirm RED.
- [ ] T005 Create `com.faolline.graphgameflow/Runtime/Graph/GameFlowGraph.cs` (`[CreateAssetMenu(menuName = "GraphGameFlow/Game Flow Graph", fileName = "NewGameFlowGraph")] public class GameFlowGraph : BaseGraph { }`, XML docs) and add `[CreateAssetMenu(menuName = "GraphGameFlow/Actions/Load Scene", fileName = "NewLoadSceneAction")]` to `Runtime/Scene/LoadSceneAction.cs`. Confirm T003/T004 GREEN.

## Phase 3: US3 — One-click runnable sample (Priority: P2) 🎯 the TDD core

> Sequenced first among the stories because it is the test-driven heart and depends only on Foundational
> (the graph asset + the scene action + graphcore's universal nodes) — not on the visual views.

**Goal**: a menu command generates the reference scene-flow as a runnable `GameFlowGraph` asset.

**Independent test**: the built graph matches the reference structure and, driven by a `GraphFlowDriver` with
a recording loader, walks A → await → B → end.

- [ ] T006 [P] [US3] Write `GameFlowSampleBuilderTests` in `com.faolline.graphgameflow/Tests/EditMode/GameFlowSampleBuilderTests.cs`: calling the builder produces a `GameFlowGraph` with exactly 1 Start + 3 Statements (Load A / Wait / Load B) + 1 End, 4 edges, the Wait node's `AwaitSignalName == "advance"`, and the two Load nodes each carrying one `LoadSceneAction` (scenes "A"/"B") (INV-3); driving that asset with a `GraphFlowDriver` + `StubSceneLoader` (auto-advance) records "A", parks on the await, then on `RaiseSignal("advance")` records "B" and ends (INV-4). Clean up created assets in `[TearDown]` (`AssetDatabase.DeleteAsset`). Confirm RED.
- [ ] T007 [US3] Create `com.faolline.graphgameflow/Editor/Samples/GameFlowSampleBuilder.cs`: static `[MenuItem("Faolline/GraphGameFlow/Create Reference Scene-Flow Sample")]`; a public `CreateSample()` that builds the reference `GameFlowGraph` (GUID node/edge ids, `AssetDatabase.GenerateUniqueAssetPath`, two `LoadSceneAction` sub-assets via `AddObjectToAsset`, saves, pings) and **returns the created `GameFlowGraph`** for testability. `[GraphGameFlow]` log; XML docs. Confirm T006 GREEN.

## Phase 4: US1 — Create and author a gameflow graph visually (Priority: P1)

**Goal**: a creatable graph + a window with node views/edges to author the universal node set.

**Independent test**: Create ▸ GraphGameFlow ▸ Game Flow Graph makes an asset; double-clicking opens the
window; adding nodes + edges persists them (validated by the sample opening + compile).

- [ ] T008 [P] [US1] Create `com.faolline.graphgameflow/Editor/Edges/GameFlowEdgeView.cs` (`: BaseEdgeView`), mirroring `StarterEdgeView`.
- [ ] T009 [P] [US1] Create the node views in `com.faolline.graphgameflow/Editor/Nodes/`: `StartNodeView.cs`, `StatementNodeView.cs`, `EndNodeView.cs`, `ChoiceNodeView.cs`, `SubGraphNodeView.cs` (each `: BaseNodeView` over the universal node type; ports per starter; styling via the shared USS — no inline CSS).
- [ ] T010 [US1] Create `com.faolline.graphgameflow/Editor/Graph/GameFlowGraphView.cs` (`: BaseGraphView`): `CreateNodeView` switch over the 5 universal `NodeTypeId`s → the views; `CreateEdgeView` → `GameFlowEdgeView`; `OnNodeCreated` designates the first `StartNodeData` as `EntryNodeId`; `BuildContextualMenu` adds "Add Start/Statement/Choice/SubGraph/End Node" at the cursor; `GetChoiceView`/`RemoveChoiceEdges` choice helpers (mirror starter).

## Phase 5: US2 — Configure scene loads and signal waits in the inspector (Priority: P1)

**Goal**: the inspector edits a node's actions (attach a Load Scene), await-signal, wait duration, conditions,
checkpoint, and per-type sections — without code.

**Independent test**: with a node selected, the inspector exposes the on-enter action list (drop a Load
Scene) and the await-signal / wait-duration fields, and editing them updates the node data.

- [ ] T011 [US2] Create `com.faolline.graphgameflow/Editor/Inspector/GameFlowNodeInspectorView.cs` (`: BaseNodeInspectorView`): `BindNode` calls `AddBaseNodeSection` (gives title/checkpoint/conditions/**on-enter & on-exit actions**) and adds a **Flow** foldout with bound PropertyFields for `_awaitSignal` and `_waitDuration`; plus End-reason (EndNode), SubGraph target+inherit (cycle-checked), and Choice label/condition/add-remove sections mirrored from `StarterNodeInspectorView`; `SetGraph`/`SetGraphView`. XML docs.
- [ ] T012 [US1] Create `com.faolline.graphgameflow/Editor/Window/GameFlowGraphEditorWindow.cs` (`: BaseGraphEditorWindow`): `[MenuItem("Faolline/Open GraphGameFlow Editor")]`; `[OnOpenAsset]` opening/focusing a window for a double-clicked `GameFlowGraph`; `CreateGraphView` → `GameFlowGraphView`; `CreateNodeInspectorView` → `GameFlowNodeInspectorView`; `OnGraphLoaded` wires the inspector's graph + graph-view; `PopulateToolbar` adds a **Validate** button (graphcore `GraphValidator`). (Depends on T010 + T011.)

## Phase 6: Polish & cross-cutting

- [ ] T013 Bump `com.faolline.graphgameflow/package.json` `0.1.0` → `0.2.0`.
- [ ] T014 Run the ENTIRE existing suite via batchmode: EditMode (654 slice-1+foundation + the new editor tests) green AND the slice-1 8 PlayMode green (graphcore/graphstandard + slice-1 runtime untouched, INV-5). Record totals.
- [ ] T015 [P] Update `com.faolline.graphgameflow/README.md` (an "Authoring in the editor" section: Create menu, window, inspector affordances, the sample) and `CHANGELOG.md` (`0.2.0` — GameFlowGraph + editor window/views/inspector + sample builder + LoadSceneAction Create menu).
- [ ] T016 [P] Verify `[GraphGameFlow]` prefix on editor logs, XML docs on public editor API, node-view styling via USS (no inline CSS), and confirm the generated sample opens in the window.

## Dependencies

- **Setup (T001–T002)** → everything.
- **Foundational (T003–T005)** → blocks all stories (the graph asset + the action attribute).
- **US3 (T006→T007)** depends only on Foundational — independent of the visual views; the TDD core.
- **US1 (T008–T010, T012)** and **US2 (T011)** are the editor views; T012 (window) depends on T010
  (graph view) + T011 (inspector) — so implement the inspector before the window.
- **Polish (T013–T016)** last.

## Parallel opportunities

- T003 ‖ T004 (different test files), then T005 implements both.
- T006 (US3 test) can be written in parallel with the US1/US2 view work.
- T008 ‖ T009 (edge view ‖ node views) — different files.
- T015 ‖ T016 in Polish.

## Implementation strategy

- **MVP = Setup + Foundational + US3**: a creatable graph + the one-click sample that runs under the driver.
  This alone makes gameflow usable hands-on (the user's core need) and is fully test-driven.
- **+ US1 + US2**: the visual window + inspector to author arbitrary flows by hand.
- **+ Polish**: 654 EditMode + 8 PlayMode still green, docs + version bump, prefix/XML/USS verified.

## Notes

- Only the new `com.faolline.graphgameflow/Editor/` tree + `Runtime/Graph/GameFlowGraph.cs` + the
  `LoadSceneAction` attribute + `package.json`/docs change. graphcore + graphstandard + the slice-1 gameflow
  runtime are UNTOUCHED.
- The editor mirrors `com.faolline.starterGraph/Editor` one-for-one (window/graph view/node views/inspector/
  edge view/sample builder), adapted to gameflow naming + the Load Scene affordance, reusing graphcore's base.
- Scene change stays an **action** dropped into a node's action list, never a node type (locked slice-1).
