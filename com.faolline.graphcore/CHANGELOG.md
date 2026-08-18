# Changelog

All notable changes to **com.faolline.graphcore** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/) and the project uses
[Semantic Versioning](https://semver.org/).

## [0.43.1]

### Fixed
- **Removed the dead `[CreateAssetMenu]` on `GraphTemplate`.** `Assets > Create > GraphCore > Graph
  Template` produced an empty asset with no inspector to populate it — the only meaningful way to
  create a template is "Save Selection as Template" from the graph editor canvas, which already
  creates the asset directly (`CreateInstance` + `AssetDatabase.CreateAsset`). Also fixed the
  `graphcore/README.md` doc that claimed `BaseGraph` had a creation menu (`Assets > Create >
  GraphCore > Base Graph`) — it never has one, by design (see `BaseGraph.cs`).

## [0.43.0]

### Changed
- **The rest of graphcore's `Debug.Log`/`LogWarning`/`LogError` call sites migrated to `Logging`**
  (`Faolline.GraphLogging`, 0.1.1): `BaseRunner`'s diagnostics (`GraphCore.Runtime`) and every Editor
  tool — `GraphValidator`, `ScriptIconAssigner`, `SignalConstantsGenerator`, `VariableConstantsGenerator`,
  `StableIdDuplicateDetector`, the graph view/inspector windows (`GraphCore.Editor`) — now go through the
  same per-category toggle as the bootstrap/`LogAction` wiring already shipped in 0.42.0.

## [0.42.0]

### Added
- **New dependency: `com.faolline.graphlogging` (0.1.0).** `GraphCoreUnityBootstrap` now wires the
  existing `GraphLog` seam (the engine-agnostic `Runtime.Core` warning/error sink used by `BaseContext`)
  to `Logging.Warning/Error("GraphCore.Context", ...)` instead of calling `Debug.LogWarning`/
  `Debug.LogError` directly — Core's own diagnostics now share the same per-category on/off control
  (`Faolline ▸ Diagnostics ▸ Log Settings`) as every other package that adopts the facade. `Runtime.Core`
  itself is untouched (still zero `UnityEngine` references) — only the bootstrap wiring, already in the
  engine-referencing `Runtime` assembly, changed.
- **`LogAction` gained a `Category` field** (default `"GraphCore.LogAction"`), routed through
  `Logging.Info` instead of a raw `Debug.Log` — a designer can silence or scope a specific Log
  node's output project-wide without deleting it.

## [0.41.0]

### Changed — `GraphLinkNodeData` no longer forces its target into the build (soft reference)

`GraphLinkNodeData` is a documentary, non-executing cross-reference — `BaseRunner` passes straight
through it like a comment and never touches `TargetGraph` at runtime — but its hard
`[SerializeField] BaseGraph` field made Unity treat the link as a real serialization dependency,
pulling the target (and everything *it* references) into the same build/bundle inclusion group as
the host graph for zero runtime value.

The field is now a GUID (`TargetGraphGuid`, new public member), resolved on demand. `TargetGraph`
keeps its exact public signature (`BaseGraph`, get/set) — no change needed anywhere it was already
used (drag-and-drop assignment, canvas double-click navigation) — it is simply `#if UNITY_EDITOR`
now, since nothing at runtime ever legally dereferenced it. No migration is provided for existing
serialized links (an intentional, authorized exception — see spec `047-graph-soft-links`); re-assign
the target once after updating.

### Added — `GraphValidator`: two new checks

- A `GraphLinkNodeData` whose recorded GUID no longer resolves to any asset is flagged (`Warning`) —
  compensates for the compile-time safety net the hard reference used to provide.
- A new generic extension seam, `IGraphValidatorExtension`/`GraphValidatorExtensionRegistry`
  (mirrors the existing `ContextKeyLabelRegistry` shape), lets a downstream lib flag a
  `SubGraphNodeData` target it considers problematic — e.g. `graphgameflow` (0.17.0) uses this to
  warn when a hard sub-graph reference accidentally crosses into a graph registered as an
  independently-loadable chapter root, which would silently reintroduce a full build-time pull.
  Empty/inert by default; `graphcore` itself has no opinion on what a "chapter root" is.

## [0.40.1]

### Fixed — module selector no longer hardcodes `#master`

`GraphEcosystemModuleSelector` built every git URL with `GraphEcosystemModules.json`'s fixed `"branch":
"master"`, regardless of what ref the consumer actually installed `com.faolline.graphcore` at. A project
pinned to a tag (e.g. `#v1.3.0`, per `INSTALL.md`'s reproducible-install guidance) would still have every
*other* module pulled from `#master` through this window — defeating the pin, and in at least one observed
case surfacing as UPM refusing to add a module ("invalid dependencies or related test packages") because
the two refs disagreed on what should be installed together.

The window now reads back `com.faolline.graphcore`'s own resolved `PackageInfo.packageId` (which carries
the actual `#ref` — tag, branch, or commit — for a git-sourced package) and reuses that ref for every module
it builds a URL for, falling back to the config's `branch` only when graphcore isn't installed yet or wasn't
installed from git. The window's header now names the ref it's about to use, so this is visible before
clicking Apply rather than discovered from a failure. `INSTALL.md` updated to point at tagged installs.

