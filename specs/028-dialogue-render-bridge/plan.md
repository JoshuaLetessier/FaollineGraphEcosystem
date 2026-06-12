# Implementation Plan: dialogue render bridge (slice 9)

**Branch**: `028-dialogue-render-bridge` | **Date**: 2026-06-12 | **Spec**: [spec.md](spec.md)

## Summary

Two reusable bricks so a gameflow host can *render* an embedded dialogue subgraph in ~10 lines, without coupling
the libs. **dialoguesystem**: a runner-agnostic `DialoguePresenter` (extracts `BuildLineStep`/`BuildChoiceStep`/
`ResolveChecked`/`ResolveSpeakerName` + missing-key/strict handling); `DialoguePlayer` delegates to it (behavior
identical). **gameflow**: `GraphFlowDriver.ChooseById(id)` + `AutoAdvance` no longer auto-resolves a
`ChoiceNodeData` (pauses for a pick). Line pacing stays a consumer `AutoAdvance` toggle (no graphcore change).
graphcore untouched; dialoguesystem `0.2.0 → 0.3.0`; gameflow `0.5.0 → 0.6.0`; existing suites stay green.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Dependencies**: graphcore (`BaseRunner`, `BaseContext`,
`ChoiceNodeData`); dialoguesystem (`DialogueLineNodeData`, `ChoiceNodeData`, `LineStep`/`ChoiceStep`/
`ChoiceOption`, `ILocalizationProvider`, `ILocalizedAssetProvider`, `Speaker`, `DialogueLocalizationKeys`,
`DialogueTextInterpolator`, `LocalizationStrictMode`). **Testing**: NUnit EditMode — presenter resolves
line/choice for a node owned by a plain `BaseRunner`; player delegates (existing suite green); driver pauses on
a choice under AutoAdvance and `ChooseById` selects a branch; non-choice chain still auto-advances. **Constraints**:
graphcore untouched; dialoguesystem + gameflow append-only (new members + internal delegation + the choice-pause
refinement); `[GraphDialogue]`/`[GraphGameFlow]` prefixes; XML docs. **Scope**: new `DialoguePresenter.cs` +
`DialoguePlayer.cs` refactor (dialoguesystem) + `GraphFlowDriver.cs` two changes (gameflow) + tests + READMEs/
CHANGELOGs + two MINOR bumps + the stale dialogue README version header.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ PASS | graphcore untouched. dialoguesystem + gameflow additive MINORs; `DialoguePlayer` API unchanged (delegates); the only behavior change (AutoAdvance skips choices) is a verified-safe footgun fix. Existing suites stay green. |
| II. Universal Abstractions Only | ✅ PASS (host/domain layers) | The presenter is dialogue-domain (correct lib); the driver change is universal (a `ChoiceNodeData` is graphcore). No new domain vocabulary leaks down. |
| III. Specification-First | ✅ PASS | spec approved (16/16). |
| IV. Test-Driven Development | ✅ PASS | Tests-first: presenter resolution over an external runner; driver choice-pause + `ChooseById`; player unchanged. |
| V. Simplicity (YAGNI) | ✅ PASS | Extract-and-delegate (no new behavior in the player); two small driver changes; no graphcore `PauseForInput` flag (deferred); no UI view. |
| VI. Typed Context Contract | ✅ PASS | Presenter resolves against a `BaseContext` (works on the host's `GameFlowContext`). |
| VII. Cross-lib via SubGraph only | ✅ PASS | gameflow and dialoguesystem stay decoupled; the consumer composes the host runner + the presenter. |
| Dev standards | ✅ PASS | One class per file (new presenter); XML docs; prefixes. |

**Result**: PASS — no violations.

## Project Structure

```text
com.faolline.graphdialoguesystem/
├── package.json                                   # 0.2.0 → 0.3.0
├── README.md                                      # version header fix + presenter / hosted-render note
├── Runtime/Playback/DialoguePresenter.cs          # NEW — runner-agnostic resolver
├── Runtime/Playback/DialoguePlayer.cs             # MODIFIED — delegates resolution to a presenter
└── Tests/EditMode/…                               # presenter resolution tests

com.faolline.graphgameflow/
├── package.json                                   # 0.5.0 → 0.6.0
├── README.md / CHANGELOG.md                        # ChooseById + choice-pause
├── Runtime/Driver/GraphFlowDriver.cs              # MODIFIED — ChooseById; AutoAdvance skips choices
└── Tests/EditMode/GraphFlowDriverTests.cs         # choice-pause + ChooseById tests

# com.faolline.graphcore/ : UNCHANGED.
```

**Structure Decision**: `DialoguePresenter` holds `(ILocalizationProvider, ILocalizedAssetProvider, Func<string,
Speaker> speakerLookup, LocalizationStrictMode)` and the missing-key list; exposes `ResolveLine(DialogueLineNodeData,
BaseContext)`, `ResolveChoice(ChoiceNodeData, BaseContext)`, and a convenience `Resolve(BaseNodeData, BaseContext)`
returning a `LineStep`/`ChoiceStep` or `null` for a non-dialogue node, plus `MissingKeys`/`OnMissingKey`.
`DialoguePlayer` constructs an internal presenter from its own ctor args and its `BuildLineStep`/`BuildChoiceStep`
become calls into it (forwarding `OnMissingKey`/`MissingKeys`). In `GraphFlowDriver.HandleNodeCompleted`, the
auto-advance becomes `if (_autoAdvance && !(node is ChoiceNodeData)) _runner.Proceed();`, and a new
`ChooseById(string id)` calls `_runner.ChooseById(id)` under the running guard.

## Phase 0 — Research

See [research.md](research.md): R1 extract a presenter, player delegates (behavior-preserving); R2 the driver
pauses on `ChoiceNodeData` under AutoAdvance + exposes `ChooseById`; R3 line pacing stays a consumer AutoAdvance
toggle (no graphcore flag); R4 layering — the consumer is the integration point.

## Phase 1 — Design & Contracts

[data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD)

1. **Tests (test-first)**:
   - dialoguesystem: a `DialoguePresenter` resolves a `DialogueLineNodeData` (resolved speaker/text) and a
     `ChoiceNodeData` (options + availability) using a stub provider, for a node obtained from a plain
     `BaseRunner` driving a `DialogueGraph` — i.e. NOT owned by a `DialoguePlayer`. Confirm RED.
   - gameflow: a graph reaching a `ChoiceNodeData` under `AutoAdvance = true` stays paused at the choice (no
     auto-advance); `driver.ChooseById(optionId)` advances along that branch; the existing
     `AutoAdvance_RunsChainToEnd` (non-choice) stays green. Confirm RED (`ChooseById` missing / choice
     auto-advances).
2. **Implement**:
   - dialoguesystem: add `DialoguePresenter`; refactor `DialoguePlayer.BuildLineStep`/`BuildChoiceStep`/missing-key
     handling to delegate. Keep all public API. Confirm the dialogue suite GREEN.
   - gameflow: add `ChooseById`; gate auto-advance on `!(node is ChoiceNodeData)`. Confirm GREEN.
3. **Finalize**: full suite via batchmode (graphcore + graphstandard + dialoguesystem + gameflow EditMode green;
   PlayMode green); bump dialoguesystem `0.3.0` (fix README version header) + gameflow `0.6.0`; READMEs +
   CHANGELOGs; verify append-only + layering (no cross-lib dependency added).

## Complexity Tracking

> No violations — empty.
