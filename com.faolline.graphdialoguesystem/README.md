# com.faolline.graphdialoguesystem

**Version**: 0.1.0 — **Unity**: 6000.x — depends on `com.faolline.graphcore`

A graph-based dialogue library built **entirely on top of** `com.faolline.graphcore` (zero core
changes), following the `com.faolline.starterGraph` package shape. Author branching, multi-speaker,
localized dialogues as a visual graph and play them back headlessly.

> MVP scope (this iteration): authoring + playable runtime, inline conditions/effects, localization
> across providers. See `specs/010-graphdialoguesystem-mvp/`.

---

## Architecture

```
com.faolline.graphdialoguesystem
├── Runtime/                         (refs graphcore.Runtime; NO external deps)
│   ├── DialogueGraph                BaseGraph subclass ([CreateAssetMenu])
│   ├── DialogueContext / Keys       BaseContext + typed bool/int/float/string (Principle VI)
│   ├── Nodes/DialogueLineNodeData   StatementNodeData + SpeakerKey/TextKey/ExpressionKey
│   ├── Choices/DialogueChoice       BaseChoice + DisplayTextKey
│   ├── Speakers/Speaker(+Expression) localizable name + key→asset expressions
│   ├── Conditions/                  Always T/F, Bool, Int, Float, String (+ ComparisonOperator)
│   ├── Actions/                     Log, Set Bool/Int/Float/String
│   ├── Localization/                ILocalizationProvider + CsvLocalizationProvider + Settings/Context
│   ├── Execution/                   DialogueLineExecutor + registry factory
│   └── Playback/                    DialoguePlayer + LineStep/ChoiceStep/EndStep + ChoiceOption
├── Localization.Unity/              OPTIONAL adapter (gated; compiles only if com.unity.localization present)
│   └── UnityLocalizationProvider    ILocalizationProvider over String Tables
├── Editor/                          graph view, 5 node views, edge view, inspector, window, sample, colors
└── Tests/EditMode/                  headless EditMode suite (Runtime + Editor)
```

Nodes reuse graphcore's built-ins (`StartNodeData`, `ChoiceNodeData`, `EndNodeData`,
`SubGraphNodeData`) unchanged; only `DialogueLineNodeData` and `DialogueChoice` are added.

---

## Quick start

1. `Assets > Create > GraphDialogue > Dialogue Graph`.
2. Double-click it to open the **Dialogue Graph Editor** (one window per asset).
3. Right-click the canvas → add Start / Line / Choice / SubDialogue / End nodes; connect them.
4. Select a Line node to set its **Speaker Key** and **Text Key**; select a Choice to add options
   with localized labels and optional per-option conditions.
5. Provide translations (see Localization) and press **Run** (set the locale field first).

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

## Localization — abstraction + 2 providers

`ILocalizationProvider` resolves `(key, locale) → string`, returning a defined `#key` fallback (with a
warning) when a key is missing — never empty.

- **`CsvLocalizationProvider`** (default, no external dependency): a CSV table whose header is
  `Key,locale1,locale2,…`.
- **`UnityLocalizationProvider`** (optional): in the isolated `Localization.Unity` assembly, compiled
  only when `com.unity.localization` is installed (`GRAPHDIALOGUE_UNITY_LOCALIZATION`). Projects that
  don't use Unity Localization take no dependency on it (Constitution v1.2.0).

`LocalizationSettings` / `LocalizationContext` select the active provider + locale, with a safe default
when unconfigured.

---

## Testing

EditMode-only, headless, test-first (Constitution Principle IV). Run via
**Window ▸ General ▸ Test Runner ▸ EditMode**, filter `Faolline.GraphDialogue.Tests`.
