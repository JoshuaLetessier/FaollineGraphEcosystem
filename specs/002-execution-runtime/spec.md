# Feature Specification: GraphCore Execution Runtime

**Feature Branch**: `002-execution-runtime`

**Created**: 2026-05-27

**Status**: Draft

**Input**: User description: "Je veux construire la couche d'exécution de com.faolline.graphcore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Drive a Linear Graph from Start to End (Priority: P1)

A developer instantiates a `BaseRunner`, loads a graph asset, and calls `Start()` then
`Proceed()` to step through a Start → Statement → End graph. The runner transitions through
each node, executing conditions, actions, and the node's registered executor, until the
`Ended` state is reached.

**Why this priority**: This is the minimum viable path through the execution layer. Without
it, no graph-based feature in any ecosystem lib can function at runtime.

**Independent Test**: Create a `BaseGraph` with three nodes, wire up a `NodeExecutorRegistry`
with stub executors, call `Start()` then `Proceed()` twice, and assert the runner reaches
`Ended` with the correct node sequence visited.

**Acceptance Scenarios**:

1. **Given** a `BaseGraph` with Start → Statement → End, **When** `Start()` is called,
   **Then** the runner state is `NodeReady` and the current node is the StartNode.
2. **Given** the runner is `NodeReady` on Statement, **When** `Proceed()` is called,
   **Then** EntryConditions are evaluated, OnEnterActions are executed, the executor's
   `Execute()` is called, OnExitActions are executed, and the runner advances to End.
3. **Given** the runner reaches an `EndNode`, **When** execution completes,
   **Then** the runner state transitions to `Ended`.
4. **Given** a runner in `Ended` state, **When** `Proceed()` is called,
   **Then** the call is a no-op (does not throw, does not change state).

---

### User Story 2 - Use BaseContext as a Typed Blackboard (Priority: P1)

A developer creates a `BaseContext` initialized from a `BaseGraph`, reads and writes typed
parameters (`bool`, `int`, `float`, `string`) during graph execution, and subscribes to
change events on specific keys. Cloning the context preserves values but discards listeners.

**Why this priority**: `BaseContext` is the shared state carrier for conditions and actions.
Without it, `BaseCondition.Evaluate` and `BaseAction.Execute` have no runtime data to work with.

**Independent Test**: Instantiate a `BaseContext`, call `InitFromGraph()` with a graph that
has four parameters (one of each type), verify each value is readable via `Get<T>()`, mutate
two, verify `OnParameterChanged` fires, call `DeepClone()`, and verify the clone has the
same values but a fresh subscriber list.

**Acceptance Scenarios**:

1. **Given** a `BaseGraph` with `ParameterData { Key="IsComplete", Type=Bool, DefaultValue="true" }`,
   **When** `InitFromGraph()` is called, **Then** `Get<bool>("IsComplete")` returns `true`.
2. **Given** a `BaseContext` with subscriber on `"Score"`, **When** `Set<int>("Score", 42)` is called,
   **Then** the subscriber is invoked with the new value.
3. **Given** a `BaseContext` with a subscriber, **When** `DeepClone()` is called,
   **Then** the clone contains the same parameter values but the subscriber is not copied.
4. **Given** a `BaseContext`, **When** `TryGet<float>("Unknown", out var v)` is called,
   **Then** it returns `false` and `v` is default.
5. **Given** a `BaseContext` with key `"Flag"`, **When** `OffParameterChanged("Flag", handler)` is called,
   **Then** subsequent changes to `"Flag"` no longer invoke `handler`.

---

### User Story 3 - Register and Resolve Node Executors (Priority: P1)

A developer registers an `INodeExecutor` implementation for a specific node type into a
`NodeExecutorRegistry`, then resolves it by `NodeType` string. The runner uses the registry
to dispatch execution to the correct handler for each node.

**Why this priority**: Without the executor registry, `BaseRunner` cannot invoke type-specific
logic — it has no way to know what to do when it reaches a `StatementNodeData` or a custom lib node.

**Independent Test**: Register two executors for two different node types, resolve each by
type string, verify the correct instance is returned, and verify that requesting an unregistered
type returns null (or throws, per documented contract).

