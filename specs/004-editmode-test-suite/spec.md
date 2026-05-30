# Feature Specification: EditMode Test Suite

**Feature Branch**: `004-editmode-test-suite`

**Created**: 2026-05-28

**Status**: Draft

**Input**: User description: "Je veux construire la suite de tests EditMode de com.faolline.graphcore."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - BaseRunner State Machine Coverage (Priority: P1)

A developer can trust that `BaseRunner` is thoroughly tested across its full state machine:
all transitions (`Idle → NodeReady → Ended`), entry/exit action sequencing, executor dispatch,
`OnStuck` on failed entry conditions, `ChooseById` edge selection, and no-op guards after
`Ended`. Each test is isolated with a fresh `BaseGraph`, `BaseContext`, and `NodeExecutorRegistry`.

**Why this priority**: `BaseRunner` is the execution core that every downstream lib depends on.
Full state-machine coverage is required before any other runtime tests are meaningful.

**Independent Test**: Can be run as a standalone `BaseRunnerTests` fixture — a passing suite
proves the runner's lifecycle contract is sound.

**Acceptance Scenarios**:

1. **Given** a fresh runner, **When** `Start` is called with a valid graph, **Then** `State == NodeReady` and `OnNodeEntered` + `OnNodeCompleted` fire for the entry node.
2. **Given** `State == NodeReady`, **When** `Proceed` is called, **Then** exit-actions run on the current node, a history snapshot is appended, and the next node is entered.
3. **Given** an `EndNodeData` as the current node and `Proceed` is called, **Then** `State == Ended` and `OnEnded` fires with the correct `EndReason`.
4. **Given** `State == Ended`, **When** `Proceed` is called again, **Then** the call is a no-op — state and events are unchanged.
5. **Given** a node with a failing `EntryCondition`, **When** the runner tries to enter it, **Then** `OnStuck` fires and `State` remains `NodeReady`.
6. **Given** multiple outgoing edges with a matching port name, **When** `ChooseById` is called, **Then** the matching edge is selected, bypassing condition evaluation.
7. **Given** a registered `INodeExecutor` for a node type, **When** the node is entered, **Then** `Execute` is called once with the correct node and context.

---

### User Story 2 - BaseContext Typed Blackboard Coverage (Priority: P1)

A developer can trust that `BaseContext` correctly stores and retrieves `bool`, `int`, `float`,
and `string` values, fires `OnParameterChanged` events only on the correct key, and produces a
`DeepClone` where values are copied but subscriptions are not.

**Why this priority**: `BaseContext` is the shared state carrier for every graph execution.
Correctness bugs here would silently corrupt all downstream lib state.

**Independent Test**: Can be run as a standalone `BaseContextTests` fixture against only the
`BaseContext` class — no runner or graph required.

**Acceptance Scenarios**:

1. **Given** `Set<T>` called with each supported type (`bool`, `int`, `float`, `string`), **Then** `Get<T>` returns the exact value set.
2. **Given** `Set<T>` called with an unsupported type (e.g., `double`), **Then** `ArgumentException` is thrown.
3. **Given** a subscriber registered via `OnParameterChanged("key", handler)`, **When** `Set` is called for that key, **Then** the handler fires with the new value.
4. **Given** a subscriber on key "A", **When** `Set` is called for key "B", **Then** the handler does not fire.
5. **Given** `OffParameterChanged` called to remove a handler, **When** `Set` is called again, **Then** the handler does not fire.
6. **Given** a context with values, **When** `DeepClone()` is called, **Then** the clone has the same values and mutating the original does not affect the clone.
7. **Given** a context with a subscriber, **When** `DeepClone()` is called, **Then** setting a value on the clone does not fire the original's subscriber.

---

### User Story 3 - History Integrity Coverage (Priority: P1)

A developer can trust that history snapshots are structurally sound: `GoBack()` restores the
runner to the exact prior state (node + context values), `GoBackToCheckpoint()` finds the nearest
checkpoint node, `HistoryDepth` caps the buffer by evicting oldest entries, and `depth == 0`
means unlimited history.

**Why this priority**: History is a safety-critical feature used by downstream libs for
undo/replay. A single edge case in cap enforcement or snapshot restoration can corrupt gameplay.

**Independent Test**: Can be run as a standalone `HistoryTests` fixture — uses `BuildChainGraph`
helper to construct linear graphs of arbitrary length.

**Acceptance Scenarios**:

