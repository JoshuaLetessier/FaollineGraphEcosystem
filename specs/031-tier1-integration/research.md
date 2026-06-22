# Research: Tier-1 Integration Improvements

**Branch**: `031-tier1-integration` | **Date**: 2026-06-22

## R1: How PlayDialogueAction parks the flow

**Decision**: Use the existing `AwaitSignalName` mechanism on the host node.

PlayDialogueAction is a `BaseAction` (OnEnter). But BaseAction.Execute() only receives
a `BaseContext` — it can't set `AwaitSignalName` on the node because that's a serialized
field set at authoring time, not at runtime.

**Approach**: The action does NOT use AwaitSignalName. Instead, PlayDialogueAction works
as a **companion** to a node that already has `AwaitSignalName` set. The action starts
the dialogue player, and when the dialogue ends, it raises the signal that the node is
waiting for. The signal name is configurable on the action (defaults to a deterministic
name derived from the graph's GraphId).

**Alternative considered**: Making PlayDialogueAction a custom INodeExecutor instead of a
BaseAction. Rejected: executors are registered by NodeType string, so we'd need a custom
node type. Using BaseAction + AwaitSignal on the existing node is simpler and works with
any node type (Statement, SubGraph, etc.).

**Final pattern**:
1. Author places a Statement node with `AwaitSignalName = "dialogue_done"` (or auto-derived)
2. Author attaches `PlayDialogueAction` to OnEnter, referencing the DialogueGraph
3. At runtime: action.Execute → starts DialoguePlayer → player.OnEnded → context.RaiseSignal
4. The runner resumes automatically

This is clean, composable, and requires zero new node types.

## R2: DialogueBus — ambient event relay

**Decision**: A `static class DialogueBus` in graphdialoguesystem Runtime.

The bus relays all DialoguePlayer events so any UI can subscribe without holding a
reference to the player instance. It also exposes routing methods (Advance, Choose) so
the UI can drive the active dialogue.

**Contract**:
- `OnDialogueStarted(DialogueGraph)` — a new dialogue is playing
- `OnLine(LineStep)` — relayed from player
- `OnChoices(ChoiceStep)` — relayed from player
- `OnEnded(EndStep)` — relayed from player, also used by PlayDialogueAction to signal flow
- `OnStuck()` — relayed from player
- `Advance()`, `Choose(id)`, `RaiseSignal(name)`, `Tick(float)` — routes to active player
- `ActivePlayer` — the currently playing DialoguePlayer (null when idle)

**Single-active constraint**: Only one dialogue at a time. Starting a new one while one
is playing logs a warning and force-ends the previous (or queues — but YAGNI, single for now).

**Alternative considered**: An instance-based event relay (non-static). Rejected: the whole
point is removing the boilerplate of finding/referencing the player. Static bus is the
simplest answer for single-active dialogues.

## R3: Context Watch — accessing BaseContext from probes

**Decision**: Add `BaseContext ContextOf(IGraphRunProbe)` to a new editor-only registry,
rather than modifying IGraphRunProbe.

IGraphRunProbe is a frozen interface (constitution I). Adding a method would break existing
implementations. Instead:

**Approach**: A parallel static registry `GraphRunContextRegistry` (editor-only, alongside
GraphRunMonitor). When a runner starts, it registers its context keyed by its probe.
BaseRunner already creates a RunnerProbe and registers it — it can also register the context.
ReactiveEvaluator and FlowRunner do the same.

```csharp
#if UNITY_EDITOR
public static class GraphRunContextRegistry
{
    static Dictionary<IGraphRunProbe, BaseContext> _map;
    public static void Register(IGraphRunProbe probe, BaseContext ctx);
    public static void Unregister(IGraphRunProbe probe);
    public static BaseContext GetContext(IGraphRunProbe probe);
}
#endif
```

The Context Watch window: iterates `GraphRunMonitor.Probes`, calls
`GraphRunContextRegistry.GetContext(probe)` for each, lets the user pick which to watch.

**Alternative considered**: Making `_context` on BaseRunner internal/protected. Rejected:
violates encapsulation and creates a coupling the constitution discourages.

## R4: QuestEvaluator auto-evaluate — coalescing strategy

**Decision**: Immediate evaluate with re-entrancy guard (no frame-delayed batching).

Since QuestEvaluator is headless (no MonoBehaviour, no Update), there's no natural frame
boundary to coalesce against. Options:

1. **Immediate with guard**: On each context change, if not already evaluating, call
   Evaluate(). If a change fires during Evaluate() (because an action modifies context),
   set a dirty flag and re-evaluate once after the current pass.
2. **EditorApplication.delayCall**: Only works in editor, not at runtime.
3. **Coroutine-based**: Requires a MonoBehaviour host.

Option 1 is the only one that's headless and works everywhere.

**Subscription strategy**: Subscribe to ALL parameter/collection changes (wildcard), not
specific keys. Reason: BaseCondition subclasses are opaque — the evaluator can't know
which keys they read. Subscribing to all is O(1) setup and the evaluate itself is already
designed to be cheap (idempotent, early-exit on unchanged state).

**Implementation**:
```csharp
private bool _autoEvaluate;
private bool _evaluating;
private bool _dirtyDuringEvaluate;

public void EnableAutoEvaluate() {
    if (_autoEvaluate) return;
    _autoEvaluate = true;
    _context.OnParameterChanged(HandleAutoEvaluate);  // wildcard overload needed?
    _context.OnCollectionChanged(HandleAutoEvaluate);
}
```

**Problem**: BaseContext.OnParameterChanged takes a `string key` parameter — it's per-key,
not wildcard. Same for OnCollectionChanged. There's no "subscribe to all changes" mechanism.

**Resolution**: We need to add a wildcard subscription to BaseContext, OR use a different
approach. Looking at the BaseContext API:

- `OnParameterChanged(string key, Action<string> handler)` — per-key
- `OnCollectionChanged(string key, Action<string> handler)` — per-key

**Decision**: Add `OnAnyParameterChanged(Action<string> handler)` and
`OnAnyCollectionChanged(Action<string> handler)` to BaseContext. These fire for ANY key
change. This is a MINOR addition (append-only, no break). The handler receives the
changed key for filtering.

This also benefits the Context Watch window (subscribe once, repaint on any change).
