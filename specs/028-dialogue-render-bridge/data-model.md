# Phase 1 — Data Model: dialogue render bridge

## DialoguePresenter — NEW (dialoguesystem, `Faolline.GraphDialogue`)

| Member | Kind | Description |
|--------|------|-------------|
| ctor `(ILocalizationProvider localization, ILocalizedAssetProvider assets = null, Func<string,Speaker> speakerLookup = null, LocalizationStrictMode strictMode = Permissive)` | NEW | Holds the providers + strict mode + a missing-key list. |
| `LineStep ResolveLine(DialogueLineNodeData line, BaseContext ctx)` | NEW | Resolved speaker name + localized/interpolated text + expression key + voice. |
| `ChoiceStep ResolveChoice(ChoiceNodeData choice, BaseContext ctx)` | NEW | Options with resolved label + availability (from each option's condition). |
| `DialogueStep Resolve(BaseNodeData node, BaseContext ctx)` | NEW | Convenience: a `LineStep`/`ChoiceStep` for a dialogue node, else `null`. |
| `IReadOnlyList<string> MissingKeys` / `event Action<string> OnMissingKey` | NEW | Missing-key tracking (moved from the player). |

## DialoguePlayer — MODIFIED (behavior-preserving)

| Member | Change |
|--------|--------|
| ctor | builds an internal `DialoguePresenter` from its args; forwards `presenter.OnMissingKey` to its own `OnMissingKey`; `MissingKeys` reads the presenter's. |
| `BuildLineStep` / `BuildChoiceStep` / `ResolveChecked` / `ResolveSpeakerName` | now call the presenter (removed or thin-delegating). No public API change. |

## GraphFlowDriver — MODIFIED (gameflow, `Faolline.GraphGameFlow`)

| Member | Change |
|--------|--------|
| `HandleNodeCompleted(node)` | `if (_autoAdvance && !(node is ChoiceNodeData)) _runner.Proceed();` (was: always Proceed when auto). |
| `ChooseById(string id)` | NEW public: `if (running) _runner.ChooseById(id);` (mirrors `Advance`). |

## Validation / invariants

- **INV-1**: `presenter.ResolveLine(line, ctx)` ⇒ a `LineStep` matching what `DialoguePlayer` emits for the same
  line/context/providers (speaker, text, expression, voice).
- **INV-2**: `presenter.ResolveChoice(choice, ctx)` ⇒ options with label + availability per option condition;
  `Resolve(nonDialogueNode, ctx)` ⇒ null.
- **INV-3**: Missing-key/strict behavior (permissive/audit/strict, `MissingKeys`, `OnMissingKey`) is identical
  whether via the presenter or the player.
- **INV-4**: `DialoguePlayer` public API + all existing dialogue tests unchanged (delegation only).
- **INV-5**: Under `AutoAdvance = true`, entering a `ChoiceNodeData` does NOT advance (pauses for a pick); a
  non-choice chain still auto-advances to the end.
- **INV-6**: `driver.ChooseById(id)` advances along the matching branch; no-op when not running.
- **INV-7**: graphcore untouched; dialoguesystem `0.2.0 → 0.3.0`; gameflow `0.5.0 → 0.6.0`; all suites green; no
  cross-lib dependency added (gameflow ⊥ dialoguesystem).