1. **Given** a runner advanced to node N, **When** `GoBack()` is called, **Then** the runner re-enters the previous node and context values match the snapshot taken before advancing.
2. **Given** an empty history, **When** `GoBack()` is called, **Then** the call is a no-op — no exception, state unchanged.
3. **Given** a graph where node N is `IsCheckpoint == true`, **When** `GoBackToCheckpoint()` is called from a later node, **Then** the runner restores to node N.
4. **Given** no checkpoint in history, **When** `GoBackToCheckpoint()` is called, **Then** the call is a no-op.
5. **Given** `HistoryDepth == N` (N > 0) and N+1 snapshots taken, **Then** the oldest snapshot is evicted, leaving exactly N entries, and one extra `GoBack()` is a no-op.
6. **Given** `HistoryDepth == 0` and 10 advances, **Then** all 10 `GoBack()` calls succeed without a no-op.

---

### User Story 4 - SubGraph Stack and Context Coverage (Priority: P1)

A developer can trust that `SubGraphNodeData` correctly pushes a new frame onto the execution
stack, context inheritance and isolation are mutually exclusive, and when the sub-graph's
`EndNodeData` is reached, execution correctly pops the frame and resumes the parent graph.

**Why this priority**: SubGraph is the cross-lib composition mechanism defined in Constitution
Principle VI. Incorrect push/pop or context leakage would corrupt multi-graph execution.

**Independent Test**: Can be run as a standalone `SubGraphTests` fixture — builds minimal
parent/child graph pairs using `ScriptableObject.CreateInstance`.

**Acceptance Scenarios**:

1. **Given** a parent graph with a `SubGraphNodeData` pointing to a child graph, **When** the runner enters the sub-graph node and `Proceed` is called, **Then** the child graph's entry node is entered next.
2. **Given** `InheritParentContext == true`, **When** the sub-graph executor writes a value to the context, **Then** the parent context reflects that value after the sub-graph completes.
3. **Given** `InheritParentContext == false`, **When** the sub-graph executor reads the context, **Then** values set in the parent context are not visible.
4. **Given** the child graph reaches its `EndNodeData`, **When** the frame is popped, **Then** the parent graph resumes from the node after the `SubGraphNodeData`.
5. **Given** a valid non-cyclic parent → child execution, **When** the runner completes, **Then** `State == Ended` and `OnEnded` fired exactly once.

---

### User Story 5 - CycleDetection Coverage (Priority: P1)

A developer can trust that cycle detection correctly identifies direct cycles (A → A),
indirect cycles (A → B → C → A), and accepts valid acyclic graphs — and that
`GraphCycleException` is thrown before any node execution begins.

**Why this priority**: Cycle detection is non-negotiable per Constitution Principle VI.
An undetected cycle causes infinite recursion at runtime.

**Independent Test**: Can be run as standalone `CycleDetectionTests` — operates on
`BaseGraph` instances built in-memory with no file I/O.

**Acceptance Scenarios**:

1. **Given** a graph that references itself via a `SubGraphNodeData`, **When** execution reaches that node, **Then** `GraphCycleException` is thrown before any executor runs.
2. **Given** graphs A → B → C where C references A, **When** the runner enters C's sub-graph node, **Then** `GraphCycleException` is thrown identifying A's `GraphId`.
3. **Given** a valid graph with no cycles, **When** the full graph is executed, **Then** no exception is thrown and all nodes are visited.
4. **Given** a `GraphCycleException`, **When** inspecting `CyclicGraphId`, **Then** it matches the `GraphId` of the graph that would have been re-entered.
5. **Given** a direct cycle detected, **When** the exception is thrown, **Then** no `OnNodeEntered` or `OnNodeCompleted` event has fired for the cyclic target node.

---

### Edge Cases

