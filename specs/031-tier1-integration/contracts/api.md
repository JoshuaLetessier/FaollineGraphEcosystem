# API Contracts: Tier-1 Integration Improvements

## 1. PlayDialogueAction (graphdialoguesystem)

```csharp
[CreateAssetMenu(menuName = "GraphDialogue/Actions/Play Dialogue")]
public sealed class PlayDialogueAction : BaseAction
{
    // Serialized
    public DialogueGraph DialogueGraph { get; set; }
    public string SignalName { get; set; }       // empty = auto "dialogue_done_{graphId}"
    public bool TitleFallback { get; set; }      // default true

    public override void Execute(BaseContext context);
}
```

### Execute contract:
1. If DialogueGraph is null → `[GraphDialogue]` warning, return (flow NOT parked)
2. Derive signal name: `SignalName` if non-empty, else `"dialogue_done_" + graph.GraphId`
3. Call `DialogueBus.Play(DialogueGraph, context, speakerLookup, onEnded)`
   - speakerLookup = `graph.FindSpeaker`
   - onEnded = `_ => context.RaiseSignal(signalName)`
4. The calling node MUST have `AwaitSignalName` set to the same signal name

## 2. DialogueBus (graphdialoguesystem)

```csharp
public static class DialogueBus
{
    // State
    public static DialoguePlayer ActivePlayer { get; }
    public static bool IsPlaying { get; }

    // Events (relayed from active player)
    public static event Action<DialogueGraph> OnDialogueStarted;
    public static event Action<LineStep> OnLine;
    public static event Action<ChoiceStep> OnChoices;
    public static event Action<EndStep> OnEnded;
    public static event Action OnStuck;

    // Play a dialogue through the bus
    public static void Play(
        DialogueGraph graph,
        BaseContext context,
        Func<string, Speaker> speakerLookup = null,
        Action<EndStep> onEnded = null,
        bool titleFallback = true);

    // Route input to active player (no-op when idle)
    public static void Advance();
    public static void Choose(string choiceId);
    public static void RaiseSignal(string name);
    public static void Tick(float deltaSeconds);

    // Force-stop the active dialogue (if any)
    public static void Stop();
}
```

### Play contract:
1. If IsPlaying → `[GraphDialogue]` warning, Stop() the current, then start new
2. Create DialoguePlayer with titleFallback
3. Subscribe to player events, relay to bus events
4. Call player.Start()
5. Fire OnDialogueStarted
6. When player ends: fire OnEnded, invoke onEnded callback, clear ActivePlayer

## 3. BaseContext additions (graphcore)

```csharp
// New methods on BaseContext (append-only, MINOR)
public void OnAnyParameterChanged(Action<string> handler);
public void OffAnyParameterChanged(Action<string> handler);
public void OnAnyCollectionChanged(Action<string> handler);
public void OffAnyCollectionChanged(Action<string> handler);
```

### Contract:
- `OnAnyParameterChanged` fires for EVERY `Set<T>(key, value)` call, with the key as arg
- `OnAnyCollectionChanged` fires for every `AddToCollection`, `RemoveFromCollection`, `ClearCollection`
- Multiple handlers supported (multicast)
- Thread safety: same-thread only (Unity main thread)
- Existing per-key subscriptions unchanged and fire BEFORE the wildcard

## 4. GraphRunContextRegistry (graphcore, editor-only)

```csharp
#if UNITY_EDITOR
public static class GraphRunContextRegistry
{
    public static void Register(IGraphRunProbe probe, BaseContext context);
    public static void Unregister(IGraphRunProbe probe);
    public static BaseContext GetContext(IGraphRunProbe probe);
}
#endif
```

## 5. ContextWatchWindow (graphcore, editor-only)

```csharp
// EditorWindow — no public API beyond the menu item
// Menu: Window → Faolline → Context Watch
```

## 6. QuestEvaluator additions (graphquest)

```csharp
// New methods on QuestEvaluator (append-only, MINOR)
public bool IsAutoEvaluateEnabled { get; }
public void EnableAutoEvaluate();
public void DisableAutoEvaluate();
```

### EnableAutoEvaluate contract:
1. Subscribes to `context.OnAnyParameterChanged` and `context.OnAnyCollectionChanged`
2. On each change: if not currently evaluating → Evaluate()
3. If change fires during Evaluate() → set dirty flag, re-evaluate once after
4. Does NOT handle timed objectives — consumer still calls `Evaluate(float now)` for timers
5. Idempotent: calling twice is a no-op

### DisableAutoEvaluate contract:
1. Unsubscribes from all wildcard handlers
2. Clears dirty flag
3. Idempotent
