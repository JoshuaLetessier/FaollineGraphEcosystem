# Data Model: Tier-1 Integration Improvements

**Branch**: `031-tier1-integration` | **Date**: 2026-06-22

## New Entities

### PlayDialogueAction (graphdialoguesystem)

| Field | Type | Serialized | Description |
|-------|------|------------|-------------|
| _dialogueGraph | DialogueGraph | Yes | The dialogue to play |
| _signalName | string | Yes | Signal to raise when done (empty = auto-derived from graph id) |
| _titleFallback | bool | Yes | Use node Title when localization key is missing |

- Extends: `BaseAction` (graphcore)
- CreateAssetMenu: "GraphDialogue/Actions/Play Dialogue"
- Execute(BaseContext): starts DialogueBus.Play(), subscribes to end

### DialogueBus (graphdialoguesystem)

| Member | Type | Description |
|--------|------|-------------|
| ActivePlayer | DialoguePlayer (readonly) | Currently playing player, null when idle |
| OnDialogueStarted | event Action\<DialogueGraph\> | Fires when a dialogue begins |
| OnLine | event Action\<LineStep\> | Relayed from player |
| OnChoices | event Action\<ChoiceStep\> | Relayed from player |
| OnEnded | event Action\<EndStep\> | Relayed from player |
| OnStuck | event Action | Relayed from player |

- Static class (single-active dialogue)
- Methods: Play(graph, context, speakerLookup, onEnded), Advance(), Choose(id), RaiseSignal(name), Tick(float), Stop()

### GraphRunContextRegistry (graphcore, editor-only)

| Member | Type | Description |
|--------|------|-------------|
| _map | Dictionary\<IGraphRunProbe, BaseContext\> | Probe → context mapping |

- Static class, #if UNITY_EDITOR
- Methods: Register(probe, context), Unregister(probe), GetContext(probe)

### BaseContext additions (graphcore)

| Member | Type | Description |
|--------|------|-------------|
| OnAnyParameterChanged | Action\<string\> | Fires for ANY parameter key change |
| OnAnyCollectionChanged | Action\<string\> | Fires for ANY collection key change |
| OffAnyParameterChanged | Action\<string\> | Unsubscribe |
| OffAnyCollectionChanged | Action\<string\> | Unsubscribe |

### ContextWatchWindow (graphcore, editor-only)

- EditorWindow, IMGUI
- Menu: Window → Faolline → Context Watch
- State: selected probe index, scroll position
- Refresh: subscribes to GraphRunMonitor.Changed, repaints on change

### QuestEvaluator additions (graphquest)

| Member | Type | Description |
|--------|------|-------------|
| IsAutoEvaluateEnabled | bool (readonly) | Whether auto-evaluate is active |
| _autoEvaluate | bool (private) | Internal flag |
| _evaluating | bool (private) | Re-entrancy guard |
| _dirtyDuringEvaluate | bool (private) | Deferred re-evaluate flag |

- Methods: EnableAutoEvaluate(), DisableAutoEvaluate()

## Modified Entities

### BaseContext (graphcore) — MINOR

Append-only: add wildcard change subscription (OnAnyParameterChanged, OnAnyCollectionChanged).
Existing per-key subscriptions unchanged.

### BaseRunner (graphcore) — PATCH

Editor-only: in EditorWireProbe(), also register context with GraphRunContextRegistry.
In EditorUnwireProbe(), unregister. Zero runtime change.

## Version Bumps

| Package | Current | New | Reason |
|---------|---------|-----|--------|
| graphcore | 0.19.0 | 0.20.0 | OnAnyParameterChanged/CollectionChanged + GraphRunContextRegistry + ContextWatchWindow |
| graphdialoguesystem | 0.8.0 | 0.9.0 | PlayDialogueAction + DialogueBus |
| graphquest | 0.2.0 | 0.3.0 | QuestEvaluator.EnableAutoEvaluate() |
