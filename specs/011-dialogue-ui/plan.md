# Implementation Plan: In-Game Dialogue UI

**Branch**: `011-dialogue-ui` | **Date**: 2026-06-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-dialogue-ui/spec.md`

## Summary

Add a runtime presentation layer to `com.faolline.graphdialoguesystem` so a dialogue can be shown on
screen with no custom code. A `DialogueDriver` MonoBehaviour owns a headless `DialoguePlayer`, forwards
its `OnLine`/`OnChoices`/`OnEnded`/`OnStuck` events to an `IDialogueView`, and routes player input
(pointer + keyboard). Two interchangeable views render the same steps: a **Canvas** view (UGUI +
TextMeshPro) and a **UI Toolkit** view (UIDocument). A shared `DialogueViewBase` MonoBehaviour holds the
speaker/avatar lifecycle. Because `DialoguePlayer` resolves localized strings upstream, the UI displays
ready-to-show strings and takes **no localization dependency**. All of this ships in a new, separate
assembly so the headless core stays MonoBehaviour-free.

## Technical Context

**Language/Version**: C# / Unity 6000.0 (per ecosystem constitution)

**Primary Dependencies**: `com.faolline.graphdialoguesystem.Runtime` (player, steps, `Speaker`);
TextMeshPro (`Unity.TextMeshPro`) for the Canvas view; built-in UI Toolkit (`UnityEngine.UIElements`)
for the UI Toolkit view; `Unity.InputSystem` (gated) + legacy Input Manager for keyboard.

**Storage**: N/A (presentation only; no persistence in this feature)

**Testing**: Unity Test Framework. EditMode tests for the testable seams (driver event→view routing,
choice routing by id, avatar resolution, input→action mapping via injected seams). Interaction-heavy
rendering verified via the sample scenes / optional PlayMode.

**Target Platform**: Any Unity runtime platform; Editor 6000.0+.

**Project Type**: Unity package — a new UI sub-assembly of `com.faolline.graphdialoguesystem`.

**Performance Goals**: 60 fps; no per-frame heap allocations in steady state (avatar swaps and choice
rebuilds allocate only on step changes, which is acceptable).

**Constraints**: Headless core MUST stay free of UI dependencies and MonoBehaviours; the view contract
notifies via C# `Action`/`event` (no `UnityEvent`); choice selection by `ChoiceId`, never index.

**Scale/Scope**: Small — one contract, one base, two views, one driver, one optional transition helper,
plus two sample setups. No runtime API change to the existing player.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ PASS | No graphcore change. No change to existing dialogue runtime public API; UI only consumes it. |
| II. Universal Abstractions Only | ✅ PASS | UI is dialogue-domain; lives in graphdialoguesystem, not graphcore. graphcore untouched. |
| III. Specification-First | ✅ PASS | `spec.md` approved (checklist all-green) before this plan. |
| IV. Test-Driven Development | ✅ PASS (adapted) | EditMode tests written first for the logic seams. UI rendering itself is interaction-based → covered by samples / optional PlayMode, consistent with "EditMode tests only required for headless core". |
| V. Simplicity (YAGNI) | ✅ PASS | One shared base + two thin views + one driver. No extra patterns; reuses `Speaker`, steps, player as-is. |
| VI. Typed Context Contract | ✅ N/A | This feature adds no new `BaseContext` usage; it consumes resolved steps. |
| VII. Cross-lib via SubGraph only | ✅ N/A | No cross-lib dependency introduced. |
| Dev Standard: no MonoBehaviour/UnityEvent in **Runtime core** | ✅ PASS | MonoBehaviours live ONLY in the new UI assembly (its own runtime), never in the headless dialogue/graphcore core. View notifications use C# `event Action`. |
| Dev Standard: dependencies justified | ✅ PASS | TMP/UI Toolkit/Input are presentation essentials, justified in spec Dependencies. No `com.unity.localization` dependency (player resolves text). No ecosystem-lib deps. |
| Dev Standard: error prefix | ✅ PASS | UI uses the `[GraphDialogue]` prefix for logs (package convention). |

**Result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/011-dialogue-ui/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── ui-contract.md   # Phase 1 output (IDialogueView + driver surface)
└── checklists/
    └── requirements.md  # from /speckit-specify
```

### Source Code (repository root)

```text
com.faolline.graphdialoguesystem/
└── UI/
    ├── Runtime/
    │   ├── com.faolline.graphdialoguesystem.UI.asmdef   # refs Runtime + Unity.TextMeshPro (+ Unity.InputSystem gated)
    │   ├── IDialogueView.cs            # contract: ShowLine / ShowChoices / HideAll / BindSpeakers + OnChoiceSelected
    │   ├── DialogueViewBase.cs         # MonoBehaviour: shared speaker registry + avatar lifecycle
    │   ├── CanvasDialogueView.cs       # UGUI + TMP view
    │   ├── UIToolkitDialogueView.cs    # UIDocument view (Dynamic | Slots choice modes)
    │   ├── DialogueDriver.cs           # owns DialoguePlayer, wires events, routes input
    │   └── AvatarTransition.cs         # optional spawn/demote/despawn animation hook (abstract)
    └── Tests/
        └── EditMode/
            ├── com.faolline.graphdialoguesystem.UI.Tests.EditMode.asmdef
            ├── DialogueDriverRoutingTests.cs   # event→view routing, choice-by-id, advance gating
            ├── AvatarLifecycleTests.cs         # speaker resolution + graceful fallback (headless seam)
            └── DialogueViewContractTests.cs    # a recording fake view validates driver behaviour

com.faolline.graphdialoguesystem/
└── Samples~/DialogueUI/          # or Samples/ — Canvas + UI Toolkit demo scenes/prefabs
    ├── Canvas/
    └── UIToolkit/
```

**Structure Decision**: A single new UI assembly `com.faolline.graphdialoguesystem.UI` under
`com.faolline.graphdialoguesystem/UI/Runtime/`, mirroring how `Localization.Unity` is a sub-assembly of
the same package. MonoBehaviours and UI tech (UGUI/TMP/UI Toolkit) are confined here; the headless core
keeps zero UI knowledge. Both views and the driver share `DialogueViewBase`. Tests in a sibling EditMode
assembly. Samples isolated so they pull no weight into the runtime.

## Phase 0 — Research

See [research.md](research.md). Resolves: (1) view contract shape given resolved-text steps,
(2) avatar lifecycle reuse from the reference vs. our `Speaker.TryGetExpression`, (3) choice rendering
strategies for UI Toolkit (Dynamic vs Slots), (4) input backend strategy (pointer always-on; keyboard
across legacy + new Input System), (5) assembly references and dependency gating, (6) testable seams.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md): the UI types, their fields/relationships, and the reused runtime types.
- [contracts/ui-contract.md](contracts/ui-contract.md): `IDialogueView` surface + the driver's public
  control surface + the choice-selected notification contract.
- [quickstart.md](quickstart.md): how an integrator wires a Canvas or UI Toolkit dialogue in a scene.

## Complexity Tracking

No constitution violations — section intentionally empty.
