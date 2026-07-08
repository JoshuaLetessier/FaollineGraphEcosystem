# com.faolline.graphdialoguesystem

**Version**: 0.17.0 — **Unity**: 6000.x — depends on `com.faolline.graphcore` ≥ 0.35.0, `com.faolline.graphlocalization` ≥ 0.6.0

A graph-based dialogue library built **entirely on top of** `com.faolline.graphcore` (zero core
changes), following the `com.faolline.starterGraph` package shape. Author branching, multi-speaker,
localized dialogues as a visual graph and play them back headlessly.

> MVP scope (this iteration): authoring + playable runtime, inline conditions/effects, localization
> across providers. See `specs/010-graphdialoguesystem-mvp/`.

---

## Installation

Recommended — install `com.faolline.graphcore`, then **Window ▸ Faolline ▸ Graph Ecosystem Modules**
and tick *Graph Dialogue System* (its dependencies `graphcore` + `graphlocalization` are added
automatically).

Direct git URL (**Package Manager ▸ + ▸ Add package from git URL**) — add all three, since UPM does
not auto-resolve git dependencies:

```
…?path=Assets/FaollineGraphEcosystem/com.faolline.graphcore#master
…?path=Assets/FaollineGraphEcosystem/com.faolline.graphlocalization#master
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphdialoguesystem#master
```

Depends on: `com.faolline.graphcore`, `com.faolline.graphlocalization`. See
[`../INSTALL.md`](../INSTALL.md).

---

## Architecture

```
com.faolline.graphdialoguesystem
├── Runtime/                         (refs graphcore.Runtime + graphlocalization.Runtime)
│   ├── DialogueGraph                BaseGraph subclass ([CreateAssetMenu]); owns its Speakers
│   ├── DialogueContext / Keys       BaseContext + typed bool/int/float/string (Principle VI)
│   ├── Nodes/DialogueLineNodeData   StatementNodeData + SpeakerKey + ExpressionKey (line key DERIVED)
│   ├── Choices/DialogueChoice       BaseChoice (label key DERIVED from the Id; Title = source text)
│   ├── Speakers/Speaker(+Expression) localizable name (key derived) + key→asset expressions
│   ├── Conditions/                  Always T/F, Bool, Int, Float, String (+ ComparisonOperator)
│   ├── Actions/                     Log, Set Bool/Int/Float/String
│   ├── Localization/                DialogueLocalizationKeys (derives keys from node/choice/speaker id)
│   ├── Execution/                   DialogueLineExecutor + registry factory
│   └── Playback/                    DialoguePlayer + LineStep/ChoiceStep/EndStep + ChoiceOption
├── UI/                              Canvas + UI Toolkit views, DialogueDriver, avatars (separate assembly)
├── Editor/                          graph view, node/edge views, inspector, window, sample, validator hook
└── Tests/                           headless EditMode + PlayMode suites
```

Nodes reuse graphcore's built-ins (`StartNodeData`, `ChoiceNodeData`, `EndNodeData`,
`SubGraphNodeData`) unchanged; only `DialogueLineNodeData` and `DialogueChoice` are added. **Localization**
(providers, build pipeline, dashboard, manifest) lives in the separate **`com.faolline.graphlocalization`**
package (a dependency): the player resolves text through its `ILocalizationProvider`, and keys are never
hand-typed — they are derived from node/choice/speaker identity by `DialogueLocalizationKeys`.

---

## Quick start

1. `Assets > Create > GraphDialogue > Dialogue Graph`.
2. Double-click it to open the **Dialogue Graph Editor** (one window per asset).
3. Right-click the canvas → add Start / Line / Choice / SubDialogue / End nodes; connect them.
4. Assign the graph's **Speakers** (graph inspector → *Speakers*). Select a Line node to pick its
   **Speaker** + **Expression** from dropdowns and set its **Title** (the source text); select a Choice
   to add options whose **Title** is the source label, with optional per-option conditions. Localization
   keys are derived automatically — never typed.
