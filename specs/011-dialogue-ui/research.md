# Phase 0 Research: In-Game Dialogue UI

## R1 — View contract shape (resolved-text model)

**Decision**: `IDialogueView` accepts the player's step objects directly and a speaker binding, and
raises a `ChoiceSelected(string choiceId)` event:
- `void BindSpeakers(IReadOnlyList<Speaker> speakers)`
- `void ShowLine(LineStep step)`
- `void ShowChoices(ChoiceStep step)`
- `void HideAll()`
- `event Action<string> ChoiceSelected`

**Rationale**: Steps already carry resolved strings (`LineStep.ResolvedText`, `ResolvedSpeakerName`,
`ChoiceOption.ResolvedLabel`). The view is a pure renderer — no localization, no key/table knowledge.
Passing the step objects (rather than loose strings) keeps the contract stable if more resolved fields
are surfaced later and lets the view read `ExpressionKey`/`SpeakerId` for avatars.

**Alternatives considered**:
- *Mirror the reference `IDialogueUI` 1:1* (SetActiveDialogue + raw keys): rejected — its `SetActiveDialogue`
  + table-name plumbing only exists because that system resolves localization in the UI. We resolve
  upstream, so that surface is dead weight.
- *Loose primitives (`ShowLine(string speaker, string text)`)*: rejected — loses `SpeakerId`/`ExpressionKey`
  needed for avatars and weakens future extensibility.

## R2 — Avatar lifecycle

**Decision**: Port the reference `DialogueUIAdapterBase` avatar logic into `DialogueViewBase`
(current/previous mounts, spawn → demote-to-previous → despawn, optional `AvatarTransition`,
`destroyAvatarOnHide`), but resolve the prefab through our existing `Speaker.TryGetExpression(key, out asset)`
and index speakers by `Speaker.SpeakerId`. Avatar selection uses `LineStep.SpeakerId` + `ExpressionKey`.

**Rationale**: The reference's avatar choreography is solid and tech-independent; reusing it avoids
reinventing it and keeps both views consistent. Our `Speaker` already exposes expression resolution with
a fallback, satisfying FR-012 (graceful degradation) directly.

**Alternatives considered**:
- *Per-view avatar code*: rejected — duplication; avatars are identical across Canvas/UI Toolkit.
- *Avatars as a separate component*: deferred (YAGNI) — base class is sufficient now.

## R3 — UI Toolkit choice rendering

**Decision**: Support two modes via an inspector enum, mirroring the reference: **Dynamic** (create one
`Button` per option in a container each `ShowChoices`, clear on next step) and **Slots** (reuse buttons
named `choice-0..choice-N` predefined in the UXML). Unavailable options → `SetEnabled(false)` + a
`disabled` USS class.

**Rationale**: Dynamic is zero-UXML-boilerplate; Slots gives designers full layout control and stable
focus order. Both are cheap to support and proven in the reference.

**Alternatives considered**: Dynamic-only — rejected (designers often want fixed, styled slots).

## R4 — Input strategy

**Decision**:
- **Pointer/click is primary and always available** — wired through UGUI `Button.onClick` and UI Toolkit
  `Button.clicked`. This needs no input package and satisfies advance + choose without any backend.
- **Keyboard is a convenience in `DialogueDriver`**: Space = advance, 1–9 = choose, implemented with both
  backends — `#if ENABLE_INPUT_SYSTEM` (Keyboard.current) and `#else`/`#if ENABLE_LEGACY_INPUT_MANAGER`
  (`UnityEngine.Input`) — exactly as the reference `DialogueManagerUserControlled`.
- The driver exposes plain methods `Advance()` and `Choose(string choiceId)` so input and buttons both
  funnel through one path (the same path tests drive).

**Rationale**: Clicks make the feature usable regardless of project input settings; keyboard mirrors the
reference for parity (FR-008). Funnelling through `Advance()`/`Choose()` gives a testable seam.

**Assembly reference nuance**: `Unity.InputSystem` is referenced by the UI asmdef; its code is fully
`#if ENABLE_INPUT_SYSTEM`-gated (Unity defines that symbol only when the package is present, which is the
default in Unity 6). Projects that fully removed the Input System package remove the reference; clicks +
legacy keyboard still work. This matches the reference package's shipped configuration.

## R5 — Assembly & dependencies

**Decision**: One assembly `com.faolline.graphdialoguesystem.UI` (Runtime, Editor-agnostic) referencing:
`com.faolline.graphdialoguesystem.Runtime`, `Unity.TextMeshPro`, `Unity.InputSystem`. UI Toolkit needs no
reference (engine module). `autoReferenced: true` so scene scripts see it. No `com.unity.localization`
reference — confirms the resolved-text model and keeps the optional-adapter rule satisfied.

**Rationale**: Keeps MonoBehaviours/UI out of the headless core (constitution), mirrors the
`Localization.Unity` sub-assembly pattern, and minimizes coupling.

**Alternatives considered**:
- *Split Canvas and UI Toolkit into two assemblies* (so TMP isn't pulled when only UI Toolkit is used):
  rejected for now (YAGNI) — one small assembly is simpler; can split later if TMP-free projects demand it.

## R6 — Testable seams (TDD)

**Decision**: Make `DialogueDriver` accept an `IDialogueView` and drive it through `Advance()`/`Choose()`.
EditMode tests use a **recording fake `IDialogueView`** + a real `DialoguePlayer` over an in-memory graph
to assert: line→`ShowLine`, choices→`ShowChoices`, end→`HideAll`, `Choose(id)` routes to the correct
branch, advance is ignored at a choice point, and `ChoiceSelected` funnels to `player.Choose`. Avatar
resolution is tested on `DialogueViewBase` via a seam that returns the resolved prefab/asset for a
speaker+expression without instantiating (or instantiates under a temp root and asserts/cleans up).

**Rationale**: Maximizes headless coverage of the logic per the constitution; leaves only pixel-level
rendering to samples/manual, which is the irreducible interaction part.

**Alternatives considered**: PlayMode-only — rejected (slower, weaker isolation, and the routing logic is
perfectly EditMode-testable through the fake view).

## Resolved unknowns

All Technical Context items are determined; no `NEEDS CLARIFICATION` remain. Open implementation-time
detail: exact UXML element names for the UI Toolkit sample (inspector-configurable; defaults
`line-text`, `speaker-name`, `choices-container`, slot prefix `choice-`).
