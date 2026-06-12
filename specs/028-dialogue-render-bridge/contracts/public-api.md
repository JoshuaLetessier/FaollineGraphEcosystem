# Public API Contract — dialogue render bridge

## dialoguesystem 0.3.0 — `Faolline.GraphDialogue`

```csharp
/// Runner-agnostic resolution of dialogue nodes into displayable steps. Works on the current node of ANY
/// runner (e.g. a gameflow host that embeds a dialogue subgraph), so a host can render dialogue without
/// owning a DialoguePlayer.
public sealed class DialoguePresenter
{
    public DialoguePresenter(
        ILocalizationProvider localization,
        ILocalizedAssetProvider assets = null,
        Func<string, Speaker> speakerLookup = null,
        LocalizationStrictMode strictMode = LocalizationStrictMode.Permissive);

    public LineStep   ResolveLine(DialogueLineNodeData line, BaseContext context);
    public ChoiceStep ResolveChoice(ChoiceNodeData choice, BaseContext context);
    public DialogueStep Resolve(BaseNodeData node, BaseContext context); // LineStep/ChoiceStep, or null if not a dialogue node

    public IReadOnlyList<string> MissingKeys { get; }
    public event Action<string> OnMissingKey;
}

// DialoguePlayer: public API UNCHANGED — now delegates resolution to an internal DialoguePresenter.
```

## gameflow 0.6.0 — `GraphFlowDriver` (`Faolline.GraphGameFlow`)

```csharp
/// Selects a choice branch on the running flow (no-op when not running). Mirrors Advance()/RaiseSignal().
public void ChooseById(string id);

// AutoAdvance behavior: when enabled, a ChoiceNodeData is NOT auto-advanced — it pauses for ChooseById.
// Non-choice nodes auto-advance exactly as before.
```

## Behavior contract

| Call | Result |
|------|--------|
| `presenter.ResolveLine(line, ctx)` | `LineStep` (speaker, localized+interpolated text, expression, voice) |
| `presenter.ResolveChoice(choice, ctx)` | `ChoiceStep` (options: label + availability per condition) |
| `presenter.Resolve(nonDialogueNode, ctx)` | `null` |
| enter `ChoiceNodeData`, `AutoAdvance=true` | pauses (no auto-pick) |
| `driver.ChooseById(optionId)` on a paused choice | advances along that branch |
| non-choice chain, `AutoAdvance=true` | auto-advances to the end (unchanged) |

## Compatibility

- **Additive**: a new presenter class + a new driver method; `DialoguePlayer` API unchanged (delegates). The one
  behavior change (AutoAdvance skips choices) is a verified-safe footgun fix.
- **Versioning**: dialoguesystem `0.2.0 → 0.3.0`; gameflow `0.5.0 → 0.6.0`. graphcore untouched.
- **Layering**: no dependency added between gameflow and dialoguesystem.
