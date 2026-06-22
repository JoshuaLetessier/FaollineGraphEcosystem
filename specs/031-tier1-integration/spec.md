# Feature Specification: Tier-1 Integration Improvements

**Feature Branch**: `031-tier1-integration`

**Created**: 2026-06-22

**Status**: Draft

**Input**: Benchmark-driven improvements — three features that unlock new classes of games
by reducing integration boilerplate and improving observability.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — PlayDialogueAction: zero-boilerplate dialogue from a gameflow (Priority: P1)

A game developer building a scene-driven RPG has a gameflow graph with NPC interaction
points. Today, launching a dialogue from a gameflow node requires ~40 lines of custom
MonoBehaviour code per NPC: instantiate a DialoguePlayer, subscribe to events, wire up
UI, and RaiseSignal when done. With PlayDialogueAction, the developer drags the action
onto a gameflow node's OnEnter list, assigns the DialogueGraph asset, and the flow
automatically parks, plays the dialogue, and resumes when it ends.

**Why this priority**: This is the #1 integration boilerplate reported in two dogfood
rounds. Every narrative game built on the ecosystem hits this wall.

**Acceptance Scenarios**:

1. **Given** a gameflow Statement node with a PlayDialogueAction referencing a DialogueGraph,
   **When** the runner enters the node, **Then** the action parks the runner via AwaitSignal,
   creates a DialoguePlayer, starts it, and raises OnDialogueStarted on the ambient bus.

2. **Given** a playing dialogue reaches an End node, **Then** the action raises the
   awaited signal on the context, the runner resumes, and OnDialogueEnded fires on the bus.

3. **Given** a dialogue emits a LineStep, **Then** the ambient DialogueBus.OnLine event
   fires with the step, so any subscribed UI can render it without knowing the player exists.

4. **Given** a dialogue emits a ChoiceStep, **Then** DialogueBus.OnChoices fires, and
   calling DialogueBus.Choose(id) routes to the active player.

5. **Given** no DialogueGraph is assigned (null), **Then** the action logs a
   `[GraphDialogue]` warning and does NOT park the runner (the flow continues).

6. **Given** the action is tested headlessly (EditMode), **Then** the full lifecycle
   (start → line → advance → end → signal) is verifiable without a scene.

---

### User Story 2 — Context Watch: live parameter/collection inspector (Priority: P2)

A game developer debugging a quest that won't unlock opens the graph editor and sees the
live cursor on the right node, but can't tell why the condition fails. They open
Window → Faolline → Context Watch, which shows every parameter and collection in the
active BaseContext in real-time. They see `forest_cleared = false` and immediately
understand the issue.

**Why this priority**: The live cursor (shipped) answers "where am I?"; Context Watch
answers "why am I stuck?" — the natural follow-up every user asks.

**Acceptance Scenarios**:

1. **Given** Play Mode with an active GraphFlowDriver, **When** the Context Watch window
   is open, **Then** it lists all typed parameters (key, type, value) and all collections
   (key, items) from the driver's context.

2. **Given** a parameter changes at runtime (e.g. gold goes from 100 to 90), **Then** the
   Context Watch updates within one editor repaint (not polling — event-driven via
   OnParameterChanged).

3. **Given** a collection changes (item added/removed), **Then** the watch updates via
   OnCollectionChanged.

4. **Given** no active context (Edit Mode or no driver running), **Then** the window shows
   an informational message, not an error.

5. **Given** multiple GraphRunMonitor probes are active, **Then** the user can select which
   context to watch (dropdown).

---

### User Story 3 — QuestEvaluator auto-evaluate: push instead of poll (Priority: P3)

A game developer has 5 active quests sharing one BaseContext. Today they call
`evaluator.Evaluate()` for each quest every frame in Update — 5 evaluations * 60fps =
300 calls/second, most of which change nothing. With auto-evaluate, each QuestEvaluator
subscribes to context changes and only re-evaluates when a relevant parameter or
collection actually changes.

**Why this priority**: Eliminates boilerplate AND improves performance. The seams
(OnParameterChanged, OnCollectionChanged) already exist in BaseContext.

