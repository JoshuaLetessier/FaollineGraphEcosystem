# Research: Full Graph-Stack Save & Restore

The user's decisions (D1-A fresh frames = documented limit + capture error, D2-A scoped layer reopened with defaults, D3-A explicit all-or-nothing failure with deepest-valid frame, D4-A dialogue later) and the corrections agreed on 2026-10-03 are fixed. This file records the implementation-level decisions on top of them, each grounded in the current code (v1.4.27, graphcore 0.43.4).

---

## R1: Where the stack logic lives

**Decision**: graphcore. `BaseRunner` gains `GetExecutionPath()` and `StartFromPath(...)`. A new static `GraphRunPath.TryResolve(...)` holds the pure check, which `StartFromPath` also calls.

**Rationale**:
- `_graphStack` is `private readonly Stack<GraphExecutionState>` (`BaseRunner.cs:157`). Building frames without entering their nodes needs that stack, `_rootGraph`, `_history`, the node/edge indexes and the editor probe, all private.
- `EnterSubGraph` (`BaseRunner.cs:630`) already owns the three context modes. Restore has to rebuild the same modes, so it belongs next to it.
- graphdialoguesystem (D4, later) can adopt the same two calls without depending on graphsave.

**Alternatives considered**:
- Logic in graphsave via reflection on the private stack: rejected (fragile, invisible to graphcore's semver).
- Expose the raw `Stack<GraphExecutionState>`: rejected. `GraphExecutionState` is mutable (`CurrentNodeId`, `FrameContext` setters), so a caller could corrupt a live run.

---

## R2: Frame description types

**Decision**:
- graphcore: `public sealed class GraphRunFrame` (immutable: `GraphId`, `NodeId`, `ContextMode`, constructor), and `public enum GraphRunContextMode { Shared = 0, Scoped = 1, Fresh = 2 }`.
- graphsave: `[Serializable] public class GraphRunSnapshot.Frame { public string GraphId; public string NodeId; public GraphRunContextMode ContextMode; }`, mapped to and from `GraphRunFrame`.

**Rationale**: matches the existing graphsave pattern (`Param`, `Collection` are nested `[Serializable]` classes with public fields) and keeps the save format owned by graphsave (Complexity Tracking). Explicit enum values keep the stored integers stable.

**Alternatives considered**: a single `[Serializable]` graphcore type with public fields (rejected, see plan); storing the mode as a string (rejected, the enum already round-trips as an int with both serializers and the values are pinned).

---

## R3: Context mode at capture — read from what actually ran

**Decision**: `GetExecutionPath()` derives each frame's mode from the live frame, not from the content flags:
- frame 0 (root): `Shared` by convention, never checked;
- `OpenedLocalContext == true` → `Scoped`;
- else `FrameContext` is the same instance as the enclosing frame's → `Shared`;
- else → `Fresh`.

**Rationale**: this is exactly what `EnterSubGraph` produced (`OpensScope` → same context + `BeginLocalContext` + `OpenedLocalContext = true`; `InheritParentContext` → same context; else `new BaseContext()`), so the save records the truth even if someone flips a flag on the asset during play in the editor. At restore, the *current* `SubGraphNodeData` flags give the mode to rebuild. A mismatch with the saved mode is a `ContextModeMismatch` error (spec edge case).

---

## R4: The check (`GraphRunPath.TryResolve`) — order, reasons, indices

**Decision**: one forward pass, first failure wins:

| Step (frame `i`) | Failure reason | `FrameIndex` | `DeepestValidFrameIndex` |
|---|---|---|---|
| root null | `NullRootGraph` | 0 | -1 |
| path null/empty | `EmptyPath` | -1 | -1 |
| frame null | `NodeMissing` | i | i-1 |
| `i == 0` and `path[0].GraphId != root.GraphId` | `RootGraphMismatch` | 0 | -1 |
| `path[i].GraphId` already seen at `j < i` | `RepeatedGraph` | i | i-1 |
| `path[i].NodeId` empty or not in graph `i` | `NodeMissing` | i | i-1 |
| `i < last`: node is not a `SubGraphNodeData` | `NotASubGraphNode` | i | i |
| `i < last`: `TargetGraph == null` | `SubGraphTargetMissing` | i | i |
| `i < last`: `TargetGraph.GraphId != path[i+1].GraphId` | `SubGraphTargetMismatch` | i | i |
| `i < last`: mode from the node's flags `!= path[i+1].ContextMode` | `ContextModeMismatch` | i | i |

`DeepestValidFrameIndex` = the deepest `k` such that `path[0..k]` would itself pass the check as a shorter path. The game can use it for its own fallback, for example resuming at that frame's sub-graph node, which replays that sub-graph from its start. The library never does this on its own (D3-A).

`Message` is a single English sentence naming the reason, the frame index and the ids involved, e.g. `Frame 1: node 'n42' no longer exists in graph 'b7f…'.`

**Rationale**:
- Graph `i ≥ 1` is only ever reached as `TargetGraph` of frame `i-1`'s node (FR-006). The libraries never resolve a graph by id (FR-005).
- `RepeatedGraph` cannot come out of a live run (`EnterSubGraph` throws `GraphCycleException` first), so it can only mean a corrupted or edited save, or cyclic content. Either way it is a data error reported like the others, not an exception.
- `FindNode` must never receive a null key (`Dictionary.TryGetValue(null)` throws). Empty ids are checked first and reported as `NodeMissing`.

---

## R5: Restore algorithm (`StartFromPath`)

**Decision**:
1. Argument checks as in `StartFrom` (`ArgumentNullException` for null root/context/path).
2. `GraphRunPath.TryResolve(root, path, out graphs, out error)`. On failure, throw `GraphRunPathException(error)`. Nothing has been touched yet.
3. Reset exactly like `StartFrom`: `_context = context`, `_registry`, `_rootGraph = root`, clear stack/history/indexes, reset saturation flags.
4. Build frames bottom-up:
   - Frame 0: `{ Graph = root, CurrentNodeId = path[0].NodeId, FrameContext = context }`.
   - Frame i: `Shared` → parent context; `Scoped` → parent context + `BeginLocalContext(graph i)` + `OpenedLocalContext = true`; `Fresh` → `new BaseContext()` + `InitFromGraph(graph i)`.
5. `_state = NodeReady`, wire the editor probe while `_context` is still the root context (same registration as `Start`). Then `_context` = top frame's context.
6. `EnterCurrentNode()`: only the top frame's node runs entry conditions, enter actions, executor and `OnNodeEntered`. The enclosing frames stay on their `SubGraphNodeData` and are never entered.

**Rationale**:
- `HandleEndNode` (`BaseRunner.cs:688`) needs nothing else from a parent frame. It pops, ends the scope if `OpenedLocalContext`, restores `_context = parentFrame.FrameContext` and recomputes the parent's outgoing edges from `CurrentNodeId`. So a restored stack continues exactly like a played one (FR-011), and `AvailableEdges` can stay empty on parent frames.
- `HistoryDepth` is read from `_rootGraph` (FR-013). History starts empty, as with `StartFrom`, so `GoBack` cannot rewind before the load point.
- The editor probe registers `GraphRunContextRegistry.Register(_runProbe, _context)`. In a normal run that context is the root one (`Start`), so restore registers the root context too.

**Alternatives considered**: re-entering every frame's node top-down (rejected: replays parent enter actions, the opposite of the requirement); implementing restore via `RestoreEntry` and a synthetic `HistoryEntry` (rejected: it calls `Undo` on the current node and `CopyValuesFrom` into the top context, both wrong here).

---

## R6: Failure channel

**Decision**:
- **Pure checks**: `GraphRunPath.TryResolve` (graphcore) and `GraphRunSnapshot.TryValidate(rootGraph, out GraphRunPathError)` (graphsave). No side effect, safe to call before `Boot`.
- **graphcore `StartFromPath`**: throws `GraphRunPathException` (carries `Error`) before any mutation.
- **graphsave `Restore`** (existing `void`): checks first. On failure it logs `Logging.Error("GraphSave", "[GraphSave] Restore refused: {error.Message} Nothing was changed.")` and returns, without applying values or starting the runner.
- **graphgameflow `Boot(snapshot)`** (existing `void`, possibly deferred): after the existing null/running/no-graph guards, a stacked snapshot is checked against `_graph`. On failure it logs `Logging.Error("GraphGameFlow", "[GraphGameFlow] GraphFlowDriver.Boot: snapshot does not match graph '{name}': {error.Message} Staying inert.")` and returns before the context or runner is created.

**Rationale**: FR-008 needs a check the consumer can call ahead of the deferred `Boot`. FR-009 needs loud failure at every entry point. Existing `void` signatures cannot change (FR-018), and both already report problems by logging (`Boot` warns and stays inert; `JsonFileGraphSaveStore` logs errors).

---

## R7: Capture never throws; fresh-frame report (D1-A)

**Decision**: `Capture(runner, context)` = `Capture(context, top graph id, top node id)` + `Frames` from `runner.GetExecutionPath()`, then a fresh-frame scan. For each frame `i ≥ 1` with mode `Fresh`, if `i < last`, or if `i == last` and `NodeId != runner.CurrentGraph.EntryNodeId`, it logs one `Logging.Error("GraphSave", "[GraphSave] Capture: frame {i} (graph '{id}') runs on its own fresh context, whose values are not saved; restoring this snapshot restarts that frame from its graph's defaults.")`. The snapshot is still returned.

**Rationale**:
- The consumer captures inside `OnNodeEntered`. An exception there propagates out of `EnterCurrentNode` before `OnNodeCompleted`/await subscription, which leaves the run stuck (spec FR-004).
- At a fresh sub-graph's entry node, `new BaseContext()` + `InitFromGraph` + the replayed entry effects reproduce the original state exactly, so no report is needed. Everywhere else data is lost, and the report is loud.
- The scan needs only `runner.CurrentGraph.EntryNodeId` for the deepest frame. Enclosing fresh frames are always lossy, since they progressed to their sub-graph node.
- `GetExecutionPath()` cannot throw: every frame's `Graph` is non-null by construction (`Start`/`StartFrom` reject a null graph, `EnterSubGraph` refuses a null target). A null runner gives an empty path.

---

## R8: Serialization compatibility

**Decision**: `public List<Frame> Frames = new List<Frame>();` with `HasStack => Frames != null && Frames.Count > 0` and `RootGraphId => HasStack ? Frames[0].GraphId : GraphId` as get-only properties.

**Verified behavior**:
- **JsonUtility** serializes public fields of `[Serializable]` classes (including nested lists and enums as ints) and never properties. A JSON without `Frames` reads back as an empty list, the same mechanism the existing `Collection.Counts` back-compat relies on (`GraphRunSnapshot.cs:47-63`).
- **Newtonsoft** (`com.faolline.savesystem.json` `JsonSaveSystem` uses `JsonConvert.SerializeObject/DeserializeObject`, `Library/PackageCache/com.faolline.savesystem.json@e3013ce954f8/Runtime/JsonSaveSystem.cs:32,90`) serializes public fields *and* public get-only properties. `HasStack`/`RootGraphId` therefore appear as extra keys in its JSON. On read, get-only properties without a setter are skipped, so they are recomputed from `Frames` and harmless. Covered by a round-trip test through the real `JsonSaveSystem`.

**Alternatives considered**: methods instead of properties (rejected: properties read better at the consumer's call site, and the extra Newtonsoft keys are inert); a stored `RootGraphId` field (rejected: redundant with `Frames[0]` and can diverge).

---

## R9: Snapshots without a stack; single-level stacked snapshots

**Decision**:
- No stack (`Capture(context, id, node)`, or a JSON without `Frames`): `Restore` is byte-for-byte today's path (`ApplyTo(replace)` + `StartFrom(graph, CurrentNodeId or entry)`). `TryValidate` returns `true` for any non-null graph (there is nothing to check, and the old path did not check). `RootGraphId` = `GraphId`. `GetExecutionPath()` = empty.
- Stacked with one frame (captured at root level): the full check applies, so the graph id must equal the saved root id (FR-015). Restoring into a different graph now fails loudly instead of re-entering a same-named node by chance. CHANGELOG note; acceptable because no saves exist and that was never a supported use.

---

## R10: Driver integration

**Decision**: `Boot(snapshot, context, registry)`:
- keeps its existing order of guards: deferred-if-reentrant, null snapshot, already running, no graph;
- for a stacked snapshot it runs `TryValidate(_graph)`, and on failure logs and returns before the context or runner is created;
- otherwise it creates the context, calls `ApplyTo(replace)`, creates the runner and subscribes as today, then calls `StartFromPath(_graph, snapshot.GetExecutionPath(), _context, registry)` (stacked) or `StartFrom` (no stack), then `DrainAutoAdvance()`.

**Rationale**: the supplied `context` must stay untouched on failure (spec US3 #2), so the check comes before `ApplyTo`. The deferred re-entrant path replays the same method, so it gets the same check for free.

---

## R11: Scoped frames (D2-A)

**Decision**: rebuild a `Scoped` frame with `BeginLocalContext(graph i)` on the shared context, *after* graphsave's `ApplyTo`. Saved globals are applied first, then the layer opens seeded with its graph's declared defaults. Local values written before the save are not restored. Documented, no capture message: `GetAllVariables` already excludes them by design (`BaseContext.cs:220-227`).

Two nested scoped frames: `BaseContext` keeps one flat overlay (`BaseContext.cs:242-250`, "nested local contexts are not supported"). Replaying `BeginLocalContext` per scoped frame reproduces the live run exactly, including its warning, and nothing more is attempted.

---

## R12: Testing approach

**Decision**:
- **graphcore** `ExecutionPathTests`: read path (empty / 1 / 2 / 3 levels, modes), restore at 2 and 3 levels (top node, `CurrentGraph`, `OnNodeEntered` fired once for the top only, parent `OnEnterActions` + executor not re-run), continuation through every `End` to `OnEnded` once, top frame on a `SubGraphNodeData`, each context mode (shared write visible in root; scoped layer present/seeded/discarded on end; fresh separate + seeded), history empty and depth from the root. `ExecutionPathValidationTests`: every reason with `FrameIndex`/`DeepestValidFrameIndex`, `StartFromPath` throwing with an unchanged runner, `TryResolve` with no side effect.
- **graphsave** `GraphStackSnapshotTests`: capture at 1/2/3 levels, JsonUtility round-trip, old JSON without `Frames`, explicit-ids capture, restore 2/3 levels + continuation, mismatch → error logged + context untouched + runner idle, single-level stacked into another graph fails, fresh-frame capture report (and none at entry), capture from inside `OnNodeEntered` (the consumer pattern) leaves the run intact.
- **graphsave.savesystem**: stacked snapshot through the real `JsonSaveSystem`.
- **graphgameflow** `GraphFlowDriverSnapshotTests`: boot with stack from root, continuation into the root with driver events, wrong root → error + inert + supplied context untouched, no-stack unchanged, re-entrant boot from `OnEnded`, consumer pattern (capture on Start/checkpoint `OnNodeEntered` → JSON → new driver boots).
- Run headless via batchmode, targeted while red/green, full suite at the end (baseline before changes recorded).