## [0.40.0]

### Added — `FaollineGraphSettings` (project-level output folder for the constants generators)

The `Generate(string outputPath)` overload added in 0.39.0 fixed the asmdef problem but left every
consumer writing their own `[MenuItem]` around it — and left the ecosystem's own default
`Faolline ▸ Graph ▸ Generate Constants` menu still targeting the unusable `Assets/Generated/`, one reflexive
click away from a second `GraphSignals`/`GraphVariables` compiling into `Assembly-CSharp` (CS0436 — the
`Assembly-CSharp` copy shadows the asmdef'd one for anything in `Assembly-CSharp`, silent everywhere else,
since both start from the same Defs and only diverge once one of the two stops being regenerated).

`FaollineGraphSettings` (`ScriptableSingleton`, `ProjectSettings/FaollineGraphSettings.asset` — versioned,
shared by the team, not per-machine `EditorPrefs`) holds one `GeneratedConstantsFolder`. Both generators'
`Generate()` read it (falling back to the unchanged `DefaultOutputPath` when unset — no behavior change for
an existing consumer), so the ecosystem's own menu becomes correct once the folder is set, instead of
needing a competing one. A single folder (not one path per generator) rules out `GraphSignals` and
`GraphVariables` landing in different assemblies. Exposed at Edit ▸ Project Settings ▸ Faolline Graph
(`FaollineGraphSettingsProvider`) — a consumer's own tooling can set
`FaollineGraphSettings.instance.GeneratedConstantsFolder` directly instead, both routes write the same
asset. The settings page warns when the configured folder has no ancestor `.asmdef`, i.e. exactly the
condition that would silently compile the output into `Assembly-CSharp` again.

## [0.39.0]

### Added — `Generate(string outputPath)` overload on both constants generators

`SignalConstantsGenerator.Generate()`/`VariableConstantsGenerator.Generate()` only ever wrote to their
hardcoded `DefaultOutputPath` (`Assets/Generated/GraphSignals.cs` / `GraphVariables.cs`) — a folder with no
asmdef of its own. In an asmdef-layered consumer project that means the generated class compiles into
`Assembly-CSharp`, which no asmdef can reference back into: the generated constants were unusable from any
layer, not just inconveniently placed. `Generate(string outputPath)` lets a consumer point generation at a
folder their own asmdef (e.g. Domain) actually covers; `Generate()` is unchanged (`DefaultOutputPath`,
same as before). Found via external review of `INTEGRATION.md`'s Clean Architecture guidance, which is
exactly the audience this broke for.

## [0.38.0]

### Added — `graphcore.Runtime.Core` (engine-agnostic run-state assembly)

