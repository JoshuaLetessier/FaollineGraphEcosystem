# Changelog

All notable changes to **com.faolline.graphcore** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.33.2]

### Added
- **`StableGuidPersistence.PersistAll()` + menu `Faolline ▸ Graph ▸ Persist Stable Ids`** — synchronously
  flushes every `IStableGuidIdentity` asset (`SignalName`, `CollectionEntry`, `CollectionName`, `BaseGraph`)
  to disk, persisting any GUID that `OnEnable` assigned in memory but never wrote. This is the deterministic
  remedy for the 0.33.1 bug's exact trigger: the 0.33.1 auto-heal uses `EditorApplication.delayCall`, which
  fires on the next editor tick — so it self-heals a normal interactive session, but a one-shot
  `-batchmode -executeMethod … -quit` (CI, or scripted asset generation) exits *before* any tick and never
  persists. `PersistAll` runs immediately, so it works under `-quit`:
  `-executeMethod Faolline.GraphCore.StableGuidPersistence.PersistAll`. Run it once after migrating a
  pre-existing project, or as a CI step before generating constants / building saves. A brand-new project
  that only creates assets through the Create menu never needs it (those persist their GUID at creation).

## [0.33.1]

### Fixed
- **Stable-GUID identities are now persisted to disk when assigned in `OnEnable` (finding 23).** A GUID
  assigned in `OnEnable` (when the id field is empty) reached memory but never disk — assigning a serialized
  field in code does not mark the asset dirty, so Unity never wrote it back. An asset created via the Create
  menu was fine (its `OnEnable` runs before the asset is first serialized), but an asset that reached an empty
  id another way — a pre-existing asset from before its id field existed, or one whose old id field
  deserialized empty — **re-derived a different random GUID every session**. Invisible within one run (all
  references share the live instance) but desyncing anything that crosses a session boundary: a generated
  `GraphSignals` constant, and, worse, a save file whose `RaisedSignals` reloaded in a session that never
  explicitly saved the asset would see an already-fired signal silently "un-fire". A new editor-only
  `StableGuidPersistence.ScheduleSave` (called from `OnEnable` right after a fresh assignment) flushes it to
  disk on the next editor tick, for persistent assets only (runtime/test instances and player builds are
  skipped). Applied to all four stable-GUID types — `SignalName`, `CollectionEntry`, `CollectionName`,
  `BaseGraph` — which all shared this latent bug. (Consumer dogfood finding.)

## [0.33.0]

Signal identity re-base (spec `032-signal-identity-rebase`). Unpublished ecosystem — clean break, no
migration (re-generate consumer content).

### Changed (breaking)
- **`SignalName` identity is now a stable GUID (`Key`), not a human string.** `SignalName` adopts the exact
  model of `CollectionEntry`/`CollectionName`/`BaseGraph.GraphId`: a GUID assigned once in `OnEnable`, never
  editable, is what gets raised/awaited/matched/saved. `(string)SignalName` now returns that GUID (was the
  display name). `_name` becomes a **cosmetic** `DisplayName` — rename it (or the asset file) freely; the
  data (awaits/raises/saves) keeps matching on the unchanged GUID. `SignalName` implements
  `IStableGuidIdentity`, so the duplicate detector regenerates a Ctrl+D copy's GUID. Closes the old
  file-name-drift bug (renaming an asset silently changed the raised string and broke saved history).
- **Islands.** Asset signals key on the GUID; the raw-string channel (`RaiseSignal(literal)` and the raw
  `AwaitSignalName` field) keys on literals. The two do NOT cross — a raw `RaiseSignal("advance")` no longer
  wakes a node awaiting a `SignalName` asset. To raise an asset signal from code, use the generated constant
  or a held `SignalName` reference (implicit → GUID).

### Added
- **`SignalConstantsGenerator`** (editor) — `Faolline ▸ Signals ▸ Generate Constants` scans the project's
  `SignalName` assets and generates a `GraphSignals` static class of `const string`s: the symbol derives from
  each signal's `DisplayName` (compile-checked, e.g. `GraphSignals.PlayerInteracted`), the value is its GUID.
  This is the load-bearing bridge for raising asset signals from pure host code. Renaming a signal changes
  only the symbol (stale code fails to compile — the intended, safe rename), never the value. A symbol
  collision (two display names sanitizing to the same identifier) is a **blocking error**, never a silent
  merge. The generator ships in graphcore; the generated class lives in the consumer project (zero deps).
