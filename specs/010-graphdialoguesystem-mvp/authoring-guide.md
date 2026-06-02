# GraphDialogue — Authoring Guide

## 1. Core concepts

```
DialogueGraph (ScriptableObject)
  ├─ Nodes: Start → Line → Choice → SubGraph → End
  └─ Parameters (typed blackboard: bool, int, float, string)

Speaker (ScriptableObject)
  ├─ SpeakerId   — logical id used in line nodes
  └─ DisplayNameFallback — shown when the localization key cannot resolve

DialoguePlayer (runtime, headless)
  └─ Drives a graph, emits steps: LineStep | ChoiceStep | EndStep
```

---

## 2. Graph structure

### Minimal graph
```
Start → Line → End
```

### Branching
```
Start → Line (checkpoint) → Choice ──► Option A → End(Completed)
                                   └──► Option B → End(Cancelled)
```

### Sub-dialogue (reuse)
```
Parent:  Start → Line → SubGraph ─────► End
                           │
Child:   Start → Line → End
```
`SubGraph.InheritParentContext = true` shares the blackboard.
`= false` gives the child an isolated context (useful for re-usable sub-graphs).

---

## 3. Node title vs localization key

| Field | Purpose | Editable |
|-------|---------|---------|
| **Node Title** (`BaseNodeData.Title`) | Visual label on the canvas; used as the **source/default text** pre-filled into the localization table | Double-click or right-click → Rename |
| **Localization key** | Derived automatically from the node Id: `line_<nodeId>`, `choice_<choiceId>` | Never typed by hand |

> **Pattern**: give every line node a meaningful title ("Mayor greets hero") — the builder copies it as the default EN text in the String Table.

---

## 4. Key naming — node Ids

Node Ids are GUIDs by default (stable, auto-generated). For the **sample** or **deterministic** graphs, you can set them manually:

```csharp
var node = new DialogueLineNodeData
{
    Id      = "intro",    // → key "line_intro"
    Title   = "Welcome traveller.",
    SpeakerKey = "npc_mayor",
};
```

For runtime graphs, leave Ids as GUIDs — the localization builder handles them correctly.

---

## 5. Conditions & actions

### Built-in conditions (gate nodes or choices)
| Type | What it checks |
|------|---------------|
| `AlwaysTrueCondition` | Always passes |
| `AlwaysFalseCondition` | Always blocks |
| `BoolCondition` | `context[key] == expectedValue` |
| `IntCondition` / `FloatCondition` | numeric comparison (==, !=, <, <=, >, >=) |
| `StringCondition` | equality or negated equality |

### Built-in actions (enter / exit effects)
| Type | What it does |
|------|-------------|
| `LogAction` | Logs a debug message |
| `SetBoolAction` / `SetIntAction` / `SetFloatAction` / `SetStringAction` | Write a value to the context blackboard |

### Custom condition
```csharp
[CreateAssetMenu(menuName = "GraphDialogue/Conditions/HasItem")]
public class HasItemCondition : BaseCondition
{
    public string ItemId;
    public override bool Evaluate(BaseContext ctx)
        => Inventory.Instance.Has(ItemId);
}
```

### Custom action
```csharp
[CreateAssetMenu(menuName = "GraphDialogue/Actions/PlaySound")]
public class PlaySoundAction : BaseAction
{
    public AudioClip Clip;
    public override void Execute(BaseContext ctx)
        => AudioManager.Play(Clip);
}
```

---

## 6. Localization workflow

```
1. Build the graph — give nodes descriptive Titles
2. Run: Faolline ▸ Localization ▸ Build All Tables
   → LocalizationDatabase.asset updated
   → (if mode = UnityLocalization) String Tables created/updated under
     Assets/Localization/Collections/GraphDialogue/
3. Open Dashboard to verify coverage per lib
4. Translate empty entries in the String Table editor
5. Repeat from step 2 when new nodes are added
```

### Mode selection
Open **Faolline ▸ Localization ▸ Dashboard** → **Settings** button:
- **Csv** (default): use `CsvLocalizationProvider` — simple, no external package.
- **UnityLocalization**: com.unity.localization String Tables.

### Validation modes (build-time)
| Mode | Behaviour |
|------|-----------|
| `Permissive` | Silent — use during rapid iteration |
| `Warn` *(default)* | Log warnings for locale gaps |
| `Strict` | Log errors — use as pre-release gate |

### Strict modes (runtime)
| Mode | Behaviour |
|------|-----------|
| `Permissive` | Return `#key` silently |
| `Audit` *(default)* | Return `#key` + log + record in `DialoguePlayer.MissingKeys` |
| `Strict` | Throw `LocalizationException` |

---

## 7. Running a dialogue

```csharp
// 1. Setup
var player = new DialoguePlayer(
    graph,
    new DialogueContext(),
    new CsvLocalizationProvider(csvText, "en"),
    speakerId => speakerLookup[speakerId]
);

// 2. Subscribe
player.OnLine    += line    => ShowLine(line.ResolvedText, line.ResolvedSpeakerName);
player.OnChoices += choices => ShowChoices(choices.Options);
player.OnEnded   += end     => HideDialogueUI();

// 3. Start
player.Start();

// 4. Advance / choose
player.Advance();
player.Choose(choiceId);
player.Back();
player.BackToCheckpoint();
```