- What if `Start` is called a second time on a running runner? The runner resets all internal state (graph stack, history, state machine) as if freshly constructed.
- What if `GoBack()` is called during a sub-graph execution? The snapshot includes the full graph stack; the restore brings back both the node and the stack frame.
- What if `HistoryDepth` is changed between advances? The cap is read at each `AppendSnapshot` call; a reduction takes effect on the next append.
- What if a `DeepClone` result is passed to another runner's `Start`? It behaves as a fully independent context with no subscriber carry-over.
- What if `GoBackToCheckpoint()` finds multiple checkpoints? The nearest (most recent) checkpoint is restored, not the first.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each test fixture class (`BaseRunnerTests`, `BaseContextTests`, `HistoryTests`, `SubGraphTests`, `CycleDetectionTests`) MUST be in the `Faolline.GraphCore.Tests` namespace, in `Tests/EditMode/Execution/`.
- **FR-002**: Test methods MUST follow the `MethodName_Scenario_ExpectedResult` naming convention.
- **FR-003**: Each test method MUST follow the Arrange/Act/Assert pattern — setup, single action, single assertion (or a tightly related group).
- **FR-004**: No mutable state MUST be shared between tests. Each test class MUST use `[SetUp]` / `[TearDown]` or local variables to construct and destroy all `ScriptableObject` instances.
- **FR-005**: `BaseRunnerTests` MUST cover all `RunnerState` transitions: `Idle → NodeReady`, `NodeReady → NodeReady` (via `Proceed`), and `NodeReady → Ended`.
- **FR-006**: `BaseRunnerTests` MUST cover entry-condition failure (`OnStuck`), `ChooseById` edge selection by both edge ID and port name, and executor dispatch.
- **FR-007**: `BaseContextTests` MUST cover `Set`/`Get` for all four supported types, `TryGet`, `Has`, unsupported-type rejection, `OnParameterChanged` subscription/unsubscription, and `DeepClone`.
- **FR-008**: `HistoryTests` MUST cover snapshot integrity after `GoBack`, checkpoint restoration via `GoBackToCheckpoint`, `HistoryDepth` cap enforcement with eviction, and unlimited history when `depth == 0`.
- **FR-009**: `SubGraphTests` MUST cover stack push/pop, `InheritParentContext == true` (shared values), `InheritParentContext == false` (isolated values), and `OnEnded` propagation after sub-graph completion.
- **FR-010**: `CycleDetectionTests` MUST cover direct cycles, indirect chains (minimum depth 3), a valid acyclic graph, `GraphCycleException.CyclicGraphId` value, and the invariant that no executor runs before the exception is thrown.
- **FR-011**: All `ScriptableObject` instances created in tests MUST be destroyed via `Object.DestroyImmediate` in `[TearDown]` — no ScriptableObject leaks.
- **FR-012**: No test MAY depend on disk assets, Unity Play Mode, or any Editor-only API from the `UnityEditor` namespace — tests MUST run as EditMode tests without scene loading.

### Key Entities

- **BaseRunnerTests**: Fixture covering the `BaseRunner` state machine — transitions, action sequencing, executor dispatch, stuck conditions, and choice selection.
- **BaseContextTests**: Fixture covering typed get/set, event subscriptions, and deep clone semantics of `BaseContext`.
- **HistoryTests**: Fixture covering `GoBack`, `GoBackToCheckpoint`, history cap, and unlimited depth behavior.
- **SubGraphTests**: Fixture covering sub-graph push/pop, context inheritance vs isolation, and end-node propagation to parent.
- **CycleDetectionTests**: Fixture covering runtime cycle detection via `BaseRunner` — direct, indirect, valid, exception payload, and pre-execution guarantee.
- **TestGraphBuilder**: Internal helper (non-test class) that creates `BaseGraph` instances with configurable node chains, checkpoints, and sub-graph references for use across fixtures.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All five test fixtures run green in Unity's Test Runner (EditMode) with zero failures, zero errors, and zero skipped tests.
- **SC-002**: The full suite executes in under 5 seconds on a standard developer machine — no test relies on async operations or `WaitForSeconds`.
- **SC-003**: Each of the five test fixtures can be run in isolation (individual fixture run) and produces the same pass/fail result as when the full suite runs.
- **SC-004**: Zero `ScriptableObject` instances leak between tests — verified by Unity's object leak detector showing no orphaned assets after the suite completes.
- **SC-005**: Every `RunnerState` value, every supported `ParameterType`, and every `EndReason` value has at least one dedicated test that covers it.
- **SC-006**: `CycleDetectionTests` includes at least one test asserting that no executor ran before `GraphCycleException` was thrown — verified by a call counter on a stub executor.

## Assumptions

- Tests run in Unity's EditMode test runner under Unity 6000.x. No PlayMode tests are required; `BaseRunner` is headless.
- `ScriptableObject.CreateInstance<BaseGraph>()` is available in EditMode tests without a running scene.
- The existing `com.faolline.graphcore.Tests.EditMode.asmdef` already references both the Runtime and Editor assemblies; no new assembly definition is needed.
- `TestGraphBuilder` (or equivalent inline helper methods) is internal to the test assembly — it is not exposed as a public API.
- The `DataLayer/` tests (feature 001) and `Editor/` tests (feature 003) are out of scope for this suite; this spec covers `Execution/` only.
- `INodeExecutor.Undo` is part of the executor interface; stub executors in tests that don't test undo may implement it as a no-op.