New `noEngineReferences` assembly (`com.faolline.graphcore/Runtime.Core/`), referenced by
`graphcore.Runtime`. Carries the pure-C# run-state model, moved out of `graphcore.Runtime` unchanged in
behavior: `BaseContext` and `SignalArgs`. Two seams make this possible without Core naming any
`UnityEngine` type:

- `BaseContextTypeRegistry` — Core only recognizes `bool`/`int`/`float`/`string` for
  `BaseContext.Set`/`Get`/`RaiseSignal`; the Unity layer registers `Vector2`/`Vector3`/`Color` back at
  load (`GraphCoreUnityBootstrap`, covers both the editor — including EditMode tests — and player
  builds). Runtime-visible behavior is unchanged.
- `GraphLog` — a minimal `Action<string>` logging seam; `GraphCoreUnityBootstrap` wires it to
  `Debug.LogWarning`/`Debug.LogError`.
- `BaseContext.InitFromGraph(BaseGraph)` and the graph-seeded `BeginLocalContext(BaseGraph)` overload
  moved to `BaseContextGraphExtensions` (extension methods in `graphcore.Runtime`) since `BaseGraph`/
  `VariableDef` are ScriptableObject assets — same call syntax, no call-site changes anywhere in the
  ecosystem. Two new public primitives on `BaseContext`, `SeedGlobalIfAbsent`/`SeedLocalIfAbsent`, back
  them.
- `BaseContext.CopyValuesFrom` stays `internal`; `graphcore.Runtime` (where `BaseRunner` lives) gets
  friend access via `[InternalsVisibleTo]`.

Every downstream package's asmdef now references `graphcore.Runtime.Core` alongside `graphcore.Runtime`
(mechanical sweep, ~30 asmdefs); `DependencyMatrixTests.Allowed` and `ARCHITECTURE.md` updated in the
same commit.

**Scope note:** this is deliberately narrow — the runner, nodes, and the asset/authoring model
(`BaseAction`/`BaseCondition`/`BaseGraph`) stay in `graphcore.Runtime`. Making those engine-agnostic too
was evaluated and rejected as out of scope (their coupling to `ScriptableObject` runs too deep to be
worth resolving here); this slice exists to give `BaseContext` a Unity-independent test/compile surface,
not to make the graph runner referenceable from a `noEngineReferences` Application layer.

## [0.37.0]

### Added — `GraphCategoryGroup`

A generic, `[CreateAssetMenu]`-exposed asset (`com.faolline.graphcore/Runtime/Grouping/GraphCategoryGroup.cs`)
that lets a non-dev user organize any `BaseGraph` (quests today, other verticals later) into one or
more named groups (e.g. "Main" / "Side") with no code required. A graph may belong to zero, one, or
several groups at once — intentional, not a gap. Carries no stable-GUID identity (unlike
`VariableDef`/`SignalDef`/`CollectionDef`): nothing in the runtime reads it, so it stays as simple as a
label and a list.

Membership is displayed and edited on the graph's own inspector (no-selection panel) through a new
`GraphCategoryGroupInspectorExtension`, registered via the existing `InspectorExtensionRegistry.RegisterGraphSection`
seam — the same pattern `graphlocalization`'s `LocalizationInspectorExtension` already uses, but for the
first time editing a *foreign* asset (the matching `GraphCategoryGroup`(s)) rather than data embedded on
the inspected graph itself. Since group membership is stored forward-only (group → graphs), the
extension reverse-scans project `GraphCategoryGroup` assets once per inspector bind (not per redraw) to
find which group(s) contain the current graph.

No changes to `QuestGraph`, `BaseGraph`, or `QuestEvaluator` — this is pure editor-time organizational
metadata with zero runtime consumer.

### Added — `EXTENSIBILITY.md`

New repo-root doc (alongside `ARCHITECTURE.md`/`INTEGRATION.md`) documenting `InspectorExtensionRegistry`
as a public extension seam: its contract (`RegisterGraphSection`/`RegisterNodeSection`, call ordering,
the `markDirty` parameter and when to bypass it for foreign-asset edits), plus `GraphCategoryGroup`
walked end-to-end as the worked example.

