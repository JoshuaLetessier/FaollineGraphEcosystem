# Research: GraphCore Execution Runtime

## Decision 1: BaseContext Parameter Storage — Single Object Dictionary

**Decision**: `BaseContext` uses a single `Dictionary<string, object>` internally. Typed
accessors (`Set<T>`, `Get<T>`, `TryGet<T>`) perform runtime type validation and boxing/unboxing.
Supported types are `bool`, `int`, `float`, `string`.

**Rationale**: One dictionary is simpler to clone, iterate, and maintain than four typed
dictionaries. Boxing `bool`/`int`/`float` to `object` has negligible allocation cost in a
graph execution context (parameters change rarely compared to frame updates). Type enforcement
is provided at the API boundary — callers are told at runtime if they attempt an unsupported type.

**Alternatives considered**:
- *Four typed dictionaries (`Dictionary<string, bool>` × 4)*: Rejected — requires four `ContainsKey`
  lookups for `Has()`, four iteration loops for `DeepClone()`, and four parallel change-event maps.
  Duplicates logic with no measurable performance benefit for typical graph parameter counts (< 50).
- *Generic class `TypedParam<T>`*: Rejected — not serializable by Unity; introduces a wrapper
  with no benefit since this class is runtime-only.

---

## Decision 2: BaseContext Concreteness — Concrete Class, Virtual DeepClone

**Decision**: `BaseContext` is changed from `abstract` to `concrete`. `DeepClone()` is declared
`virtual` so subclasses can override to copy their additional domain fields.

**Rationale**: The data layer declared `BaseContext` as abstract because it had no implementation —
any instantiation would be invalid. Now that it has a full implementation, making it abstract
would force every consumer (including test code) to create a subclass just to get a context,
violating Principle V (YAGNI). Making `DeepClone()` virtual is the minimal extensibility hook
needed for downstream libs that add fields.

**Semver note**: Changing `abstract` → `concrete` is not a breaking change for existing subclasses.
No consumer could have called `new BaseContext()` before (it was abstract), so this change only
opens new usage patterns.

**Alternatives considered**:
- *Keep abstract, add `ConcreteBaseContext` concrete subclass*: Rejected — unnecessary indirection.
  Adds a type with no purpose other than to route around the abstract modifier.
- *Make DeepClone abstract*: Rejected — forces every subclass to re-implement the parameter-copy
  logic. The base implementation is correct for any context that only has the standard parameter dict.

---

## Decision 3: Change Notification — Per-Key Action List

**Decision**: `OnParameterChanged` and `OffParameterChanged` manage per-key subscriber lists
stored in `Dictionary<string, List<Action<object>>>`. Subscribers receive the new value as `object`.

**Rationale**: Per-key subscriptions mean a subscriber on `"IsComplete"` is not notified when
`"Score"` changes — standard blackboard behavior. Using `Action<object>` rather than a typed
`Action<T>` avoids generic overhead at the subscription site and matches common Unity event
patterns. Callers cast to the expected type, which is known at subscription time.

**Alternatives considered**:
- *Single global `Action<string, object>` event*: Rejected — every subscriber would need to
  filter by key, adding boilerplate in consumer code and firing even for irrelevant keys.
- *Typed `Action<T>` per subscriber*: Rejected — cannot store in a homogeneous list without
  wrapping. `Action<object>` with a cast is the accepted C# pattern for this scenario.

---

## Decision 4: HistoryStack Data Structure — Bounded List

**Decision**: History is stored in a `List<HistoryEntry>` where index 0 is oldest and index
(Count-1) is newest. Capping: when a new entry is appended and `HistoryDepth > 0` and
`Count > HistoryDepth`, `RemoveAt(0)` is called. `GoBack()` removes and restores the last entry.

**Rationale**: `List<T>` with indexed removal is the simplest bounded LIFO structure that
supports both "push" (append) and "pop from end" (GoBack) and "trim from front" (cap). For
typical `HistoryDepth` values (10-100 entries), `RemoveAt(0)` on a list of `BaseNodeData`
references is O(n) but negligible.