**Acceptance Scenarios**:

1. **Given** a `NodeExecutorRegistry` with a registered executor for `"graphcore/statement"`,
   **When** `GetExecutor("graphcore/statement")` is called, **Then** the registered instance is returned.
2. **Given** a `NodeExecutorRegistry` with no executor for `"graphcore/unknown"`,
   **When** `GetExecutor("graphcore/unknown")` is called, **Then** `null` is returned.
3. **Given** a registered executor, **When** `Undo(nodeData, context)` is called on a
   default-implementation executor, **Then** it completes without error (no-op).
4. **Given** a `NodeExecutorRegistry`, **When** the same `NodeType` is registered twice,
   **Then** the second registration replaces the first without error.

---

### User Story 4 - Navigate SubGraphs with Cycle Detection (Priority: P2)

A developer adds a `SubGraphNodeData` node to a graph. When `BaseRunner` reaches it, the
sub-graph is pushed onto the execution stack and execution continues in the sub-graph.
When the sub-graph ends, execution returns to the parent graph. A cycle raises a
`GraphCycleException` before any sub-graph execution begins.

**Why this priority**: SubGraph nesting is the mechanism for modular graph composition across
the entire ecosystem. Cycle detection is a non-negotiable safety requirement per the constitution.

**Independent Test**: Build a parent graph with a `SubGraphNodeData` pointing to a child graph.
Drive the runner through the sub-graph entry, verify the stack depth increases, drive to End
in the sub-graph, verify the stack pops back. Then create a mutual reference cycle and verify
`GraphCycleException` is thrown on `Start()`.

**Acceptance Scenarios**:

1. **Given** a parent graph with a `SubGraphNodeData`, **When** the runner reaches it,
   **Then** the runner state is `Paused`, the sub-graph is pushed onto the graph stack,
   and the runner resumes in the sub-graph from its entry node.
2. **Given** the runner is executing a sub-graph and reaches an `EndNode`,
   **Then** the sub-graph is popped from the stack and execution resumes in the parent graph.
3. **Given** graph A contains a `SubGraphNodeData` pointing to graph B, and graph B
   contains a `SubGraphNodeData` pointing to graph A, **When** the runner would enter
   graph B's sub-graph back to A, **Then** `GraphCycleException` is thrown.
4. **Given** a `SubGraphNodeData` with `InheritParentContext = true`,
   **When** the sub-graph begins, **Then** the sub-graph receives the parent context directly.
5. **Given** a `SubGraphNodeData` with `InheritParentContext = false`,
   **When** the sub-graph begins, **Then** the sub-graph receives a fresh context initialized
   from the sub-graph's own `ParameterData`.

---

### User Story 5 - Rewind Execution with History (Priority: P3)

A developer uses `GoBack()` to undo the last node transition and `GoBackToCheckpoint()` to
rewind to the most recent node that was marked `IsCheckpoint = true`. The history depth is
capped by `BaseGraph.HistoryDepth`.

**Why this priority**: History is a differentiating feature of graphcore (save/restore for
adventure games, dialogue backtracking). It is lower priority than basic execution and
sub-graph navigation.

**Independent Test**: Execute a 5-node graph with one checkpoint at node 3, call `GoBack()`
twice from node 5 to land on node 3, verify context is restored. Then call `GoBackToCheckpoint()`
from node 5 directly. Verify history is capped after exceeding `HistoryDepth`.

**Acceptance Scenarios**:

1. **Given** a runner that has progressed through nodes A → B → C, **When** `GoBack()` is called,
   **Then** the runner is back on node B with context restored to its state at node B.
2. **Given** a runner at node C where node A is a checkpoint, **When** `GoBackToCheckpoint()` is called,
   **Then** the runner is back on node A with the context state from that snapshot.
3. **Given** a `BaseGraph` with `HistoryDepth = 3`, **When** 5 nodes are visited,
   **Then** only the last 3 snapshots are retained; earlier snapshots are discarded.
4. **Given** a `BaseGraph` with `HistoryDepth = 0`, **When** nodes are visited,
   **Then** the history stack grows without bound.
