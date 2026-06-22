# Quickstart: Tier-1 Integration Improvements

## PlayDialogueAction — Zero-boilerplate dialogue from a gameflow

### Before (40+ lines per NPC):
```csharp
public class VillageManager : MonoBehaviour
{
    [SerializeField] DialogueGraph guardianDialogue;
    [SerializeField] Speaker guardianSpeaker;

    public void TalkToGuardian()
    {
        var ctx = GraphFlowDriver.Active.Context;
        var player = new DialoguePlayer(guardianDialogue, ctx as DialogueContext,
            id => guardianSpeaker, titleFallback: true);
        player.OnLine += step => { /* update UI */ };
        player.OnChoices += step => { /* show choices */ };
        player.OnEnded += step => {
            // hide UI
            GraphFlowDriver.Active.RaiseSignal("dialogue_done");
        };
        player.Start();
        // also need: Advance button, Choose buttons, store player reference...
    }
}
```

### After:
1. In the graph editor, on the "Talk to Guardian" Statement node:
   - Set `AwaitSignalName` = `dialogue_done_guardian` (or leave empty for auto-derived)
   - Add `PlayDialogueAction` to OnEnter, assign the GuardianDialogue asset

2. Your UI code (once, scene-global):
```csharp
public class DialogueUI : MonoBehaviour
{
    void OnEnable()
    {
        DialogueBus.OnLine += ShowLine;
        DialogueBus.OnChoices += ShowChoices;
        DialogueBus.OnEnded += _ => HidePanel();
    }
    void OnDisable()
    {
        DialogueBus.OnLine -= ShowLine;
        DialogueBus.OnChoices -= ShowChoices;
        DialogueBus.OnEnded -= _ => HidePanel();
    }
    void ShowLine(LineStep step) { /* render */ }
    void ShowChoices(ChoiceStep step) { /* render buttons */ }
    public void OnAdvanceClicked() => DialogueBus.Advance();
    public void OnChoiceClicked(string id) => DialogueBus.Choose(id);
}
```

That's it. No per-NPC code. The UI subscribes once to DialogueBus; each NPC is just a
graph node with an action asset.

## Context Watch — live debugging

1. Open **Window → Faolline → Context Watch**
2. Enter Play Mode
3. The window shows all parameters and collections from the active runner's context
4. Change a parameter in-game → the watch updates instantly
5. Select a different probe from the dropdown if multiple runners are active

## QuestEvaluator auto-evaluate

### Before:
```csharp
void Update()
{
    mainQuestEvaluator.Evaluate();
    sideQuestEvaluator.Evaluate();
    timedQuestEvaluator.Evaluate(Time.time);
}
```

### After:
```csharp
void Start()
{
    mainQuestEvaluator.EnableAutoEvaluate();   // evaluates on context change
    sideQuestEvaluator.EnableAutoEvaluate();   // evaluates on context change
    // timed quest still needs explicit ticking for deadlines:
}
void Update()
{
    timedQuestEvaluator.Evaluate(Time.time);   // only the timed one
}
```

The non-timed quests now evaluate only when something actually changes — not 60x/second.