## [0.36.1]

### Added — `GraphValidator` flags nested `OpensScope` sub-graphs (editor-only, no runtime change)

`BaseContext`'s local-context overlay (`OpensScope`) is a flat overlay, not a stack: reaching a second
`OpensScope` sub-graph while the first is still open — possible along any path that keeps riding the
same context (`OpensScope`/`InheritParentContext` all the way down) — silently discards the outer
scope's local values, since `BeginLocalContext` only logs a runtime warning and proceeds rather than
stacking. Nobody had hit this in practice (zero test coverage, zero validator check, zero dogfood
report before now), so it stayed invisible. `GraphValidator` now walks a graph's `OpensScope`
sub-graphs recursively (through any depth of `InheritParentContext` hops) and warns at authoring time
before it becomes a silent runtime data-loss surprise. Also documented directly on
`SubGraphNodeData.OpensScope`'s XML doc.

Deliberately NOT building a real fix (a proper local-context scope stack) yet: the ecosystem is headed
toward non-linear/parallel execution engines (see the execution-paradigms direction in `TODO.md`) whose
scoping needs — a real scope tree with concurrent branches, not a LIFO stack — are structurally
different from this narrow, currently-unused nested-sub-graph case. Freezing a stack shape now, before
that design exists, risks exactly the kind of premature commitment `TODO.md`'s signal-scoping entry
already warns against. The validator warning is the appropriate stopgap: cheap, and it does not
foreclose any future design.

## [0.36.0]

### Added — `SignalPayloadMatchesCondition`

A shared signal name raised by several independent sources (e.g. one `AsyncSceneLoader`'s
`LoadCompletedSignal` used by several concurrently-parked zone/tile flows in a proximity-streaming
setup) resumes EVERY node awaiting that name, not just the one it was meant for — `BaseRunner` only
ever compares names (`Contains(node.AwaitSignalNames, name)`), never the payload. `AwaitSignalName` /
`OnSignal` stay global-to-context by design (see `TODO.md` — "Signal scoping" is a deliberately
deferred substrate change); this ships the narrower, immediately-usable fix: a `BaseCondition` for
`BaseNodeData.ResumeConditions` that gates the resume on the raised signal's last string payload
matching an expected value. Mirrors `SignalRaisedCondition` (asset, `CreateAssetMenu`, drag-drop for
a fixed/named set of cases); for a dynamic/procedural set, create an instance at runtime via
`ScriptableObject.CreateInstance` and set `ExpectedValue` at the same point the corresponding load is
issued.

### Added — `IResumeSignalAwareCondition`, and multi-signal support for `SignalPayloadMatchesCondition`

