# Quickstart: In-Game Dialogue UI

How to put a dialogue on screen. Two paths — Canvas or UI Toolkit — share the same `DialogueDriver`.

## Prerequisites
- A `DialogueGraph` asset (e.g. the sample from *Faolline ▸ GraphDialogue ▸ Generate Sample Dialogue*).
- The `Speaker` assets referenced by the graph.
- Localization already set up (CSV or Unity Localization). The driver uses `LocalizationContext.Current`
  by default; text is resolved by the player before it reaches the UI.

## Path A — Canvas (UGUI + TextMeshPro)

1. Create a Canvas. Add:
   - A `TMP_Text` for the line, a `TMP_Text` for the speaker name.
   - A `choicesContainer` GameObject with up to N `Button`s (each with a child `TMP_Text`).
   - Empty `currentAvatarRoot` / `previousAvatarRoot` transforms for avatars (optional).
2. Add `CanvasDialogueView` to the Canvas; assign the texts, container, buttons, and avatar roots.
3. Add `DialogueDriver` to a GameObject; assign:
   - `graph` = your `DialogueGraph`,
   - `view` = the `CanvasDialogueView`,
   - the `Speaker` list,
   - `autoStart = true`.
4. Press Play. The first line shows; Space or clicking advance moves on; choice buttons appear at choice
   points; clicking (or pressing 1–9) selects.

## Path B — UI Toolkit (UIDocument)

1. Create a `UIDocument` with a UXML containing:
   - a `Label` named `line-text`, a `Label` named `speaker-name`,
   - a `VisualElement` named `choices-container`,
   - (Slots mode only) `Button`s named `choice-0`, `choice-1`, …
2. Add `UIToolkitDialogueView` to the same GameObject; assign the `UIDocument`, confirm the element
   names, and pick `ChoiceDisplayMode` (Dynamic creates buttons; Slots reuses the named ones).
3. Add `DialogueDriver`; assign `graph`, `view` = the `UIToolkitDialogueView`, speakers, `autoStart`.
4. Press Play — same interaction as Path A.

## Swapping front-ends
To switch a working scene between Canvas and UI Toolkit, change only the `view` reference on the
`DialogueDriver`. The graph, speakers, and driver settings are unchanged (SC-002).

## Scripting (manual control)
```csharp
[SerializeField] DialogueDriver driver;

void Begin()       => driver.StartDialogue(myGraph);
void OnNextButton()=> driver.Advance();
void Pick(string id)=> driver.Choose(id);
```

## Avatars
Assign a `Speaker`'s expression prefabs (key → prefab) and optionally a `FallbackExpression`. The view
spawns the avatar matching each line's `SpeakerId` + `ExpressionKey` at `currentAvatarRoot`, demoting the
previous speaker to `previousAvatarRoot`. Assign an `AvatarTransition` to animate swaps (optional).

## Notes
- Unavailable choices (failed conditions) appear greyed/disabled and cannot be selected.
- If no view is assigned, the dialogue still runs logically and logs a warning.
- Locale changes take effect on the next emitted step, not on the line already on screen.
