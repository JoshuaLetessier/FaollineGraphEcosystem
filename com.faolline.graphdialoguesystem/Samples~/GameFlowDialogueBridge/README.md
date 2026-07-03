# GameFlow Dialogue Bridge (sample)

Reuse a **standalone** dialogue view (Canvas or UI Toolkit, typewriter, auto-advance, choice timeout,
voice, history) for a dialogue **embedded in a flow** (`GraphFlowDriver` + a `SubGraph` node), instead of
hand-wiring `DialoguePresenter` from scratch per the README's minimal snippet.

## Why this is a sample, not a package

`com.faolline.graphdialoguesystem` and `com.faolline.graphgameflow` must not depend on each other
(ecosystem constitution — cross-lib composition happens only through `SubGraphNodeData` at the graph
level). This bridge necessarily references both, so it ships as source you import and own, not a shipped
assembly with a cross-lib dependency. **Requires both packages installed** in your project.

## What's in here

- **`GraphFlowDialogueSource.cs`** — an `IDialoguePlaybackSource` that wraps a `GraphFlowDriver` +
  `DialoguePresenter`: resolves each entered node, raises `OnLine`/`OnChoices`, suspends the driver's
  `AutoAdvance` while a dialogue segment is active (restored on exit), and forwards `Advance()`/
  `Choose(id)` to `driver.Advance()`/`driver.ChooseById(id)`.
- **`FlowDialogueBridge.cs`** — a `MonoBehaviour` mirroring `DialogueDriver`'s inspector shape (view,
  auto-advance, choice timeout, voice, speakers list) that wires a `GraphFlowDialogueSource` into the same
  `DialoguePlaybackController` `DialogueDriver` uses internally.

## Setup

1. Author your flow (`com.faolline.graphgameflow`) with one or more `SubGraph` nodes pointing at
   `DialogueGraph` assets, and add a `GraphFlowDriver` to a scene object.
2. Add a Canvas/UI Toolkit view (`CanvasDialogueView`/`UIToolkitDialogueView`) as you would for a
   standalone `DialogueDriver` — see the **Dialogue UI** sample.
3. Add `FlowDialogueBridge` to a scene object; assign the `GraphFlowDriver`, the view, and every `Speaker`
   any reachable dialogue subgraph uses (there's no single `DialogueGraph` asset here to read speakers
   from, unlike `DialogueDriver.ActiveSpeakers`). If the driver is a persistent cross-scene one (booted in
   another scene with *Persist Across Scenes*), leave the field empty — the bridge falls back to
   `GraphFlowDriver.Active` at Awake.
4. To let the player advance lines, wire a "Continue" button (or your input handler) to the bridge's
   public `Advance()` — same skip-typewriter-then-step behaviour as `DialogueDriver`.
5. Boot the flow (`GraphFlowDriver.Boot()` / `BootOnStart`). When the flow enters a dialogue node, the
   bridge shows it through your view; when it exits back to a non-dialogue node, the view clears and the
   flow's own `AutoAdvance` setting resumes.

## Combining with standalone dialogues

Nothing stops the same scene from also having ambient PNJ dialogues via plain `DialogueDriver` components
— `FlowDialogueBridge` and `DialogueDriver` are independent, and can even share the same view prefab
across NPCs and the scripted flow (though not the same view *instance* live at once, since only one
dialogue is on screen at a time).