- **`SignalName.Create(displayName)`** — factory for runtime/test signals with a fresh GUID and a display
  label.

### Notes
- **Label resolver (spec FR-009) deferred**: the Context Watch window does not surface signals today (only
  parameters and collections), so there is no signal GUID to label. Revisit if signal history is added there.

## [0.32.0]

### Added
- **Collections support quantities (stacking).** `BaseContext.AddToCollection(key, item, count)` /
  `RemoveFromCollection(key, item, count)` add/subtract units of an item on top of its existing quantity
  (inventory-style stacking) — additive, never idempotent, and always fire `OnCollectionChanged` on a real
  quantity change (even when the item already existed). The existing 2-argument overloads are UNCHANGED:
  still a plain idempotent "ensure present" / "remove entirely" pair, so every pre-existing caller keeps its
  exact behaviour. `CollectionItemCount(key, item)` reads a specific item's quantity;
  `GetCollectionWithCounts(key)` returns every (item, quantity) pair. `CollectionCount(key)` keeps its
  existing meaning — the DISTINCT item count, unaffected by quantity.
- **Collections preserve insertion order.** `GetCollection(key)` (now `IReadOnlyList<string>`, widened from
  `IReadOnlyCollection<string>`) and `GetAllCollections()` yield distinct items in the order they were first
  added, instead of an arbitrary hash-set order. A removed-then-re-added item is treated as a fresh
  insertion (moves to the end) — re-adding an item still present never reorders it. No known consumer
  depended on the old arbitrary order, so this is additive, not a behavior break.
- **`AddToCollectionAction` / `RemoveFromCollectionAction` gain a `Stack` toggle + `Count` field.** OFF
  (default — matches every asset authored before this option existed) keeps the classic idempotent
  add / whole-stack remove. ON adds/subtracts `Count` units instead.

