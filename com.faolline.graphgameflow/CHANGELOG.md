# Changelog

All notable changes to **com.faolline.graphgameflow** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.2.0]

### Added
- **Editor authoring** for gameflow, mirroring the StarterGraph editor:
  - `GameFlowGraph : BaseGraph` — a creatable graph asset (Assets ▸ Create ▸ GraphGameFlow ▸ Game Flow Graph);
    a `BaseGraph`, so the slice-1 `GraphFlowDriver` accepts it unchanged.
  - `GameFlowGraphEditorWindow` + `GameFlowGraphView` + node views (Start/Statement/Choice/SubGraph/End) +
    `GameFlowEdgeView`: a visual canvas to add/connect/move nodes (opens on double-click; Save + Validate
    toolbar), reusing graphcore's editor infrastructure.
  - `GameFlowNodeInspectorView`: edits a node's actions (drop in a Load Scene), conditions, checkpoint, and a
    **Flow** foldout for the await-signal name and wait duration; plus End / SubGraph / Choice sections.
  - `GameFlowSampleBuilder` (Faolline ▸ GraphGameFlow ▸ Create Reference Scene-Flow Sample): generates the
    runnable reference flow (start → load A → await "advance" → load B → end) as a `GameFlowGraph` asset.
- `[CreateAssetMenu]` on `LoadSceneAction` (Assets ▸ Create ▸ GraphGameFlow ▸ Actions ▸ Load Scene).

### Notes
- Additive (MINOR): graphcore + graphstandard untouched; the slice-1 runtime is unchanged and source-
  compatible. EditMode 659 green (654 + 5 new editor/data tests); the slice-1 8 PlayMode stay green.

## [0.1.0]

### Added
- Initial release of the orchestrator / host layer above `com.faolline.graphcore` — the adapter that runs
  the headless graph runtime inside a live Unity scene.
- **GraphFlowDriver** (`MonoBehaviour`): owns a shared `GameFlowContext`, boots and drives the Linear
  `BaseRunner`, forwards `Update`'s `deltaTime` to `Tick`, exposes `RaiseSignal`/`Advance`/`Stop`, and
  re-exposes the runner's lifecycle as C# events (`OnNodeEntered`/`OnNodeCompleted`/`OnEnded`/`OnStuck`/
  `OnWaitingForSignal`). All logic is in public methods (thin `Start`/`Update`/`OnDestroy`), so the wiring is
  EditMode-testable. Auto-advance and manual-advance both supported.
- **LoadSceneAction** (`BaseAction`, NOT a node type): loads a Unity scene (Single/Additive) when it runs,
  attachable to any node's enter or exit list. Resolves the loader from the running `GameFlowContext`.
- **ISceneLoader** seam + **UnitySceneLoader** (default): keeps all driver wiring and the full
  start → load A → await → load B → end reference flow in deterministic EditMode tests, with PlayMode
  reserved for the real Unity pump.
- **GameFlowContext** (`BaseContext` subclass) + **GameFlowContextKeys**: the shared blackboard (carries the
  scene loader), with clone overrides per the Typed Context Contract.

### Notes
- graphcore and graphstandard are unchanged; the existing 634-test EditMode suite stays green (654 with this
  package's 20 EditMode tests). PlayMode adds 2 tests proving the real `Start`/`Update` pump.