5. Build/provide translations (see Localization) and press **Run** (set the locale field first).
   Use **✓ Validate** to check the graph for structural issues.

Or generate a ready-made example: **Faolline ▸ GraphDialogue ▸ Generate Sample Dialogue**.

---

## Playback (headless)

```csharp
var player = new DialoguePlayer(
    graph,                       // DialogueGraph
    new DialogueContext(),       // typed blackboard
    new CsvLocalizationProvider(csvText, "en"),
    speakerId => LookupSpeaker(speakerId));   // optional

player.OnLine    += line   => Show(line.ResolvedSpeakerName, line.ResolvedText);
player.OnChoices += step   => Present(step.Options);   // each has ResolvedLabel + Available
player.OnEnded   += end    => Finish(end.EndReason);
player.OnStuck   += ()     => Warn();

player.Start();
// player.Advance();           // past a line
// player.Choose(choiceId);    // an available option
// player.Back();              // step back (restores shared state)
// player.BackToCheckpoint();
```

`DialoguePlayer` wraps graphcore's `BaseRunner` — sub-dialogue nesting, cycle detection, and history
come from the foundation.

### Rendering a dialogue owned by another runner (`DialoguePresenter`)

When a **host** runs the dialogue — e.g. a gameflow `GraphFlowDriver` that embeds a `DialogueGraph` as a
**SubGraph** of its host flow — the host's runner owns the cursor, so a `DialoguePlayer` cannot drive it. Use a
runner-agnostic **`DialoguePresenter`** to resolve the host runner's current node into the same steps:

```csharp
var presenter = new DialoguePresenter(localization, assets, speakerLookup);

driver.OnNodeEntered += node =>
{
    switch (presenter.Resolve(node, driver.Context))   // null for non-dialogue nodes
    {
        case LineStep line:   driver.AutoAdvance = false; Show(line); break;   // "continue" → driver.Advance()
        case ChoiceStep step: Present(step.Options);              break;       // pick → driver.ChooseById(optionId)
        default:              driver.AutoAdvance = true;           break;
    }
};
```

**Pacing lines** is driven by the **resolved step type, not the node type**: in the `LineStep` case set
`driver.AutoAdvance = false` (the host already knows it is rendering a line — no need to inspect
`DialogueLineNodeData`); your "continue" button calls `driver.Advance()`. A **choice** pauses on its own (the
driver does not auto-resolve a `ChoiceNodeData`); pick with `driver.ChooseById(optionId)`.

**Missing-key fallback to the authored `Title`**: pass `titleFallback: true` to the presenter so a missing
localization key shows the node/choice authored `Title` (the source text the table is derived from) instead of
the bare `#key` marker — handy before a table is exported or for an incomplete locale (Strict still throws,
Audit still records the key):

```csharp
var presenter = new DialoguePresenter(localization, speakerLookup: lookup, titleFallback: true);
```

`DialoguePlayer` itself now resolves through this presenter internally (unchanged for standalone dialogues).
The dialogue's outcome already flows through the **shared context** (SubGraph + `InheritParentContext`), so an
authored action on a line writes straight into the host's state — no bridge code.

Want the same view (typewriter, auto-advance, choice timeout, voice, history) `DialogueDriver` gives standalone
dialogues, but for a flow-embedded one? Import the **GameFlow Dialogue Bridge** sample (Package Manager ▸ this
package ▸ Samples ▸ Import) — it wraps the pattern above into a drop-in `FlowDialogueBridge` component built on
the same `IDialoguePlaybackSource`/`DialoguePlaybackController` `DialogueDriver` uses internally. It ships as a
sample (source you import), not a package dependency, since `graphdialoguesystem` and `com.faolline.graphgameflow`
must not depend on each other.

---

## Showing dialogue in-game (Canvas / UI Toolkit)

The `com.faolline.graphdialoguesystem.UI` assembly renders the headless player on screen. The player
resolves text upstream, so the views display resolved strings (no localization dependency in the UI).
A `DialogueDriver` owns the player, routes its steps to an `IDialogueView`, and handles input
(**Space**/click = advance, **1–9**/click = choose). Swap front-ends by changing only `DialogueDriver.View`.