### Changed
- **`GetAllCollections()` does not capture quantities** — it mirrors the pre-0.32.0 shape (distinct
  membership only) so existing save-format consumers (`com.faolline.graphsave`'s `GraphRunSnapshot`) keep
  compiling and behaving unchanged. A stacked item's quantity beyond 1 is **not** round-tripped through a
  `GraphRunSnapshot` save/restore yet — a consumer relying on graphsave for persistence and adopting
  stacking should be aware a restored game currently sees quantity capped at 1 for any previously-stacked
  item. Extending `GraphRunSnapshot` to also capture quantities is a natural, not-yet-done follow-up.

## [0.31.0]

### Added
- **`IStableGuidIdentity`** — the interface generalizing the "stable GUID, assigned once, never editable"
  pattern first introduced for `BaseGraph.GraphId` (0.28.0) and just extended to
  `CollectionEntry`/`CollectionName.Key` (0.30.0). A type exposes `StableId` + `StableIdFieldName`
  (typically both explicit implementations, kept out of its normal public surface) and is automatically
  picked up by the duplicate detector below — no per-type code needed anywhere else.

### Changed
- **`GraphIdDuplicateDetector` generalized and renamed `StableIdDuplicateDetector`.** It now scans every
  `ScriptableObject` type implementing `IStableGuidIdentity` (discovered via `TypeCache`, currently
  `BaseGraph`, `CollectionEntry`, `CollectionName`) instead of only `BaseGraph`, closing the same
  duplicate-on-copy hole for the two collection types introduced in 0.30.0. Duplicates are still scoped
  strictly PER CONCRETE TYPE — a `BaseGraph` and a `CollectionEntry` coincidentally sharing a GUID string is
  not a collision, since they are never compared to each other. Menu renamed
  `Faolline ▸ Graph ▸ Fix Duplicate GraphIds` → `Faolline ▸ Graph ▸ Fix Duplicate Stable Ids`.
  `ScanAndFix(HashSet<string>)`'s signature and behavior are unchanged, only the class name moved.



### Changed (breaking)
- **`CollectionEntry.Key` / `CollectionName.Key` are now stable GUIDs, never editable.** Both previously
  exposed an editable `_key` string that fell back to the asset's file name when empty — the same
  instability class as the `BaseGraph.GraphId` duplicate-on-copy bug: renaming the asset silently changed
  the value stored in a context collection, and two independently-typed keys could collide by accident or
  typo. `Key` is now assigned once in `OnEnable` (mirroring `GraphId`) and can never be edited or duplicated
  onto another asset by a Ctrl+D (each copy gets a fresh id the same way a duplicated `BaseGraph` does).
  Two calls that must refer to the SAME collection/entry now do so by referencing the SAME asset — never by
  two assets that happen to share a typed/named string.
- **`CollectionEntry.Title` / `CollectionName.Title`** (new): an optional, purely cosmetic display label for
  editor tooling (Context Watch, etc.) — falls back to the asset name when empty. Never used as the stored
  key; renaming it (or the asset) never changes what a save file or `CollectionContainsCondition` sees.

### Migration
- The old `_key` field is recovered via `[FormerlySerializedAs]` into the new (hidden) id field:
  - An asset that had an **explicit non-empty `_key`** (deliberately typed by an author) keeps that exact
    string as its `Key` going forward — just no longer editable.
  - An asset that relied on the **empty-key/name-fallback** behaviour (the common case — most authors never
    touched the field) gets a **fresh random GUID** the first time it loads after upgrading: that "identity"
    was never actually stored in the asset (it was derived from the file name at runtime each time), so
    there is nothing to preserve. Any save data or hardcoded literal compared against a former
    name-fallback value needs updating to the new GUID (or, better, to compare against the asset's `.Key`
    rather than a literal).



### Added
- **Circular-await lint.** `GraphValidator` now detects the "cupboard" deadlock: a node that awaits a signal
  whose every internal raiser runs only AFTER that node resumes (its own exit-actions, or nodes reachable
  only through it). Reachability is computed from the entry with the awaiting node absorbing; OR-awaits pass
  if ANY awaited name can fire before the resume; a name the graph never raises stays exempt (host-raised
  pattern). (Consumer dogfood finding.)

### Changed
- **History-saturation warning deferred to the first BLOCKED rewind.** The 0.26.2 warning fired on every
  20+ step run — even in games that never rewind — because it triggered on the trim itself. Trimming at
  `HistoryDepth` is now silent again; the (once per run) warning fires from `GoBack`/`GoBackToCheckpoint`
  when a rewind actually hits the trimmed boundary. (Consumer dogfood finding.)
- **Editor assembly is now `autoReferenced`.** A consumer editor script WITHOUT its own asmdef
  (plain `Assets/Editor/`) can now call `GraphValidator` etc. directly — previously the assembly was
  invisible to the predefined assemblies and reachable only via reflection. (Consumer dogfood finding.)

## [0.28.0]

### Added
- **Duplicate-GraphId detector (editor).** Duplicating a graph asset (Ctrl+D, file copy) copies the
  serialized `GraphId` — `OnEnable` only assigns when empty — so two assets silently shared an id that
  SubGraph cycle detection and save/localization keys rely on. A new `GraphIdDuplicateDetector`
  (AssetPostprocessor) now scans on graph-asset import and regenerates the duplicate's id: the asset that
  was NOT just imported keeps its id, and every regeneration logs both paths + ids so an intentional
  replace-workflow can be spotted and reverted. Manual pass: `Faolline ▸ Graph ▸ Fix Duplicate GraphIds`.

## [0.27.0]

### Added
- **`BaseNodeData.AwaitSignalNamesExtra`** — extra OR-await signal names as raw strings, the code-first
  counterpart of the `AwaitSignals` asset list (assets suit the visual editor; strings serialize plainly in
  the graph asset and need no `SignalName` sub-asset). Both lists merge into `AwaitSignalNames`, de-duplicated,
  primary first; the runner is untouched. Surfaced by graphstandard 0.15.0's `Await(params string[])` builder.
- **`BaseRunner.DetachEditorProbe()`** — unregisters the runner's editor live-cursor probe. A host MUST call
  it when discarding a runner (stop, teardown, replacing it with a new run): the graph editor takes the FIRST
  probe answering for a graph, so a dead run's probe would otherwise shadow the live one on replay (and the
  probe kept the runner/graph/context reachable for the whole session). No-op in player builds.
- **`GraphRunMonitor.Clear()` / `GraphRunContextRegistry.Clear()` + play-mode purge.** A new editor hook
  (`GraphRunMonitorPlayModeReset`) empties both registries when Play mode exits, so probes a host forgot to
  detach — or leftovers surviving a disabled domain reload (Enter Play Mode Options) — never shadow the next
  session's runs.

