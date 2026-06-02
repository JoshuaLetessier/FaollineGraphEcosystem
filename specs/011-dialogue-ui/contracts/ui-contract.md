# UI Contract: In-Game Dialogue UI

Public surface introduced by `com.faolline.graphdialoguesystem.UI` (namespace `Faolline.GraphDialogue.UI`).
Consumes the existing runtime; introduces no runtime/core API change.

## IDialogueView

```csharp
public interface IDialogueView
{
    // Supply the speakers used to resolve avatars (indexed by Speaker.SpeakerId).
    void BindSpeakers(IReadOnlyList<Speaker> speakers);

    // Render a spoken line: resolved text + resolved speaker name; request the matching avatar.
    void ShowLine(LineStep step);

    // Render the choice options: one control per option; unavailable options are non-selectable.
    void ShowChoices(ChoiceStep step);

    // Clear all text, choices, and avatars.
    void HideAll();

    // Raised when the player selects an option; carries the option's ChoiceId (routing key).
    event System.Action<string> ChoiceSelected;
}
```

**Contract guarantees**
- Implementations MUST display `LineStep.ResolvedText` / `ResolvedSpeakerName` and
  `ChoiceOption.ResolvedLabel` verbatim — no localization performed in the view.
- `ShowChoices` MUST present exactly `step.Options.Count` options (subject to slot limits, see below),
  each non-selectable when `ChoiceOption.Available == false`.
- Selecting an option MUST raise `ChoiceSelected` with that option's `ChoiceId` (never an index).
- `HideAll` MUST leave no active choice controls and no spawned avatars.
- Notifications MUST use C# `event Action` (no `UnityEvent`).

**Slot-limited views (UI Toolkit Slots mode / fixed Canvas buttons)**
- If options exceed available slots, the surplus is not shown and a `[GraphDialogue]` warning is logged.

## DialogueDriver (control surface)

```csharp
public sealed class DialogueDriver : MonoBehaviour
{
    void StartDialogue(DialogueGraph graph); // (re)start; clears prior state/avatars
    void Advance();                          // advance the current line; no-op during choices
    void Choose(string choiceId);            // select an option by id
    void Back();                             // step back (player history)
    void BackToCheckpoint();                 // step back to last checkpoint
}
```

**Behaviour guarantees**
- Subscribes to `DialoguePlayer.OnLine/OnChoices/OnEnded/OnStuck` and forwards to the view.
- Subscribes to `IDialogueView.ChoiceSelected` and forwards to `Choose`.
- `Advance()` is ignored unless the current step is a line.
- `Choose(choiceId)` is ignored if the id is not a currently-available option.
- With no view assigned, runs the dialogue logically and logs a `[GraphDialogue]` warning (no throw).
- Unsubscribes and calls `HideAll` on disable/destroy.

## Keyboard mapping (driver convenience)

| Input | Action | Condition |
|-------|--------|-----------|
| Space / pointer "advance" | `Advance()` | current step is a line |
| Digit/Numpad 1–9 | `Choose(option[k-1].ChoiceId)` | current step is choices; option k exists and is available |

Pointer clicks on choice controls always work (independent of input backend). Keyboard is provided for
both the legacy Input Manager and the new Input System.

## Test contract (EditMode seams)

A recording fake `IDialogueView` + a real `DialoguePlayer` over an in-memory graph MUST be able to assert:
- line step → `ShowLine` with the expected resolved text;
- choice step → `ShowChoices` with the expected option ids/availability;
- `ChoiceSelected(id)` → player advances down that branch;
- end → `HideAll`;
- `Advance()` during choices → no state change.
