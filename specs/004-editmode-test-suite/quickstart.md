# Quickstart: EditMode Test Suite

## Running the Tests

1. Open Unity with the `com.faolline.graphcore` package in the project.
2. Open **Window → General → Test Runner**.
3. Select the **EditMode** tab.
4. Run either the full suite or a specific fixture by expanding
   `com.faolline.graphcore.Tests.EditMode` → `Faolline.GraphCore.Tests`.

All five execution fixtures are in `Tests/EditMode/Execution/`.

---

## Adding a New Test

1. Open the relevant fixture file (e.g., `BaseRunnerTests.cs`).
2. Add a `[Test]` method following the naming convention: `MethodName_Scenario_ExpectedResult`.
3. Arrange a fresh graph and context locally — do not use shared mutable fields.
4. Add any `ScriptableObject` instances to `_soInstances` (or via `Track()`) so they are destroyed in TearDown.
5. Run the new test in isolation first to confirm it fails for the right reason (Red step).
6. Implement the missing behaviour, then confirm it passes (Green step).

---

## Naming Convention

```
MethodName_Scenario_ExpectedResult

Examples:
  Proceed_ReachesEndNode_TransitionsToEnded
  Set_UnsupportedType_ThrowsArgumentException
  GoBack_EmptyHistory_IsNoOp
  SubGraph_InheritContext_False_ParentValuesNotVisible
  Cycle_Indirect_ThrowsGraphCycleException
```

---

## ScriptableObject Lifecycle

```csharp
// In [SetUp] or at construction:
_graph = ScriptableObject.CreateInstance<BaseGraph>();
_soInstances.Add(_graph);   // or: Track(_graph)

// In [TearDown]:
foreach (var so in _soInstances) Object.DestroyImmediate(so);
_soInstances.Clear();
```

Never share a `ScriptableObject` instance between tests. Each `[SetUp]` must create fresh instances.

---

## Writing Stub Executors

```csharp
// Inline inner class — no import needed
private class LambdaExecutor : INodeExecutor
{
    private readonly Action<BaseNodeData, BaseContext> _exec;
    public string NodeType { get; }
    public LambdaExecutor(string type, Action<BaseNodeData, BaseContext> exec)
    { NodeType = type; _exec = exec; }
    public void Execute(BaseNodeData node, BaseContext context) => _exec(node, context);
    public void Undo(BaseNodeData node, BaseContext context) { }
}
```

Declare this inside the fixture class. If two fixtures need the same stub, duplicate it —
do not create a shared file until three or more fixtures share identical stubs.

---

## Fixture Responsibilities (at a glance)

| Fixture | Primary runtime type | Key scenarios |
|---------|---------------------|---------------|
| `BaseRunnerTests` | `BaseRunner` | State transitions, action order, executor dispatch, stuck, choices |
| `BaseContextTests` | `BaseContext` | Typed get/set, events, DeepClone isolation |
| `HistoryTests` | `BaseRunner` history | GoBack, GoBackToCheckpoint, depth cap, unlimited |
| `SubGraphTests` | `BaseRunner` sub-graph | Stack push/pop, context inherit/isolate, OnEnd |
| `CycleDetectionTests` | `BaseRunner` + `GraphCycleException` | Direct/indirect cycle, pre-execution guarantee |
