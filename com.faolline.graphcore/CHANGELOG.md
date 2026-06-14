# Changelog

All notable changes to **com.faolline.graphcore** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.13.3]

### Added
- **Toolbar grouping helpers.** `BaseGraphEditorWindow` gains a `PopulateToolbarRight(toolbar)` hook — for
  right-aligned *settings* (a language picker, etc.), called after the flexible spacer — alongside the existing
  left-aligned `PopulateToolbar` for *actions*, plus a `protected static ToolbarSeparator()` thin divider for
  grouping buttons by usage. The shared document tools (Save / Arrange / ↻ Refresh) are now followed by a divider
  so a lib's buttons read as a separate group instead of one crammed row. Default hooks are no-ops. +2 EditMode
  tests.

## [0.13.2]

### Added
- **Refresh the window without closing it: `Refresh()` + ↻ Refresh toolbar button.** `BaseGraphEditorWindow` gains
  a `Refresh()` method that rebuilds the canvas from the data (re-rendering nodes and re-routing edges, preserving
  layout + viewport) and then reloads the window's dynamic data via a `protected virtual OnRefresh()` override
  point — the "little things" read once when the toolbar/panels are built and which can go stale (a language list
  from the localization settings, an asset dropdown, etc.). It runs on every Save and from a new ↻ Refresh toolbar
  button shared by every graph editor. The canvas rebuild (previously buried in `SaveGraph`) now lives in the new
  `BaseGraphView.ReloadView()`; Save is wired as *persist-then-`Refresh()`*. Default `OnRefresh` is a no-op, so
  existing windows are unaffected. +3 EditMode tests.

## [0.13.1]