**Common setup**
1. Generate a sample (**Faolline ▸ GraphDialogue ▸ Generate Sample Dialogue**) or author a `DialogueGraph`.
2. Assign the graph's **Speakers** (graph inspector → *Speakers*) — the driver reads them; no scene list.

**Canvas (UGUI + TextMeshPro)**
1. UI ▸ Canvas with TMP `Speaker` + `Line` texts, a `Choices` container (vertical layout) holding a few
   `Button`s (each with a TMP label), and optional `AvatarCurrent`/`AvatarPrevious` roots.
2. Add **CanvasDialogueView**; assign `lineText`, `speakerText`, `choicesContainer`, the `choiceButtons`
   list (+ avatar roots).
3. Add **DialogueDriver**; assign `graph`, `View` = the CanvasDialogueView, `autoStart`.

**UI Toolkit (UIDocument)**
1. UI Toolkit ▸ UI Document; assign a **PanelSettings** and a UXML defining `speaker-name`, `line-text`,
   `choices-container` (the importable sample ships `DialogueView.uxml`/`.uss`).
2. Add **UIToolkitDialogueView**; assign the `UIDocument`; `ChoiceDisplayMode` = *Dynamic* (runtime
   buttons) or *Slots* (`choice-0…N` in the UXML).
3. Add **DialogueDriver**; assign `graph`, `View` = the UIToolkitDialogueView, `autoStart`.

**Options** (on the view/driver): typewriter (+ skip), auto-advance, choice timeout, per-speaker name
color, `{key}` text variables, localized voice (Asset Tables + an `AudioSource`), backlog
(`CanvasDialogueBacklog`).

> Full step-by-step + the UXML/USS: import the **Dialogue UI** sample
> (Package Manager ▸ this package ▸ Samples ▸ Import) — see its `README.md`.

---

## Reactivity — inline only

There are **no** condition/effect node types. Reactivity is attached inline (graphcore's native model):

- **Conditions** (`BaseCondition` subclasses) on a choice option, an edge, or a node's entry
  conditions. All entry conditions must pass to enter a node; a false edge/option condition blocks
  that branch. Missing/mistyped keys evaluate to `false` with a `[GraphDialogue]` warning (never throw).
- **Effects** (`BaseAction` subclasses) on a node's enter/exit actions, reading/writing the typed
  `DialogueContext` blackboard by key.

Context keys live only in `DialogueContextKeys` (Principle VI — no raw literals at call sites).

---

## Localization

Localization is provided by the **`com.faolline.graphlocalization`** package (a dependency). The player
resolves every line/choice/speaker through its `ILocalizationProvider`, returning a defined `#key` fallback
(with a warning) when a key is missing — never empty.

- **CSV** (default, no external dependency) or **Unity Localization** String Tables (optional, gated by
  `GRAPHLOCALIZATION_UNITY_LOCALIZATION`) — selected project-wide in the localization settings asset.
- Keys are **derived** from node/choice/speaker identity (`DialogueLocalizationKeys`); a node/choice
  **Title** and a speaker **Display Name Fallback** are the source texts pre-filled into the tables.
- Build via **Faolline ▸ Localization ▸ Build All Tables**; review coverage in the dashboard.

See the `com.faolline.graphlocalization` README for the full workflow.

---

## Testing

- **Generic context support**: `DialoguePlayer` and the dialogue bus accept any `BaseContext` (not just
  `DialogueContext`), so a host can drive dialogues on its own blackboard without subclassing.
- **Drain warning**: the player logs a `[GraphDialogue]` warning when it hits `MaxDrainSteps` during
  auto-advance, preventing silent infinite loops in mis-wired dialogue graphs.

EditMode-only, headless, test-first (Constitution Principle IV). Run via
**Window ▸ General ▸ Test Runner ▸ EditMode**, filter `Faolline.GraphDialogue.Tests`.
