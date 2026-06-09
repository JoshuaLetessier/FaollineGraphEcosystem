# Changelog

All notable changes to **com.faolline.graphgameflow** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

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