**Acceptance Scenarios**:

1. **Given** a QuestEvaluator with EnableAutoEvaluate(), **When** a context parameter that
   a quest condition reads changes, **Then** the evaluator calls Evaluate() automatically
   and fires state-change events.

2. **Given** a collection key used by a CollectionContainsCondition changes, **Then** the
   evaluator re-evaluates.

3. **Given** a timed objective (TimeLimitSeconds > 0), **Then** auto-evaluate does NOT tick
   the timer — the consumer must still call Evaluate(float now) for time enforcement (the
   auto mode only handles parameter/collection triggers).

4. **Given** EnableAutoEvaluate() then DisableAutoEvaluate(), **Then** the subscriptions
   are cleaned up and no further auto-evaluations fire.

5. **Given** multiple rapid context changes in one frame (e.g. 3 parameters set), **Then**
   the evaluator coalesces into a single Evaluate() (not 3).

6. **Given** the evaluator is tested headlessly (EditMode), **Then** the full lifecycle
   (enable → context change → auto-evaluate fires events → disable) is verifiable.

---

## Functional Requirements

### FR-010: PlayDialogueAction
- A new `BaseAction` subclass in `com.faolline.graphdialoguesystem`
- References a `DialogueGraph` (serialized field)
- On Execute: parks the runner via AwaitSignal, creates and starts a DialoguePlayer
- On dialogue end: raises the signal to resume the runner
- Ambient `DialogueBus` (static) relays all player events (OnLine, OnChoices, OnEnded, OnStuck)
- DialogueBus exposes `Advance()`, `Choose(id)`, `RaiseSignal(name)`, `Tick(float)` for UI routing
- The action reads speaker lookup from the DialogueGraph.Speakers list
- CreateAssetMenu for editor discoverability

### FR-020: Context Watch Editor Window
- An `EditorWindow` in `com.faolline.graphcore` Editor assembly
- Menu: Window → Faolline → Context Watch
- Discovers active contexts via `GraphRunMonitor.Probes` (already tracks active runners)
- Needs a way to get the BaseContext from a probe — extend IGraphRunProbe or use a parallel registry
- Displays parameters sorted by key: Key | Type | Value
- Displays collections sorted by key: Key | Items (comma-separated)
- Event-driven refresh: subscribes to OnParameterChanged("*") and OnCollectionChanged("*")
  (or to GraphRunMonitor.Changed + full repaint)
- Repaint frequency: once per change, not per frame
- Works only in Play Mode — shows "Not playing" message in Edit Mode

### FR-030: QuestEvaluator auto-evaluate
- New public methods on `QuestEvaluator`: `EnableAutoEvaluate()`, `DisableAutoEvaluate()`
- Auto-evaluate subscribes to `context.OnParameterChanged` for ALL keys (the evaluator
  can't statically know which keys its arbitrary BaseCondition subclasses read)
- Auto-evaluate subscribes to `context.OnCollectionChanged` for ALL keys (same reason)
- Coalescing: set a dirty flag on change, evaluate on next access or via a deferred invoke
  (since there's no Update loop, evaluate immediately but guard against re-entrancy)
- Timer-based objectives: auto-evaluate does NOT call Evaluate(float now) — only Evaluate()
- DisableAutoEvaluate() unsubscribes and clears the dirty flag
- Property `IsAutoEvaluateEnabled` for inspection

## Non-Functional Requirements

- Zero breaking changes to existing APIs
- graphcore references nothing downstream (PlayDialogueAction lives in graphdialoguesystem)
- All new public API = MINOR version bump
- EditMode tests for all three features
- No MonoBehaviour in core runtime (PlayDialogueAction is a BaseAction)
- No UnityEvent — C# Action<T> only

## Assumptions

- A1: PlayDialogueAction uses the ambient LocalizationContext.Current for localization
- A2: Context Watch uses IMGUI (EditorGUILayout) for simplicity and compatibility
- A3: Auto-evaluate coalescing is immediate (no frame-delayed batching) — re-entrancy guard suffices
- A4: DialogueBus is static and single-active (one dialogue at a time); concurrent dialogues are out of scope
