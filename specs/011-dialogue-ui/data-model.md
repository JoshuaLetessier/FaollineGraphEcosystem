# Data Model: In-Game Dialogue UI

All new types live in assembly `com.faolline.graphdialoguesystem.UI`, namespace
`Faolline.GraphDialogue.UI`. No runtime/core type is modified.

## Reused (from `com.faolline.graphdialoguesystem` runtime — unchanged)

- `DialoguePlayer` — events `OnLine(LineStep)`, `OnChoices(ChoiceStep)`, `OnEnded(EndStep)`, `OnStuck()`;
  methods `Start()`, `Advance()`, `Choose(string choiceId)`, `Back()`, `BackToCheckpoint()`.
- `LineStep { string NodeId; string SpeakerId; string ResolvedSpeakerName; string ResolvedText; string ExpressionKey }`
- `ChoiceStep { string NodeId; IReadOnlyList<ChoiceOption> Options }`
- `ChoiceOption { string ChoiceId; string ResolvedLabel; bool Available }`
- `EndStep { string NodeId; EndReason EndReason }`
- `Speaker` — `SpeakerId`, `DisplayNameFallback`, `bool TryGetExpression(string key, out UnityEngine.Object asset)`, `FallbackExpression`.
- `DialogueGraph`, `DialogueContext`, `ILocalizationProvider` (used to construct the player).

## New types

### `IDialogueView` (interface)
The presentation boundary the driver talks to.

| Member | Signature | Notes |
|--------|-----------|-------|
| BindSpeakers | `void BindSpeakers(IReadOnlyList<Speaker> speakers)` | Supplies speakers for avatar resolution. |
| ShowLine | `void ShowLine(LineStep step)` | Render resolved text + speaker; request avatar. |
| ShowChoices | `void ShowChoices(ChoiceStep step)` | Render one control per option; disable unavailable. |
| HideAll | `void HideAll()` | Clear text, choices, avatars. |
| ChoiceSelected | `event Action<string>` | Raised with the chosen option's `ChoiceId` (C# event, not UnityEvent). |

### `DialogueViewBase : MonoBehaviour` (abstract)
Shared, tech-independent behaviour for both views.

- **Serialized**: avatar mounts (`currentAvatarRoot`, `previousAvatarRoot`), `destroyAvatarOnHide`,
  optional `AvatarTransition transition`, `waitTransitions`, `verboseLog`.
- **State**: `Dictionary<string, Speaker>` by `SpeakerId`; `_currentAvatar`, `_previousAvatar`,
  `_currentSpeakerId`, `_currentExpressionKey`; active swap coroutine.
- **Behaviour**: `BindSpeakers` (index by `SpeakerId`); `RequestAvatarSwap(speakerId, expressionKey)`
  → resolve via `Speaker.TryGetExpression` (fallback to `FallbackExpression`), spawn at current mount,
  demote prior to previous mount, despawn, optional transition; `ClearAvatarsOnHide`.
- **Events**: `OnAvatarSpawned/Demoted/Despawned(GameObject)`.
- **Abstract**: `ShowLine`, `ShowChoices`, `HideAll` (implemented by concrete views), plus the
  `ChoiceSelected` event from `IDialogueView`.

### `CanvasDialogueView : DialogueViewBase, IDialogueView`
UGUI + TextMeshPro renderer.

- **Serialized**: `TMP_Text lineText`, `TMP_Text speakerText`, `GameObject choicesContainer`,
  `List<Button> choiceButtons` (each with a child `TMP_Text`).
- **ShowLine**: set `lineText`/`speakerText` to resolved strings, hide choices, `RequestAvatarSwap`.
- **ShowChoices**: hide line, show container, for each option enable a button, set its label, wire
  `onClick → raise ChoiceSelected(option.ChoiceId)`, `interactable = option.Available`; surplus buttons
  hidden.
- **HideAll**: clear texts, hide container/buttons, `ClearAvatarsOnHide`.

### `UIToolkitDialogueView : DialogueViewBase, IDialogueView`
UIDocument renderer with two choice modes.

- **Serialized**: `UIDocument document`; element names (`lineElementName`, `speakerElementName`,
  `choicesContainerName`); `ChoiceDisplayMode { Dynamic, Slots }`; `choiceButtonClass`,
  `choiceSlotPrefix`, `maxChoiceSlots`.
- **Binds** `Label` line/speaker and a `VisualElement` choices container (lazy, after document ready).
- **ShowChoices**: Dynamic → create `Button`s; Slots → reuse `choice-i`. Disabled state via `SetEnabled`
  + `disabled` class. `clicked → raise ChoiceSelected(choiceId)`.

### `DialogueDriver : MonoBehaviour`
The single drop-in orchestrator.

- **Serialized**: `DialogueGraph graph`; `MonoBehaviour viewBehaviour` (must be `IDialogueView`);
  speaker list or provider hookup; `bool autoStart = true`; input toggles.
- **Owns**: a `DialoguePlayer` built from `graph` + `DialogueContext` + an `ILocalizationProvider`
  (from `LocalizationContext.Current` by default) + a speaker lookup.
- **Wiring**: subscribes player events → `view.ShowLine/ShowChoices/HideAll`; subscribes
  `view.ChoiceSelected → Choose`.
- **Public control**: `StartDialogue(DialogueGraph)`, `Advance()`, `Choose(string choiceId)`,
  `Back()`, `BackToCheckpoint()`.
- **Input** (`Update`): Space → `Advance()` when on a line; digits 1–9 → `Choose` the k-th currently
  displayed option when on choices; both input backends; ignores advance during choices.
- **Lifecycle**: unsubscribes + `HideAll` on disable/destroy; clears state on restart.

### `AvatarTransition : MonoBehaviour` (abstract, optional)
Hook for animating avatar changes.

- `IEnumerator Spawn(GameObject avatar)`, `IEnumerator Despawn(GameObject avatar)`,
  `IEnumerator DemoteToPrevious(GameObject avatar, Transform previousRoot)`.
- Default: none assigned → instant swaps.

## Relationships

```
DialogueDriver ──owns──> DialoguePlayer ──emits──> LineStep/ChoiceStep/EndStep
      │                                                     │
      └──holds──> IDialogueView <───────────────────────────┘ (ShowLine/ShowChoices/HideAll)
                       ▲   │
                       │   └──raises──> ChoiceSelected(choiceId) ──> DialogueDriver.Choose ──> player.Choose
                       │
        DialogueViewBase (avatars, speakers)
            ▲                     ▲
   CanvasDialogueView    UIToolkitDialogueView
```

## Validation rules (from requirements)

- Unavailable `ChoiceOption` → non-interactable control (FR-004).
- Selection identifies the option by `ChoiceId` (FR-005).
- Advance ignored during a choice step (FR-007).
- Unknown speaker/expression → no avatar, no exception, use `FallbackExpression` if set (FR-012).
- Hide/end clears text, choices, avatars (FR-013).
- Driver tolerates a null view (logical run + warning) (FR-017).
- No UI dependency in the headless core (FR-014) — enforced by assembly boundary.
