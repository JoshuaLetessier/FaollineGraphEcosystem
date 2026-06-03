# Dialogue UI — Samples

Wiring recipes for both front-ends. The C# (views + driver) is fully implemented and tested; scenes and
prefabs are assembled in the editor by following these steps (a few minutes each).

Prerequisites:
- A `DialogueGraph` asset — generate one via **Faolline ▸ GraphDialogue ▸ Generate Sample Dialogue**.
- Its `Speaker` assets (the sample creates `SampleSpeaker_Mayor`). Speakers are assigned **on the graph**
  (graph inspector → *Speakers*); line nodes then pick a speaker from a dropdown and the driver reads them.
- Localization set up (the sample auto-loads its CSV into `LocalizationContext`).

---

## Canvas (UGUI + TextMeshPro)

1. **Canvas** (UI ▸ Canvas). Under it:
   - `Speaker` — TextMeshPro - Text (UI).
   - `Line` — TextMeshPro - Text (UI).
   - `Choices` — empty GameObject (this is the `choicesContainer`); add a vertical layout group.
     - Add 4 child **Button**s, each with a child *TextMeshPro - Text* label.
   - *(optional)* `AvatarCurrent` and `AvatarPrevious` — empty RectTransforms for avatars.
2. Add **CanvasDialogueView** (to the Canvas) and assign: `lineText`, `speakerText`, `choicesContainer`,
   the `choiceButtons` list, and the two avatar roots.
3. Add **DialogueDriver** (any GameObject) and assign: `graph`, `view` = the CanvasDialogueView,
   `autoStart = true`. (No speaker list — the driver takes them from the graph.)
4. Press **Play**: the line shows; **Space** or a click advances; choice buttons appear; click or press
   **1–9** to choose.

---

## UI Toolkit (UIDocument)

1. **UIDocument** GameObject. Assign a **PanelSettings** and the provided
   `UIToolkit/DialogueView.uxml` (it defines `speaker-name`, `line-text`, `choices-container`).
   Add `UIToolkit/DialogueView.uss` to the document (or reference it from the UXML).
2. Add **UIToolkitDialogueView** to the same GameObject; assign the `UIDocument`. Defaults match the
   UXML element names. Pick `ChoiceDisplayMode`:
   - **Dynamic** — buttons are created at runtime in `choices-container`.
   - **Slots** — add `Button`s named `choice-0`, `choice-1`, … to the UXML and select Slots.
3. Add **DialogueDriver**; assign `graph`, `view` = the UIToolkitDialogueView, `autoStart` (speakers come
   from the graph).
4. Press **Play** — same interaction as Canvas.

---

## Swapping front-ends
Change only the `DialogueDriver.view` reference. The graph, speakers, and driver settings are identical.

## Avatars
Give each `Speaker` expression prefabs (key → prefab) and optionally a fallback. The view spawns the
avatar for each line's `SpeakerId` + `ExpressionKey` at the current mount and demotes the prior speaker
to the previous mount. Assign an `AvatarTransition` subclass to animate swaps.