### Fixed
- **Node inspector no longer overlaps itself.** Two issues from the 0.12.0 shared-inspector refactor: (1) `BindNode`
  reset the panel via `ClearInspector()`, which rebuilds the *no-selection* content (parameters / speakers) — so a
  selected node painted that panel AND its own sections on top of each other. `BindNode` now clears WITHOUT
  rebuilding the no-selection content (only `BindNode(null)` shows it). (2) The panel is now a `ScrollView` instead
  of a plain `VisualElement`, so a node with more fields than the panel is tall SCROLLS instead of flex-shrinking
  its sections into one another. `Add`/`Clear` keep targeting the content (a `ScrollView`'s `contentContainer`),
  so call sites and `childCount` are unchanged.

## [0.13.0]

### Added
- **Value-type parameters.** `ParameterType` gains `Vector2`, `Vector3` and `Color` alongside the four primitives
  (Bool/Int/Float/String). `ParameterData` carries a typed default per new type (`Vector2Default` / `Vector3Default`
  / `ColorDefault`, plus `ParameterData.Vector2/Vector3/Color(...)` factories), the node inspector edits them with
  the matching UI Toolkit field (`Vector2Field` / `Vector3Field` / `ColorField`, swapped by `ParameterDataDrawer`),
  and `BaseContext` accepts them (`_supportedTypes` now allows the three Unity value types). They serialize natively
  (JsonUtility) and round-trip through `com.faolline.graphsave`. Object/GameObject references stay out (they need a
  stable-id scheme, deferred).

### Notes
- Additive / non-breaking. Existing assets and the string `DefaultValue` shim keep working; the runtime type set
  simply grew from 4 to 7. EditMode 725 / PlayMode 9.

## [0.12.0]

### Changed
- **Typed parameter defaults.** `ParameterData` stores its default in a field matching its `Type`
  (`BoolDefault`/`IntDefault`/`FloatDefault`/`StringDefault`, plus `ParameterData.Bool/Int/Float/String(...)`
  factories) instead of a single string parsed at run time — no parse-failure warnings, edit-time type safety.
  Legacy assets self-migrate (the old `_defaultValue` string is parsed into the typed field lazily on first
  read; the `DefaultValue` string property stays as a back-compat shim). `BaseContext.SeedFromGraph` reads the
  typed default directly (no parsing).

### Added
- **Shared node-inspector base.** The parameter panel, the graph state (`Graph` / `SerializedGraph` / `SetGraph`
  / `RefreshSerializedGraph` / `MarkGraphDirty`), the End and SubGraph node sections, and the bound-node refresh
  now live ONCE in `BaseNodeInspectorView` instead of being copy-pasted in every lib's node inspector. The
  parameter default is edited with a TYPED field (Toggle / Integer / Float / Text) that swaps with the chosen
  type, and a `ParameterDataDrawer` shows only the matching default field. A `BuildNoSelectionContent()` virtual
  (+ `LogContext` / `SubGraphSectionTitle` hooks) lets a lib add its own bits — the Dialogue inspector keeps its
  Speakers list and "SubDialogue" title. Each lib inspector now holds only its lib-specific node fields + Choice.

### Notes
- Additive / non-breaking (the string `DefaultValue` API and all existing assets keep working). Editor
  de-duplication; runtime 4-type cap (`BaseContext._supportedTypes`) unchanged. EditMode 724 / PlayMode 9.

## [0.11.0]

### Added
- **Edges route around nodes (editor)** — orthogonal edges now weave AROUND node boxes instead of passing under
  them, live: the obstacle-avoiding routing recomputes every repaint (incl. while you drag a node). A leg stays a
  clean elbow when it already clears everything; otherwise it is routed on the grid of the nodes' margin-inflated
  boundary lines with a shortest-path search that prefers few turns. The edge's own endpoint nodes are excluded
  (it legitimately touches them). New pure `OrthogonalEdgeRouter.RouteAvoiding(...)` (unit-tested);
  `OrthogonalEdgeControl` feeds it the sibling node boxes.
- **Run cursor pulses on Waiting too** — a node parked on a signal/timer now pulses (amber), like the live
  cursor (blue); other statuses stay static. `RunCursorColors.TryPulse`.

### Notes
- Additive (MINOR), editor-only. Live avoidance trades CPU per repaint for always-clean routing (the user's
  choice over a baked-on-Arrange variant). Tests green (EditMode 712 / PlayMode 9).

## [0.10.0]

### Added
- **Live in-game run cursor** — while the game is playing, the graph editor window highlights the running graph
  the way the Animator window highlights the active state, as a full per-node state map: the live cursor
  (pulsing blue), the visited trail, sub-graph parents (the node whose sub-graph is currently running stays lit
  instead of going dark), and an end marker. Engine-agnostic editor seam in graphcore Runtime, compiled out of
  player builds (`#if UNITY_EDITOR`, zero footprint): `IGraphRunProbe` (`StatusOf(graph, nodeId)` +
  `ActiveNodeId(graph)`), a static `GraphRunMonitor` (probe registry + `Changed` event), and the
  `GraphRunNodeStatus` vocabulary (Running/Waiting/Active/Visited/Ended/Locked/Available/Completed).
  `BaseRunner` self-registers a probe while playing (reading its frame stack + history), so EVERY host that
  drives a runner — gameflow, dialogue, custom — lights the canvas up for free. The window survives Play thanks
  to the 0.9.0 persistence fix. `BaseRunner.CurrentGraph` exposes the active frame's graph.
- Editor: `BaseGraphView` paints the displayed graph's nodes from the probes and pulses the cursor;
  `BaseNodeView.SetRunCursor` / `PulseRunCursor` colors the node border (palette centralised in
  `RunCursorColors`). Reactive and Flow engines in graphstandard register their own probes (Locked/Available/
  Completed maps), so those paradigms light up too.

### Notes
- Additive (MINOR). Runtime untouched in player builds; the whole seam is editor-only. Tests stay green
  (EditMode 709 / PlayMode 9).

## [0.9.0]

### Added
- **Editor window survives domain reloads** — the open graph is now remembered across a domain reload (entering
  Play, a script recompile, or reopening Unity with the window docked). Previously the canvas came back **blank**
  and you had to close and reopen the window; the window now reloads the graph into the rebuilt view via a
  serialized reference (`BaseGraphEditorWindow._persistedGraph`).
- **Auto-save (editor)** — the canvas is persisted automatically before the window or editor closes and before a
  domain reload, so node/group moves (which are only synced into the data on save) are no longer lost. New
  `BaseGraphView.AutoSave(bool writeToDisk)` syncs the live layout into the data and marks it dirty; the window
  flushes it to disk on a genuine close (`OnDestroy`) and on editor quit (`EditorApplication.quitting`), and
  syncs (dirty-only) on teardown / `ExitingEditMode` so nothing is dropped across a reload.

### Notes
- Additive (MINOR). Runtime untouched; the lifecycle wiring lives entirely in `BaseGraphEditorWindow` /
  `BaseGraphView`, so every package's window (gameflow, dialogue, …) gets both behaviors for free.
- The viewport (zoom/pan) is not yet persisted across a reload — the graph reloads but the camera may reset.

## [0.8.0]

### Added
- **Malleable edges (editor)** — graph edges now render as right-angle (orthogonal) polylines you can shape:
  **double-click** an edge to add a bend point, **drag** the white dots to move them, **right-click** a dot to
  remove it. Backed by `BaseEdgeData.Waypoints` (`List<Vector2>`, additive editor-metadata like
  `BaseNodeData.Position`; persisted with the graph; no runtime effect). Implemented entirely in the shared
  `BaseEdgeView`/`OrthogonalEdgeControl` + `OrthogonalEdgeRouter` (pure, unit-tested), so every package's graph
  window (gameflow, dialogue, …) gets it for free. Edge routing reuses Unity's `EdgeControl` coordinate handling
  (render points replaced, not drawn over) and extends the pick bbox to keep bent edges selectable.

- **Auto-arrange (editor)** — a **Arrange** toolbar button tidies the whole graph into a left-to-right layered
  layout: longest-path layering on the cycle-broken DAG (looping shells don't hang), a barycenter pass to reduce
  edge crossings, and columns centered vertically. Column-skipping edges are routed through a lane **below** the
  rows (auto-generated bend points) so they don't pass under intermediate nodes. Backed by the pure, unit-tested
  `GraphAutoLayout` (`Arrange` + `RouteLongEdges`); applied by `BaseGraphView.ArrangeGraph` (rebuilds + frames).

### Notes
- Additive (MINOR). Runtime untouched except the additive `Waypoints` field; existing suites stay green.
- **Known limitation**: the live preview while editing waypoints can lag the data; a **Save (Ctrl+S)** fully
  refreshes the routing (the graph view reloads, preserving the viewport). A toolbar hint documents this.
- Auto-arrange routes column-skipping edges below the rows; full obstacle-avoidance (weaving *around* arbitrary
  nodes) is still out of scope — edges between far columns use the lane, not a per-node detour.

## [0.7.0]

### Added
- **Guarded await — re-armable resume gate.** `BaseNodeData.ResumeConditions` (optional `List<BaseCondition>`,
  default empty): a parked await node now resumes on a matching `RaiseSignal` **only if all resume conditions
  pass** (AND; null entries skipped). A name match with a failing gate is **ignored and the node stays parked**
  (re-armable) — the actor can raise again once the context satisfies the gate. This expresses "press the button
  anytime, it only acts when the world is ready" *in the graph*, the re-arm semantics that gating an outgoing
  edge cannot give (the edge consumes the signal and gets stuck on a false gate). A direct host `Advance`/GoTo
  override is not gated.

### Notes
- Additive (MINOR); append-only. `AwaitSignalName`/`EntryConditions`/`RaiseSignal` and all other members
  unchanged; an await node with no resume conditions behaves byte-for-byte as before. From round-4 dogfooding
  (a consumer had to hand-wire `if (IsExitOpen) RaiseSignal("exit")` — now expressible as a resume gate).

## [0.6.0]

### Added
- **Timed waits**: `BaseNodeData.WaitDuration`, `BaseRunner.Tick(deltaSeconds)`,
  `RunnerState.WaitingForTime`, `OnWaitingForTime`. The host feeds elapsed time; a node holds until its
  duration elapses. Append-only.

## [0.4.0]

### Added
- **Signals** (host → runtime): `BaseNodeData.AwaitSignalName`, `BaseRunner.RaiseSignal`(+ scalar payload),
  `RunnerState.WaitingForSignal`, `OnWaitingForSignal`; a `BaseContext` signal channel
  (`RaiseSignal`/`OnSignal`/`OffSignal`/`TryGetLastSignal`, `SignalArgs`).
- **Collections**: named string-sets on `BaseContext`
  (`AddToCollection`/`RemoveFromCollection`/`CollectionContains`/`CollectionCount`/`GetCollection`/
  `ClearCollection`/`OnCollectionChanged`/`GetAllCollections`), deep-cloned by `DeepClone`. Append-only.

## [0.3.0]

### Added
- **Global + local execution contexts**: a sub-graph can ride the parent context with a fresh local overlay
  (`BeginLocalContext`/`EndLocalContext`; `SubGraphNodeData.OpensScope`). Local writes are discarded when the
  scope ends. Append-only on `BaseContext`/`BaseRunner`.

## [0.2.0]

### Added
- Collapsible **node groups** on the canvas (`GraphGroupData`, `BaseGroupView`).
- Reusable **GraphValidator** (Editor) + menu *Faolline ▸ Graph ▸ Validate Selected Graph*:
  flags missing/duplicate Start, invalid `EntryNodeId`, edges to/from missing nodes, isolated
  nodes, choices without options, and options with no outgoing edge.

### Fixed
- Node color is restored after a drag (UIElements timing); changing the color auto-enables the
  color override.

## [0.1.0]

### Added
- Data layer: `BaseGraph`, `BaseNodeData`, `BaseEdgeData`, built-in node types (Start/Statement/
  Choice/SubGraph/End), typed `ParameterData`, `BaseChoice`, `BaseCondition`, `BaseAction`.
- Execution runtime: headless `BaseRunner`, `BaseContext` blackboard, pluggable executors,
  sub-graph nesting, history, cycle detection.
- Editor: graph view, node/edge views, inspector, window; copy/paste with new GUIDs.