`BaseNodeData.ResumeConditions` evaluates as one AND across the whole list regardless of which of
several OR'd `AwaitSignalNames` actually fired — so a payload-matching condition scoped to only ONE of
those names (e.g. a completion signal) used to incorrectly veto a resume triggered by a DIFFERENT
awaited name (e.g. a failure signal added via `AwaitSignalNamesExtra`), since it had no way to know it
wasn't the one being judged. New opt-in interface `IResumeSignalAwareCondition.EvaluateResume(context,
raisedSignalName)`: `BaseRunner` now passes the name that actually triggered the resume attempt to any
`ResumeConditions` entry that implements it (plain `BaseCondition`s are unaffected — same
`Evaluate(context)` as always). `SignalPayloadMatchesCondition` implements it and abstains (passes)
on any raised name that isn't its own `Signal`, so several instances — one per awaited name — now
compose as the intended OR instead of an accidental AND. Also gained `MatchMode`
(`Exact`/`StartsWith`) for payload formats like `AsyncSceneLoader`/`AddressablesSceneLoader`'s failure
signal (`"{sceneName}: {reason}"`).

## [0.35.1]

### Added — module selector can now add external backends (registry + cross-repo git)

Until now `GraphEcosystemModuleSelector`/`GraphEcosystemModules.json` could only add packages that
live in *this* repo. Two optional backends therefore had no selectable row at all: the Unity
Localization provider behind `graphlocalization`'s CSV/Unity split (no package.json dependency — it's
conditionally compiled via `versionDefines`, so nothing ever pulled it in) and the external
UnitySaveSystem backend behind `graphsave.savesystem` (a *different* git repo the old
`BuildGitIdentifier` had no way to address). Consumers had to edit `manifest.json` by hand for both.

- `ModuleEntry` gains two optional fields: `"registry": true` (add by bare package name, resolved
  from the Unity registry — e.g. `com.unity.localization`) and `"gitUrl"` (explicit full git URL
  override for a package outside this repo — e.g. UnitySaveSystem). `BuildGitIdentifier` became
  `BuildIdentifier(ModuleEntry)` to branch on these before falling back to the default
  repo/basePath/branch URL.
- Two new "↳" rows in the whitelist: **Unity Localization backend** (`com.unity.localization`,
  `dependsOn: ["com.faolline.graphlocalization"]`) — ticking it also flips on the same-named
  `Localization.Unity` bridge asmdef already shipped in both graphlocalization and
  graphdialoguesystem via their shared `versionDefines` gate — and **UnitySaveSystem (external
  backend)** (`com.faolline.savesystem.core`, via `gitUrl`), pulled in automatically once
  **Graph Save — UnitySaveSystem bridge** (`com.faolline.graphsave.savesystem`, newly listed too) is
  ticked. Dependency closure resolution (`AddClosure`) needed no changes — it already walks
  `dependsOn` regardless of a dependency's own kind.

### Fixed — module selector drift (editor-only, no runtime change)

`Editor/GraphEcosystemModules.json` had two more problems, found during the same audit:

- **`com.faolline.graphgameflow.addressables` was entirely missing from the whitelist.** The
  Addressables bridge shipped in `035-addressables-scene-loader` but the selector was never updated,
  so it could only be installed via the manual git-URL path even though nothing blocks offering it
  (its one extra dependency, `com.unity.addressables`, is a normal Unity registry package UPM resolves
  on its own — no `registry`/`gitUrl` override needed). Added, with
  `dependsOn: ["com.faolline.graphgameflow"]`.
- **Every listed `version` was stale** (e.g. graphcore itself said `0.20.0`). The install/update
  mechanism always tracks `#master` HEAD regardless of this field, so nothing broke functionally —
  but the "update available" comparison and the version column were misleading. Refreshed all seven
  pre-existing entries to their current `package.json` versions.

## [0.35.0]

### Changed / Breaking — vocabulary rename (no behaviour change)

The three context-primitive identity assets lose the misleading `Name` suffix (their identity is a GUID, not a
name), and "parameter" becomes **"variable"** everywhere (it's a mutable runtime value that varies, not a
passed-in setting). Pure rename — zero logic change; the full EditMode suite passes unchanged.

- **Asset types:** `SignalName` → **`SignalDef`**, `CollectionName` → **`CollectionDef`**, `ParameterName` →
  **`VariableDef`** (`CollectionEntry` unchanged). Renamed via `git mv` keeping each file's `.meta` GUID, so
  **existing assets keep their script link** — no `[MovedFrom]` needed.
- **Parameter → Variable** across the board: `ParameterType` → `VariableType`, `GraphParams` →
  `GraphVariables` (generated class + menu `Faolline ▸ Variables ▸ Generate Constants`),
  `IParameterReferencing` → `IVariableReferencing`, `ParameterReference` → `VariableReference`,
  `GraphParameterScanner` → `GraphVariableScanner`, `ParameterConstantsGenerator` → `VariableConstantsGenerator`.
  Action/condition field `Parameter` → `Variable`; `ReferencedParameters` → `ReferencedVariables`.