5. **Given** an empty history stack, **When** `GoBack()` is called, **Then** the call
   is a no-op (does not throw, does not change state).

---

### Edge Cases

- A graph with no `EntryNodeId` set raises an error on `Start()` (invalid graph state).
- A node with no outgoing edges and a non-End `NodeType` terminates execution silently with an `Ended` state.
- A `ChoiceNodeData` with all choices gated by failing conditions leaves the runner in a stuck state — caller must handle via `OnRunnerStuck` event or equivalent.
- `GoBack()` when history contains sub-graph state restores the full graph stack, not just the context.
- `DeepClone()` on a `BaseContext` with no parameters returns a valid empty context.
- Registering an executor with a null `NodeType` string MUST throw `ArgumentNullException`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `BaseContext` MUST provide `Set<T>(string key, T value)`, `Get<T>(string key) → T`,
  `TryGet<T>(string key, out T value) → bool`, and `Has(string key) → bool` for `T` in
  `{bool, int, float, string}`.
- **FR-002**: `BaseContext` MUST expose `OnParameterChanged(string key, Action<object> handler)`
  and `OffParameterChanged(string key, Action<object> handler)` for per-key change subscriptions.
- **FR-003**: `BaseContext.DeepClone()` MUST return a new `BaseContext`-derived instance with
  all parameter values copied and all subscriptions cleared.
- **FR-004**: `BaseContext.InitFromGraph(BaseGraph graph)` MUST populate the context with each
  `ParameterData` in `graph.Parameters`, converting `DefaultValue` (string) to the declared
  `ParameterType`.
- **FR-005**: `BaseContext` MUST be a pure C# class — no `MonoBehaviour`, no `ScriptableObject`,
  no Unity lifecycle dependency.
