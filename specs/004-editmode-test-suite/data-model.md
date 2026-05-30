# Data Model: EditMode Test Suite

**Branch**: `004-editmode-test-suite` | **Date**: 2026-05-28

This document describes the structural model of the test suite: fixture classes, their
relationships, shared helpers, and the runtime types they exercise.

---

## Fixture Classes

### BaseRunnerTests

**Location**: `Tests/EditMode/Execution/BaseRunnerTests.cs`
**Namespace**: `Faolline.GraphCore.Tests`
**Supersedes**: `BaseRunnerLinearTests.cs`

**Instance fields** (reset in `[SetUp]`):
- `BaseGraph _graph` — linear Start → Statement → End graph
- `BaseContext _ctx`
- `NodeExecutorRegistry _registry`
- `BaseRunner _runner`
- `List<UnityEngine.Object> _soInstances` — all ScriptableObjects to destroy in TearDown

**Test groups** (method prefix):
- `Start_*` — state transitions on Start, OnNodeEntered/OnNodeCompleted events
- `Proceed_*` — advance to next node, full node lifecycle order, reached EndNode
- `EntryCondition_*` — pass/fail, OnStuck, runner stays NodeReady on fail
- `ChooseById_*` — select edge by ID, select edge by port name
- `Execute_*` — registered executor called, missing executor no-throw
- `OnEnded_*` — correct EndReason, no event after Ended

**Inner stubs**:
- `class TrackingAction : BaseAction` — appends label to a log list
- `class ConstantCondition : BaseCondition` — returns a fixed bool
- `class LambdaExecutor : INodeExecutor` — delegates to an `Action<BaseNodeData, BaseContext>`

---

### BaseContextTests

**Location**: `Tests/EditMode/Execution/BaseContextTests.cs`
**Namespace**: `Faolline.GraphCore.Tests`
**Supersedes**: `ExecutionBaseContextTests.cs` (Execution subfolder), `DataLayer/BaseContextTests.cs` (data-layer subfolder — separate scope, kept as-is)

**No shared fields** — each test creates its own `BaseContext` locally.

**Test groups**:
- `Set_Get_*` — round-trip for bool, int, float, string; overwrite; unsupported type throws
- `TryGet_*` — existing key returns true + value; missing key returns false + default
- `Has_*` — existing key true, missing key false
- `Get_*` — missing key throws KeyNotFoundException
- `OnParameterChanged_*` — fires on Set, not on different key, not after Off
- `DeepClone_*` — values copied, values independent after mutation, subscriptions not copied, empty context clones fine

---

### HistoryTests

**Location**: `Tests/EditMode/Execution/HistoryTests.cs`
**Namespace**: `Faolline.GraphCore.Tests`
**Supersedes**: `BaseRunnerHistoryTests.cs`

**Instance fields**:
- `List<BaseGraph> _graphs` — all graphs, destroyed in TearDown
- `BaseGraph Track(BaseGraph g)` — adds to `_graphs`, returns the graph

**Helper**: `BuildChainGraph(int count)` → linear graph `n0 → n1 → … → n(count-1)`, where `n0` is `StartNodeData`, last is `EndNodeData`, all middle are `StatementNodeData`.

**Test groups**:
- `GoBack_*` — restores previous node, restores context values, empty history no-op
- `GoBackToCheckpoint_*` — restores nearest checkpoint, no checkpoint is no-op, multiple checkpoints restores most recent
- `History_CappedByHistoryDepth_*` — depth N evicts oldest after N+1 entries, extra GoBack is no-op
- `History_DepthZero_*` — unlimited: N advances all undoable

**Inner stubs**: `LambdaExecutor` (same pattern as BaseRunnerTests).

---

### SubGraphTests

**Location**: `Tests/EditMode/Execution/SubGraphTests.cs`
**Namespace**: `Faolline.GraphCore.Tests`
**Supersedes**: `BaseRunnerSubGraphTests.cs`

**Instance fields**:
- `List<BaseGraph> _graphs`
- `BaseGraph Track(BaseGraph g)` helper

**Helper**: `BuildLinearGraph(string id, string entryId, string endId)` → two-node graph Start → End.

**Test groups**:
- `SubGraph_Push_*` — entering SubGraphNodeData pushes child frame, child nodes visited in order
- `SubGraph_Pop_*` — child EndNode pops frame, parent resumes after SubGraphNode, OnEnded fires once
- `SubGraph_InheritContext_True_*` — value set in sub-graph visible in parent context after completion
- `SubGraph_InheritContext_False_*` — parent values not visible in sub-graph context
- `SubGraph_NullTarget_*` — null TargetGraph raises OnStuck, no exception
- `SubGraph_Nested_*` — depth > 1 works correctly (grandchild traversal)

**Inner stubs**: `LambdaExecutor`.

---

### CycleDetectionTests

**Location**: `Tests/EditMode/Execution/CycleDetectionTests.cs`
**Namespace**: `Faolline.GraphCore.Tests`
**Supersedes**: Cycle detection tests currently embedded in `BaseRunnerSubGraphTests.cs`

**Instance fields**:
- `List<BaseGraph> _graphs`
- `BaseGraph Track(BaseGraph g)` helper

**Test groups**:
- `Cycle_Direct_*` — self-reference (A → A) throws GraphCycleException
- `Cycle_Indirect_*` — A → B → C → A throws GraphCycleException at C's sub-graph node
- `Cycle_Valid_*` — acyclic graph completes without exception
- `Cycle_ExceptionPayload_*` — `GraphCycleException.CyclicGraphId` matches the re-entered graph
- `Cycle_PreExecution_*` — stub executor call count is 0 for the cyclic target when exception thrown

**Inner stubs**: `CountingExecutor : INodeExecutor` — counts `Execute` calls per node type.

---

## Relationships Between Fixtures and Runtime Types

```
BaseRunnerTests
  └─ exercises: BaseRunner, RunnerState, BaseGraph, BaseContext,
                NodeExecutorRegistry, INodeExecutor, BaseAction, BaseCondition,
                BaseEdgeData, BaseNodeData (Start, Statement, End), EndReason

BaseContextTests
  └─ exercises: BaseContext (Set, Get, TryGet, Has, OnParameterChanged, OffParameterChanged,
                DeepClone), ParameterType (via InitFromGraph if needed)

HistoryTests
  └─ exercises: BaseRunner (GoBack, GoBackToCheckpoint), HistoryEntry,
                BaseGraph.HistoryDepth, BaseContext (value restore)

SubGraphTests
  └─ exercises: BaseRunner (EnterSubGraph, HandleEndNode), SubGraphNodeData,
                GraphExecutionState (stack push/pop), BaseContext (inherit vs isolate)

CycleDetectionTests
  └─ exercises: BaseRunner (EnterSubGraph cycle check), GraphCycleException,
                SubGraphNodeData.TargetGraph, BaseGraph.GraphId
```