- **`BaseContext` value-store API:** `GetAllParameters` → `GetAllVariables`, `OnParameterChanged` →
  `OnVariableChanged`, `OnAnyParameterChanged` → `OnAnyVariableChanged`, `OffParameterChanged` →
  `OffVariableChanged`. The generic `Set/Get/TryGet<T>` are unchanged (they never said "parameter").
- The raw-string escape hatch (`context.Set<int>("hp", …)`) and the graphTest `Test*` doubles' raw `ParameterKey`
  API are deliberately **untouched** — they are the islands escape hatch, not the governed vocabulary.

Downstream packages bump their graphcore floor to `0.35.0`.

## [0.34.0]

Parameter identity re-base (spec `033-parameter-identity-rebase`). Applies the proven `032` signal model to the
last raw-string primitive — parameters. Unpublished ecosystem — clean break, no migration shim.

### Added
- **`ParameterName` asset — a typed, stable-GUID parameter definition.** `ScriptableObject, IStableGuidIdentity`
  carrying a GUID `Key` (assigned in `OnEnable`, never editable, persisted via `StableGuidPersistence`), a
  cosmetic `DisplayName`, a `ParameterType`, and a typed default (`DefaultValueBoxed`). `(string)ParameterName`
  returns the GUID — so an asset-based action/condition keys on the stable identity. Renaming a parameter's
  display name is now free (the GUID never changes; only the regenerated code symbol does). Factories
  (`ParameterName.Bool/Int/Float/String/Vector2/Vector3/Color`) for code-first authoring and tests. The asset
  IS the declaration: there is no per-graph parameter list anymore.
- **`IParameterReferencing` + `ParameterReference` (parameter + expected type).** The opt-in contract that makes
  parameters declaration-free: an action/condition exposes the `ParameterName`s it reads/writes, each tagged
  with the type it expects. Implemented by every stock parameter action/condition.
- **`GraphParameterScanner`** — walks a graph's action/condition sites (entry/resume conditions, enter/exit
  actions, choice + edge conditions) and collects referenced parameters. Used by `InitFromGraph` (seed the
  discovered parameters' defaults, seed-if-absent) and by the validator.
- **`ParameterConstantsGenerator` → `GraphParams` class** (menu `Faolline ▸ Parameters ▸ Generate Constants`):
  a `const string` per `ParameterName` asset — symbol from `DisplayName`, value = the GUID — the compile-checked
  bridge for reading/writing asset parameters from pure host code. Shares its sanitize/collision core with the
  signal generator via the new `ConstantsGeneratorCore`.
- **Graph validator: parameter type-mismatch is now an error.** A `SetIntAction` wired to a `Float` parameter
  (or two differently-typed references to one parameter) is caught at authoring time instead of silently
  corrupting the key at runtime.

### Changed / Breaking
- **The stock parameter actions/conditions reference a `ParameterName` asset instead of a raw `_parameterKey`
  string.** `SetInt/Bool/Float/String`, `AddInt/Float`, `SetRandomInt`, `ToggleBool`; `Int/Bool/Float/String`
  conditions; the three `*CompareCondition`s — all now expose a `Parameter` (or `Left`/`Right`) `ParameterName`
  field. The raw-string escape hatch stays at the `BaseContext` API level (`context.Set<int>("hp", …)`) per the
  **islands** rule: asset parameters key on the GUID, raw literals key on themselves, the two never cross.
- **`InitFromGraph` seeds discovered `ParameterName` defaults** (via `GraphParameterScanner`) instead of reading
  a per-graph parameter list. The seeding entry points are unchanged for callers.
- **Removed** `BaseGraph._parameters` / `Parameters` / `AddParameter` / `RemoveParameter`, the `ParameterData`
  class, its `ParameterDataDrawer`, and the graph-parameter authoring panel in `BaseNodeInspectorView`
  (parameters are now project assets dragged onto actions, exactly like `SignalName`). Committed sample graphs
  that referenced parameters must be regenerated via their sample-builder menu items.

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