### Fixed
- **The subgraph-signal-deadlock lint understands multi-await (OR).** `GraphValidator` previously collected
  only the primary `AwaitSignalName`; it now evaluates the full `AwaitSignalNames` union per node and only
  reports a node when NONE of its awaited names is raised inside the graph — a node awaiting
  {internal, external} is not a deadlock.
- **`RaiseSignal<T>` error message listed only 4 of the 7 supported payload types.** The check has accepted
  `Vector2`/`Vector3`/`Color` since the payload types were widened; the exception text now says so.

## [0.26.2]

### Changed
- **`BaseRunner` warns once when history saturates.** When traversal exceeds `BaseGraph.HistoryDepth` the
  oldest step is auto-trimmed (bounded memory, unchanged) — but it used to be silent, so an author who
  expected `GoBack`/`GoBackToCheckpoint` to rewind further got no signal. It now logs a one-shot
  `[GraphCore]` warning the first time it trims in a run (re-arms on `Start`/`StartFrom`). (Cryptique dogfood.)

## [0.26.1]

### Fixed
- **`GraphValidator` no longer flags a `GraphLinkNodeData` as an isolated node.** A GraphLink is a
  non-executing documentary annotation, so being unconnected is its normal state; the false-positive
  "Isolated node" warning is now suppressed for it (regular disconnected nodes still warn). Left unfixed it
  would train consumers to ignore validator warnings. (Dogfood finding.)

## [0.26.0]

### Added
- **Multi-signal await (OR).** A node can now wait for several signals at once: `BaseNodeData.AwaitSignals`
  (extra `SignalName` list) plus the existing single `AwaitSignalName` form the awaited set, exposed as
  `AwaitSignalNames`. The runner subscribes to all of them and resumes on the **first** awaited signal that
  passes `ResumeConditions` — so "resume on interact OR win" needs no consumer double-raise. Composes as
  OR-triggers × AND-gate (`ResumeConditions`) × NOT (`NotCondition`). A single await is just a one-element set
  (fully back-compatible). (Dogfood finding.)

## [0.25.0]

### Added
- **`ContextKeyLabelRegistry` + `IContextLabelResolver` (Editor).** An opt-in seam (mirrors
  `NodeTypeColorRegistry`) that lets downstream libs turn their opaque scoped context keys and collection
  entries into human-readable labels in editor tooling. The **Context Watch** window now shows a resolver's
  label (raw key as tooltip) instead of the bare key/guid. graphcore stays domain-neutral — it ships the
  registry empty; a lib registers its resolver (e.g. graphquest names `quest_completed:<id>` and objective
  guids). (Dogfood finding.)

## [0.24.0]

### Added
- **`GraphValidator` flags sub-graph signal deadlocks.** A `SubGraphNodeData` on a fresh context (Inherit
  Parent Context off, no scope) whose target graph awaits a signal that nothing inside it raises can never
  resume — the parent/host raises that signal on a different context. The validator now warns, naming the
  signal, and suggests enabling Inherit Parent Context / Opens Scope. Self-contained signal loops (the
  sub-graph raises what it awaits) are not flagged. Catches the "isolated sub-graph" authoring footgun
  statically. (Dogfood finding.)
- **`GraphValidator` flags shadowed branches.** On an auto-advanced (non-choice) node, an unconditioned
  outgoing edge that is not last makes every branch after it unreachable — the runner takes the first passing
  edge and an unconditioned edge always passes. The validator warns and points to the fix (add a condition,
  or move it last as the default/else branch — which is the supported way to author a fallback). Choice-node
  edges route by port id, so they are exempt. (Dogfood finding.)

## [0.23.0]

### Added
- **`BaseContext.OnAnySignalRaised` / `OffAnySignalRaised`** — wildcard subscription to every raised signal
  (receives the signal name), mirroring `OnAnyParameterChanged` / `OnAnyCollectionChanged`. Fires after the
  per-name `OnSignal` handlers. Lets a reactive consumer re-derive on any signal without knowing the name in
  advance — the seam `QuestEvaluator.EnableAutoEvaluate` now uses so signal-gated quests auto-evaluate.
