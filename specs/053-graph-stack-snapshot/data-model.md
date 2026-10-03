# Data Model: Full Graph-Stack Save & Restore

## Execution path (graphcore, runtime only)

An ordered list `path[0..n-1]` of **frames**, root first, deepest last. Produced by `BaseRunner.GetExecutionPath()`, consumed by `GraphRunPath.TryResolve` and `BaseRunner.StartFromPath`.

### GraphRunFrame (immutable)

| Field | Type | Meaning |
|---|---|---|
| `GraphId` | string | `BaseGraph.GraphId` of the graph this frame runs |
| `NodeId` | string | The node this frame is on. For `i < n-1` it is always a `SubGraphNodeData` (the frame is paused on it while frame `i+1` runs). |
| `ContextMode` | `GraphRunContextMode` | How this frame holds values relative to frame `i-1`. Ignored for frame 0. |

### GraphRunContextMode

| Value | Int | Live origin (`EnterSubGraph`) | Restore |
|---|---|---|---|
| `Shared` | 0 | `InheritParentContext = true`, `OpensScope = false`: same context | same context as frame `i-1` |
| `Scoped` | 1 | `OpensScope = true`: same context + `BeginLocalContext(target)` | same context + `BeginLocalContext(graph i)`, `OpenedLocalContext = true`; layer values = declared defaults (D2-A) |
| `Fresh` | 2 | both false: `new BaseContext()` + `InitFromGraph(target)` | `new BaseContext()` + `InitFromGraph(graph i)`; values = declared defaults (D1-A) |

Capture reads the mode from the live frame (`OpenedLocalContext`, context identity), restore compares it against the current `SubGraphNodeData` flags (`OpensScope` first, then `InheritParentContext`).

### Validation (path ↔ content), first failure wins

For `i = 0..n-1` (graph `0` = supplied root; graph `i ≥ 1` = `TargetGraph` of frame `i-1`'s node):

1. `i == 0`: `path[0].GraphId == root.GraphId`, else `RootGraphMismatch`.
2. `path[i].GraphId` not seen before, else `RepeatedGraph`.
3. `path[i].NodeId` non-empty and present in graph `i`, else `NodeMissing`.
4. If `i < n-1`, node `i` is a `SubGraphNodeData` (`NotASubGraphNode`) with non-null `TargetGraph` (`SubGraphTargetMissing`), whose `GraphId == path[i+1].GraphId` (`SubGraphTargetMismatch`) and whose flags give `path[i+1].ContextMode` (`ContextModeMismatch`).

Pre-checks: `NullRootGraph` (root null), `EmptyPath` (path null or empty), null frame → `NodeMissing`.

### GraphRunPathError

| Field | Type | Meaning |
|---|---|---|
| `Reason` | `GraphRunPathErrorReason` | `NullRootGraph`, `EmptyPath`, `RootGraphMismatch`, `RepeatedGraph`, `NodeMissing`, `NotASubGraphNode`, `SubGraphTargetMissing`, `SubGraphTargetMismatch`, `ContextModeMismatch` |
| `FrameIndex` | int | Frame where the check failed (-1 for `EmptyPath`) |
| `DeepestValidFrameIndex` | int | Deepest `k` such that `path[0..k]` alone passes the check; -1 if none |
| `Message` | string | One English sentence with reason, frame and ids |

`DeepestValidFrameIndex` by reason: `NullRootGraph`/`EmptyPath`/`RootGraphMismatch` → -1; `RepeatedGraph`/`NodeMissing` at `i` → `i-1`; the four sub-graph-node reasons at `i` → `i`.

## GraphRunSnapshot (graphsave, serialized)

Existing fields unchanged: `GraphId`, `CurrentNodeId` (deepest frame), `Variables`, `Collections`, `RaisedSignals`.

| Member | Kind | Serialized | Meaning |
|---|---|---|---|
| `Frames` | `List<Frame>` field | yes (new key, empty list when absent) | The execution path, root first |
| `Frame.GraphId` / `Frame.NodeId` / `Frame.ContextMode` | public fields | yes | Mirror of `GraphRunFrame` |
| `HasStack` | get-only property | JsonUtility no; Newtonsoft writes it, ignores it on read | `Frames` non-empty |
| `RootGraphId` | get-only property | same | `Frames[0].GraphId`, else `GraphId` |

Invariant when `HasStack`: `GraphId == Frames[^1].GraphId` and `CurrentNodeId == Frames[^1].NodeId` (both written by the same capture).

### Value ownership

- Root context values (`Variables`, `Collections`, `RaisedSignals`) are captured from the context the caller passes, normally the root context (`driver.Context`).
- Shared frames hold no values of their own.
- A scoped frame's layer values and a fresh frame's values are not stored (D2-A, D1-A). Capture reports the lossy fresh cases.

## Restore state transitions

```text
                 TryValidate / TryResolve
snapshot ──────────────────────────────────┐
                                            ▼
                          ┌── invalid ──► error (logged / returned / thrown); context + runner unchanged
                          │
                          └── valid ────► ApplyTo(root context, replace)
                                            ▼
                                        StartFromPath: frames rebuilt (no node entered)
                                            ▼
                                        top node entered (conditions, enter actions, executor, OnNodeEntered)
                                            ▼
                                        normal run: child End → pop → parent continues from its SubGraph node
```