---

## 8. Save / restore sessions

```csharp
// Save (call when paused at a checkpoint)
player.OnLine += step => {
    var node = graph.FindNode(step.NodeId);
    if (node is { IsCheckpoint: true })
        PlayerPrefs.SetString("save", player.SaveState(graphGuid).ToJson());
};

// Restore on next session
var state = DialogueSessionState.FromJson(PlayerPrefs.GetString("save"));
if (state != null)
{
    var player = new DialoguePlayer(graph, new DialogueContext(), provider);
    player.RestoreFrom(state); // resumes from the checkpoint
}
else
    player.Start();
```

---

## 9. Patterns for large graphs

### Pattern 1 — Checkpoint-per-scene
Mark the first node of each scene as a checkpoint. Save on every checkpoint. Players resume cleanly without replaying long branches.

### Pattern 2 — Sub-dialogue for reusable scenes
Extract repeated conversations (tutorial tips, shop dialogue) into child graphs. Reference them via `SubGraphNode`. Use `InheritParentContext = false` to isolate sub-dialogue state.

### Pattern 3 — Parameter scoping
Declare all parameters on the graph with `ParameterData`. Use `DialogueContextKeys` constants — never raw strings — to avoid typos across conditions and actions.

```csharp
// DialogueContextKeys.cs
public static class DialogueContextKeys
{
    public const string Flag    = "Flag";
    public const string Counter = "Counter";
}

// In a condition asset
condition.ParameterKey = DialogueContextKeys.Flag; // never "flag" or "FLAG"
```

### Pattern 4 — Speaker naming
Follow `<domain>_<role>` for SpeakerIds:
```
npc_mayor    npc_guard    player    system_narrator
```
This maps cleanly to localization keys: `speaker_npc_mayor`, `speaker_player`, etc.

### Pattern 5 — Graph readability
- Set `Title` on every line node (the canvas label + source text for translators)
- Set `Title` on every choice (visible on the output port)
- Use node color overrides to group thematic sections
- Name sub-graphs descriptively: `DLG_TownIntro`, `DLG_ShopFlow`

---

## 10. Extending the localization system

Just implement `IGraphLocalizationAdapter` with a parameterless constructor — it is
**auto-discovered** via `TypeCache` (no registration call, no `[InitializeOnLoad]`):

```csharp
public sealed class MyGraphLocalizationAdapter : IGraphLocalizationAdapter
{
    public string LibName => "MyGraph";

    public void ScanAndIndex(LocalizationDatabase db)
    {
        // Find your graph assets and call db.GetOrCreateGraphEntry(...).AddKey(...) / db.AddGlobalKey(...)
    }
}
```

On *Build All Tables*:
- **Csv mode** → `Assets/Localization/Csv/MyGraph.csv`
- **UnityLocalization mode** → String Tables under `Assets/Localization/Collections/MyGraph/`

> Adapters that cannot be default-constructed can still be added manually via
> `GraphLocalizationAdapterRegistry.Register(...)`.

---

## 11. Showing dialogue in-game (Canvas / UI Toolkit)

The runtime player is headless; the `com.faolline.graphdialoguesystem.UI` assembly renders it on screen.
Because the player resolves localized text upstream, the views display resolved strings directly (no
localization dependency in the UI).

**Components**
- `IDialogueView` — the view contract (ShowLine / ShowChoices / HideAll / BindSpeakers + `ChoiceSelected`).
- `DialogueViewBase` — shared MonoBehaviour: speaker registry + avatar lifecycle.
- `CanvasDialogueView` — UGUI + TextMeshPro front-end.
- `UIToolkitDialogueView` — UIDocument front-end (Dynamic or Slots choices).
- `DialogueDriver` — drop-in: owns a `DialoguePlayer`, routes steps to the view, handles input.

**Minimal setup**: add a view (Canvas or UI Toolkit) + a `DialogueDriver`, assign the `DialogueGraph`,
the `Speaker` list, and the view. Press Play. **Space**/click advances; choice buttons (or **1–9**) select.

**Swap front-ends** by changing only `DialogueDriver.view`. See the step-by-step recipes in
`com.faolline.graphdialoguesystem/UI/Samples/DialogueUI/README.md` and the feature quickstart at
`specs/011-dialogue-ui/quickstart.md`.

**Scripting**
```csharp
[SerializeField] DialogueDriver driver;
void Begin()        => driver.StartDialogue(myGraph);
void OnNext()       => driver.Advance();
void Pick(string id)=> driver.Choose(id);   // or driver.ChooseByIndex(1..9)
```

**Avatars**: assign each `Speaker`'s expression prefabs (+ optional fallback); the view spawns the
current speaker's avatar and demotes the previous one. Assign an `AvatarTransition` to animate swaps.