- **`BaseNodeData.ResumeIfSignalAlreadyRaised` (opt-in).** An await-signal node with this flag set resumes
  immediately — instead of parking in `WaitingForSignal` forever — when the awaited signal was ALREADY raised
  in the context (`HasSignalBeenRaised`) before the runner reached the node, provided the `ResumeConditions`
  gate also passes. Fixes the live-only await footgun where a signal that fired ahead of the cursor (e.g. a
  quest reward that short-circuits) froze the flow even though the raised-history knew about it. Default
  `false` keeps the existing live-only behaviour.

## [0.22.1]

### Fixed
- **Renamed action fields keep `[FormerlySerializedAs]` so pre-0.22 assets survive.** The 0.22 asset-only
  refactor renamed `RaiseSignalAction._signalAsset → _signal` and `AddToCollectionAction._collectionAsset →
  _collection` / `_valueAsset → _entry` without serialization aliases, so assets authored earlier deserialized
  to null and the action silently became a no-op. The attributes are restored. (The dropped raw-string
  fallbacks — `_signalRaw`, `_collectionKey`, `_value` — cannot be migrated to asset references and are not
  recovered.)

## [0.21.0]

### Added
- **Composite conditions: `AndCondition`, `OrCondition`, `NotCondition`.** Logical combinators that nest
  arbitrarily — express "A or B", "not C", "A and (B or C)" without custom code. And is vacuous true on
  empty; Or is false on empty; Not returns true on null inner. `Create > Faolline/Conditions/And|Or|Not`.
- **Param-to-param comparison conditions: `IntCompareCondition`, `FloatCompareCondition`,
  `StringCompareCondition`.** Compare two context keys (e.g. `hp < hpMax`) instead of a key vs a literal.
  Absent keys default to 0/empty. `Create > Faolline/Conditions/Int|Float|String Compare`.
- **`RaiseSignalAction`** — emits a named signal into the context when executed. Pairs with
  `BaseNodeData.AwaitSignalName` to let one part of a graph unblock another without host code.
- **`ToggleBoolAction`** — flips a bool parameter (absent key defaults to true).
- **`SetRandomIntAction`** — sets a param to a random int in [min, max] inclusive. Useful for dice rolls
  and branching variety.

### Changed
- **Runner subscribes to context signals when awaiting.** When the runner enters `WaitingForSignal`, it now
  subscribes to the awaited signal name on the context, so a signal raised via `context.RaiseSignal()` (e.g.
  a dialogue end callback) resumes the runner without requiring `runner.RaiseSignal()`. Fixes the
  flow→dialogue→signal handshake.
- **`BaseGraphView` split into 8 partial files** (917→252 lines in main) for maintainability: Selection,
  Nodes, Edges, Groups, Changes, Templates, ContextMenu (+ existing CopyPaste, RunCursor).
- **Lazy per-graph node/edge indexes in `BaseRunner`.** `FindNode` and `GetOutgoingEdges` now build and
  cache a dictionary on first access per graph (O(1) lookup instead of O(N)/O(E) scan). Cleared on
  `Start`/`StartFrom`.
- **`ArgumentNullException` guards on `BaseRunner.Start`/`StartFrom`** for `graph` and `context`. Produces
  a clear `[GraphCore]` error instead of a `NullReferenceException` deep in the call stack.
- **`HistoryDepth = 0` memory cost documented** in XML doc on `BaseGraph.HistoryDepth` and
  `BaseRunner.AppendSnapshot`.
- **Boxing note** added to `BaseContext` class summary, explaining GC cost of per-frame `Set<T>`/`Get<T>`
  and recommending signals or typed subclass fields for hot loops.
- **XML doc type lists updated** — `Set<T>`, `Get<T>`, `GetAllParameters`, `RaiseSignal<T>` now mention all
  7 supported types (bool, int, float, string, Vector2, Vector3, Color).

## [0.20.0]

### Added
- **Reusable graph templates (save selection / insert).** Select a group of nodes, save them as a named template;
  insert later into any graph of the same type. Paste and template insert now correctly reconnect edge ports,
  fix JSON field names in placeholders, and handle node deserialization.
- **Condition badge on edges.** Edges that carry a gate (entry conditions) now show a small badge on the canvas,
  making conditional transitions visible at a glance.
- **`SignalName` asset + `TagSelector` attribute.** Type-safe signal references: create a `SignalName`
  ScriptableObject asset to name a signal, then assign it on the node's await field instead of typing a raw
  string. `TagSelector` attribute enables inspector dropdowns for signal names.