- **FR-006**: `INodeExecutor` MUST declare: `string NodeType { get; }`,
  `void Execute(BaseNodeData node, BaseContext context)`,
  and `void Undo(BaseNodeData node, BaseContext context)` with a default no-op body (C# 8 default interface method).
- **FR-007**: `NodeExecutorRegistry` MUST provide `Register(INodeExecutor executor)` and
  `GetExecutor(string nodeType) → INodeExecutor` (returns `null` for unregistered types).
- **FR-008**: `NodeExecutorRegistry.Register` called twice for the same `NodeType` MUST replace
  the first registration silently.
- **FR-009**: `BaseRunner` MUST implement a state machine with states: `Idle`, `NodeReady`,
  `Paused` (SubGraph entered), `Ended`.
- **FR-010**: `BaseRunner` MUST maintain a `Stack<GraphExecutionState>` for nested SubGraph
  execution — push on `SubGraphNodeData` entry, pop on `EndNode`.
- **FR-011**: `GraphExecutionState` MUST hold: the current `BaseGraph`, current node id, and
  available outgoing edges for the current node.
- **FR-012**: `BaseRunner` MUST maintain a `HistoryStack` of snapshots
  `{ NodeId, GraphStack clone, Context clone }` at each node transition, capped by
  `BaseGraph.HistoryDepth` (`0` = unlimited). History snapshotting occurs AFTER `OnExitActions`
  and BEFORE advancing to the next node.
- **FR-013**: `BaseRunner.Start(BaseGraph graph, BaseContext context, NodeExecutorRegistry registry)`
  MUST initialize execution at `graph.EntryNodeId` and transition to `NodeReady`.
- **FR-014**: `BaseRunner.Proceed()` MUST advance to the next node by evaluating outgoing edges
  and selecting the first whose `Condition` evaluates to `true` (or any unconditional edge).
- **FR-015**: `BaseRunner.ChooseById(string id)` MUST select the outgoing edge or choice whose
  `Id` matches, ignoring condition evaluation.
- **FR-016**: `BaseRunner.GoBack()` MUST restore the most recent history snapshot. If the stack
  is empty, the call is a no-op.
- **FR-017**: `BaseRunner.GoBackToCheckpoint()` MUST restore the most recent snapshot whose
  node had `IsCheckpoint = true`. If none exists, the call is a no-op.
- **FR-018**: The execution sequence per node MUST be:
  1. Evaluate `EntryConditions` → skip node if any fails (or raise stuck event)
  2. Execute `OnEnterActions`
  3. Call `INodeExecutor.Execute(node, context)`
  4. Raise `OnNodeCompleted` — caller responds via `Proceed()` or `ChooseById()`
  5. Execute `OnExitActions`
  6. Evaluate outgoing edges to determine next node
  7. Append snapshot to HistoryStack
  8. Advance to next node
- **FR-019**: `BaseRunner` MUST raise `GraphCycleException` if a `SubGraphNodeData.TargetGraph.GraphId`
  is already present in the current `GraphStack`.
- **FR-020**: All `BaseRunner` events MUST use `C# Action<T>` — no `MonoBehaviour`, no `UnityEvent`.
- **FR-021**: `GraphCycleException` MUST carry the offending `GraphId` in its message.

### Key Entities

- **BaseContext**: Typed parameter blackboard with change notifications and deep clone support.
- **INodeExecutor**: Per-node-type execution interface with default no-op `Undo`.
- **NodeExecutorRegistry**: Registry mapping `NodeType` string → `INodeExecutor`.
- **BaseRunner**: Headless state machine driving graph traversal and history.
- **GraphExecutionState**: One level of graph traversal state (graph + current node + available edges).
- **HistoryEntry**: Immutable snapshot `{ NodeId, GraphStack clone, Context clone }`.
- **GraphCycleException**: Exception thrown on cycle detection, carrying the offending `GraphId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can execute a 3-node linear graph (Start → Statement → End) with
  zero Unity lifecycle calls, validated by a pure C# EditMode test.
- **SC-002**: All parameter type conversions from `ParameterData.DefaultValue` (string) to
  `bool`, `int`, `float`, `string` are correct for 100% of valid inputs.
- **SC-003**: `GoBack()` and `GoBackToCheckpoint()` correctly restore state in 100% of test
  scenarios, including cross-SubGraph rewind.
- **SC-004**: `GraphCycleException` is raised before any node of a cyclic sub-graph executes,
  with the offending `GraphId` in the message.
- **SC-005**: Zero `MonoBehaviour`, `MonoScript`, or `UnityEvent` references exist in the
  execution layer Runtime assembly.
- **SC-006**: A downstream lib can register a custom `INodeExecutor` and have it invoked by
  `BaseRunner` without modifying any graphcore file.
- **SC-007**: The execution layer compiles with zero errors and zero warnings in a fresh Unity
  project alongside the data layer (`001-data-layer`), with no ecosystem lib installed.

## Assumptions

- `BaseContext` is a concrete class (not abstract) at the graphcore level, unlike the
  placeholder declared in the data layer. Ecosystem libs may subclass it to add domain state.
- The existing `BaseContext.cs` in `Runtime/Graph/` is currently an empty abstract class; this
  feature replaces its body with the full blackboard implementation.
- `INodeExecutor.Undo` default no-op is implemented as a C# 8 default interface method
  (`void Undo(BaseNodeData, BaseContext) { }`), requiring Unity 2021+ (C# 9 compiler).
- `BaseRunner` is synchronous — `Proceed()` runs the full node sequence to completion before
  returning. Async execution is out of scope for this feature.
- `BaseRunner` does not own graph loading or saving; it receives an already-loaded `BaseGraph`
  and a pre-constructed `BaseContext`.
- `OnNodeCompleted` is an event raised by `BaseRunner` after `Execute()` returns; the consumer
  calls `Proceed()` or `ChooseById()` to advance. The runner does not auto-advance.
- History snapshots use shallow clones for the `GraphStack` (the stack structure is cloned,
  but the `BaseGraph` asset references are shared — they are read-only data layer objects).
- `BaseContext.DeepClone()` is the method used to snapshot context in history; it copies
  parameter values only (correct per spec).
- `HistoryDepth = 0` means unlimited (matching the `BaseGraph.HistoryDepth` convention from
  the data layer spec).
- The `BaseRunner` targets Unity 6.0 (Unity 6000.x), consistent with the data layer.
