# Research: EditMode Test Suite

**Branch**: `004-editmode-test-suite` | **Date**: 2026-05-28

## Summary

All technical unknowns were resolved directly from the existing codebase (features 001–003).
No external research was required; every decision below was derived from reading production
source files and the existing partial test coverage.

---

## Decision 1: Test Assembly

**Decision**: Reuse `com.faolline.graphcore.Tests.EditMode.asmdef` — no new asmdef.

**Rationale**: The assembly already exists, already references both `com.faolline.graphcore.Runtime`
and `com.faolline.graphcore.Editor`, and already targets `EditMode` platform. Creating a second
assembly for `Execution/` tests would add unnecessary indirection with no benefit.

**Alternatives considered**:
- Separate `com.faolline.graphcore.Tests.Execution.asmdef` — rejected: extra file, no isolation benefit since both test sets target EditMode.

---

## Decision 2: Fixture Naming and Consolidation

**Decision**: Use five single-responsibility fixtures with canonical names (`BaseRunnerTests`,
`BaseContextTests`, `HistoryTests`, `SubGraphTests`, `CycleDetectionTests`) in `Tests/EditMode/Execution/`.
Earlier files (`BaseRunnerLinearTests`, `ExecutionBaseContextTests`, `BaseRunnerHistoryTests`,
`BaseRunnerSubGraphTests`) are superseded — their test cases are absorbed into the new canonical fixtures.

**Rationale**: The earlier files used ad-hoc names (`BaseRunnerLinearTests` implies only linear
traversal; `ExecutionBaseContextTests` has redundant prefix). The spec defines authoritative fixture
names that match FR-001. Consolidation avoids test case duplication and makes gap analysis straightforward.

**Alternatives considered**:
- Keep old files alongside new ones — rejected: test duplication, confusing fixture boundaries.
- Rename old files in-place — rejected: the test method naming convention also needs updating
  to `MethodName_Scenario_ExpectedResult`, making a clean rewrite preferable.

---

## Decision 3: ScriptableObject Management Pattern

**Decision**: Each fixture declares a `List<UnityEngine.Object> _soInstances` field, populated
in `[SetUp]` (or via a `Track()` helper), and fully destroyed in `[TearDown]` with `Object.DestroyImmediate`.

**Rationale**: `BaseGraph` is a `ScriptableObject`. Failing to destroy it leaks unmanaged Unity
objects between tests. The `Track()` helper pattern (already used in `BaseRunnerSubGraphTests`)
is the simplest correct approach: add on creation, destroy all in teardown.

**Alternatives considered**:
- `[OneTimeTearDown]` for shared graphs — rejected: violates FR-004 (no shared mutable state).
- Not destroying at all — rejected: Unity test runner reports leaked objects as warnings.

---

## Decision 4: Stub Executor / Action / Condition Pattern

**Decision**: Use private inner classes (`LambdaExecutor`, `TrackingAction`, `ConstantCondition`)
inside each fixture that needs them. No shared stub file.

**Rationale**: Inner classes keep each fixture self-contained and eliminate cross-file dependencies.
The `LambdaExecutor` pattern (accepting an `Action<BaseNodeData, BaseContext>`) is the minimum
surface needed to test executor dispatch. Duplicating a 5-line class across two files is cheaper
than a shared helper file that creates a coupling point.

**Alternatives considered**:
- Shared `TestStubs.cs` in `Tests/EditMode/` — rejected: YAGNI; changes to stubs would affect
  all fixtures. Inner classes are simpler and scoped.

---

## Decision 5: Graph Construction Helpers

**Decision**: Each fixture defines a `BuildChainGraph(int count)` or `BuildLinearGraph(id, entryId, endId)`
helper method locally (not as a shared utility class). `SubGraphTests` and `CycleDetectionTests`
build graphs inline.

**Rationale**: A shared `TestGraphBuilder` class was considered but rejected per YAGNI. The helpers
are 15–20 lines each; inlining them avoids a shared-state risk and keeps each fixture readable
without navigation to another file. If three or more fixtures need identical construction code,
extraction should be revisited.

**Alternatives considered**:
- `TestGraphBuilder` static class — rejected at this scale; revisit if future fixture count grows.

---

## Decision 6: Cycle Detection Scope

**Decision**: `CycleDetectionTests` covers runtime cycle detection via `BaseRunner`
(`GraphCycleException` thrown at `EnterSubGraph`). The editor-time `CycleDetector.Check`
(used in `BaseGraphView`) is tested separately in `Tests/EditMode/Editor/CycleDetectorTests.cs`
(feature 003 scope).

**Rationale**: The two mechanisms are distinct: runtime detection checks the execution stack
(is this graph already running?); editor detection checks the asset graph (would this edge create
a cycle?). Mixing them in one fixture blurs the responsibility boundary.

**Alternatives considered**:
- Single `CycleDetectionTests` covering both — rejected: the editor `CycleDetector` class is
  in the Editor assembly, not the Runtime assembly; separating by assembly boundary is cleaner.