- **`AddIntAction` / `AddFloatAction`.** Increment/decrement an int or float parameter in a single action (no
  need for a Get+Set pair).
- **Version tracking + "Update All" in the module selector.** The package selector now shows version numbers and
  offers a one-click "Update All" to bump dependency floors.

### Fixed
- **Edge ports reconnect correctly on paste and template insert.**
- **JSON field names in paste/template placeholders** no longer silently drop data.
- **Node deserialization** for paste and templates handles all registered node types.

### Changed
- **Ecosystem-wide documentation pass** — tooltips, headers, `[HelpURL]` on key types, README updates.
- **HelpBox warning for unassigned SubGraph target** in the node inspector.
- **`CreateAssetMenu` removed from base actions/conditions/graph** — only concrete subclasses expose it, keeping
  the Create menu clean. Base types unified under the `Faolline/` menu.

## [0.19.0]

### Added
- **`OutcomeLabel` on End nodes.** A semantic label (`Success`, `Failure`, custom) on `BaseNodeData` for End
  nodes, surfaced in the inspector. Lets a host distinguish how a graph ended without inspecting context state.
- **`ResumeConditions` exposed in the node inspector.** The guarded-await resume gate (0.7.0) is now editable
  directly in the node inspector, not only via code.
- **Per-node `LocalizedAssetFlags`** (`[Flags]` enum replacing the old `LocalizedAssetMode`). Each node declares
  which localized asset types it needs (Text, Audio, Image, …). The graph carries a default; "Apply to all nodes"
  propagates it. Flags extracted to `graphlocalization` as the canonical definition.
- **Graph-level default `LocalizedAssetFlags` + "Apply to all nodes"** button in the graph inspector.
- **Double-click SubGraph nodes to open the target graph** in its registered editor window.

### Changed
- **`LocalizedAssetMode` → `LocalizedAssetFlags`** (`[Flags]` enum, refactor). Text flag on by default; adapters
  respect the per-node flags.
- **Tooltips** added on `OutcomeLabel`, `EntryConditions`, and `ResumeConditions` inspector fields.

## [0.18.0]

### Added
- **`GraphLinkNodeData` — a non-executing, documentary cross-reference node.** It holds a `BaseGraph TargetGraph`
  (any kind) + an optional `Note`, and is NEVER executed: the runner passes straight through it like a comment if
  it is ever wired onto the path (no pause, no actions, no executor, no access to the target). Use it to make
  composition visible — e.g. annotate a zone's flow with the quests that belong to it. Distinct from
  `SubGraphNodeData` (which IS executed). Renders via a new `GraphLinkNodeView` in EVERY lib editor (handled in
  `BaseGraphView`, no per-lib code) and there is an "Add GraphLink (reference)" canvas menu entry.