**Alternatives considered**:
- *`LinkedList<HistoryEntry>`*: Slightly more efficient for front removal, but adds API complexity
  (no index access, less readable). Not worth the trade-off for typical history sizes.
- *`Stack<HistoryEntry>` + separate age tracking*: `Stack<T>` has no O(1) remove-from-bottom.
  Would need a `Queue` as a secondary structure. Too complex for Principle V.
- *Ring buffer*: Maximum efficiency but significant implementation complexity. Rejected — YAGNI.

---

## Decision 5: INodeExecutor.Undo Default No-Op — C# 8 Default Interface Method

**Decision**: `INodeExecutor.Undo` is declared with a default no-op body using a C# 8 default
interface method: `void Undo(BaseNodeData node, BaseContext context) { }`.

**Rationale**: Unity 6000.x compiles with the Roslyn C# 9 compiler (C# 8 features are fully
available). Default interface methods allow `Undo` to be optional — implementors that don't
need undo support get the no-op for free, while those that do simply override it. This avoids
forcing an abstract `Undo` that every executor must implement even when undo is not applicable.

**Alternatives considered**:
- *Abstract `Undo` method*: Rejected — forces every executor (including trivial stub executors
  in tests) to implement a body. Violates YAGNI.
- *Separate `IUndoableNodeExecutor` interface*: Rejected — adds a second interface that `BaseRunner`
  would need to check via `is` cast. More complex than a default method.

---

## Decision 6: BaseRunner — Synchronous, Event-Driven Proceed

**Decision**: `BaseRunner` is fully synchronous. After `Execute()` is called on the executor,
the runner raises the `OnNodeCompleted` event and then pauses — the caller is responsible for
calling `Proceed()` or `ChooseById()` to advance. The runner does NOT auto-advance.

**Rationale**: Auto-advancing would be correct for non-interactive graphs (linear cutscenes)
but wrong for interactive graphs (dialogue, choices). By stopping after `OnNodeCompleted`,
the runner gives the caller full control over pacing. A linear graph can auto-call `Proceed()`
from its `OnNodeCompleted` handler; an interactive graph waits for the player.

**Alternatives considered**:
- *Auto-advance by default with opt-out flag*: Rejected — hidden state that callers forget to
  set. "Always stop, caller decides" is simpler and more explicit.
- *`async Task` for each step*: Rejected — async is out of scope per spec. Adds complexity
  and Unity's async support is inconsistent across platforms.

---

## Decision 7: SubGraph Context — Direct Pass vs. Fresh Clone

**Decision**: `InheritParentContext = true` passes the SAME context object reference into the
sub-graph. `InheritParentContext = false` creates a fresh context via `new BaseContext()` and
calls `InitFromGraph(targetGraph)`.

**Rationale**: Sharing the reference for `InheritParentContext = true` means changes inside the
sub-graph are immediately visible to the parent graph (same blackboard). This is the expected
behavior for modular graphs that share state. A fresh context for `= false` gives the sub-graph
its own isolated parameter namespace.

**Note**: When the sub-graph ends and the parent stack frame resumes, the shared context is
still valid (it was never replaced). No restore step is needed for the shared-reference case.

**Alternatives considered**:
- *Always clone context on sub-graph entry, merge on exit*: Rejected — merge semantics are
  ambiguous (which keys win? what if sub-graph adds keys?). Far more complex with no clear benefit.

---

## Decision 8: Tests Assembly — Create if Absent

**Decision**: If `Tests/EditMode/com.faolline.graphcore.Tests.EditMode.asmdef` does not exist,
it is created as part of this feature's task list. It references `com.faolline.graphcore.Runtime`
and Unity's test runner assemblies.

**Rationale**: The data layer (001) specified the test assembly in its research.md but the task
of creating it may have been deferred. The execution layer tests require it. Creating it once
here is correct — it will serve all future features.