- **Dedicated minimal GraphLink inspector** (`BaseNodeInspectorView.AddGraphLinkSection`). A selected GraphLink
  shows ONLY a real `BaseGraph` object picker for its target (never a fragile string) plus a `Note` — and NONE of
  the execution fields (title, checkpoint, color, entry conditions, on-enter/exit actions, await/wait), which are
  meaningless on a node that is never executed. Each lib inspector calls it early and returns, so the same clean
  panel appears in every editor (including GameFlow/Quest, whose inspectors don't render the universal sections).
- **`GraphEditorWindowRegistry` (Editor)** — an opt-in `graph type → opener` map (mirrors `NodeTypeColorRegistry`):
  downstream lib editors register their window, and double-clicking a GraphLink opens its target in the right
  editor. Falls back to selecting/pinging the asset with a `[GraphCore]` diagnostic when no editor is registered
  (never throws). GraphCore keeps zero knowledge of any specific lib.

## [0.17.0]

### Added
- **The rest of the primitive node set is now canonical in GraphCore.** Following the bool pair (0.16.0), the
  remaining domain-neutral primitives were hoisted: conditions `IntCondition`, `FloatCondition`,
  `StringCondition`, `AlwaysTrueCondition`, `AlwaysFalseCondition` (+ the `ComparisonOperator` enum used by the
  numeric ones); actions `SetIntAction`, `SetFloatAction`, `SetStringAction`, `LogAction`. They are universally
  true of any graph system and reference nothing downstream. `GraphStandard.*` and `GraphDialogue.*` now subclass
  these, so there is a single implementation and a collision-free type to target when a consumer imports both
  namespaces. Existing lib-typed assets keep working; new graphs should prefer the GraphCore types.

## [0.16.0]

### Added
- **Canonical primitive `BoolCondition` + `SetBoolAction`.** The generic bool condition (read a context bool,
  compare to an expected value; absent key reads false, `WarnOnMissing` opt-in) and bool setter were hoisted into
  GraphCore — they are universally true of any graph system and reference nothing downstream. Downstream libs that
  historically shipped their own (`GraphStandard.*`, `GraphDialogue.*`) now subclass these, so there is a single
  implementation and a collision-free type to target when a consumer imports both namespaces (the CS0104 source).
  Existing lib-typed assets keep working; new graphs should prefer the GraphCore types.

## [0.15.3]

### Fixed
- **Auto-arrange no longer overlaps wide/tall nodes.** The "Arrange" layout used a fixed column width (280), so
  wide nodes (e.g. long dialogue titles) overlapped the next column and terminal nodes stacked on their
  predecessor. `GraphAutoLayout.Arrange` now accepts measured node sizes (the views are already on screen when
  Arrange runs) and spaces columns by each column's ACTUAL widest node + a gap, and rows by the tallest node + a
  gap — so nothing overlaps. `RouteLongEdges` detects column-spanning edges from the distinct column positions
  (width-agnostic) and drops its lane past the source node's real right edge. Legacy behaviour (no sizes) is
  unchanged. User editor feedback: "je veux pas d'un empilement de node".

## [0.15.2]

### Added
- **Opt-in "colour edges by endpoints" toggle** (toolbar, off by default, persisted in EditorPrefs and shared
  across every graph editor). When on, each edge is drawn as a gradient from its SOURCE node's colour to its
  TARGET node's colour, so in a dense graph you can tell at a glance which nodes an edge links. New
  `BaseEdgeView.ColorByEndpoints` + `RefreshColor()` and `BaseGraphView.RefreshAllEdgeColors()`.

### Fixed
- **Edge colours no longer revert on hover/move.** Unity's `Edge.UpdateEdgeControl` resets the control colours
  from the port colours on every redraw (a node hover triggers it for all edges), which wiped any custom edge
  colour — the endpoint gradient flickered back to grey the instant the mouse touched a node. `BaseEdgeView` now
  overrides `UpdateEdgeControl` to re-assert its resolved colour afterwards (selected edges keep the native
  selection highlight; connection previews keep their default).

## [0.15.1]

### Changed
- **Edges now enter/leave ports with a generous head-on run.** The orthogonal router's default port stub grew
  from 16 to 28 graph-units, so the segment touching a port follows the port axis for longer before it may bend.
  Previously the corner sat pressed against the node edge, so an edge read as "dropping into" the in-port from
  above/below instead of arriving cleanly from the side. Still clamped to 0.4×edge-length (short edges unaffected).

## [0.15.0]

### Added
- **`BaseEdgeView.Reroute()`** — re-routes an edge around the current node boxes and repaints. The orthogonal
  router reads its obstacle snapshot at render time, but the render points were only re-marked dirty on an
  *endpoint* move — so an edge laid out before a sibling node was measured (size still NaN ⇒ skipped as an
  obstacle), or before that node was dragged into its path, kept passing UNDER the node. Public so a host
  canvas can refresh edges once geometry settles.

### Fixed
- **Orthogonal edges no longer pass under nodes on a freshly opened / rearranged graph.** `BaseGraphView` now
  re-routes every edge (coalesced to one pass per frame) whenever a node view's geometry changes — its initial
  measurement and any later move/resize. Previously an edge could keep its first route, computed before the
  obstacle node had a measured size, and render straight through it. Backlog: edge-routing revisit.

## [0.14.0]

### Added
- **One Start node per graph (enforced at authoring).** A graph has a single entry point, so the editor now
  refuses a second Start node. `BaseGraphView.AddNodeToCanvas` rejects it (warns + no-op — a safety net covering
  menus and programmatic adds), and a new shared `AppendAddStartAction()` context-menu helper greys out the
  "Add Start Node" item once a Start exists (plus a `HasStartNode()` query). All editor libs (dialogue / gameflow
  / starter / test) adopt the shared action, so the rule is uniform across the ecosystem. The validator already
  flagged >1 Start as an error; this prevents the mistake up front. +2 EditMode tests.

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
