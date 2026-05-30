# Quickstart: GraphCore Execution Runtime

## Prerequisites

- `com.faolline.graphcore` package with the data layer (`001-data-layer`) installed
- Unity 6000.x

---

## 1. Create or load a graph

**From a saved asset** (typical in production):

```csharp
BaseGraph graph = Resources.Load<BaseGraph>("MyGraph");
```

**Procedurally at runtime** (tooling, tests, generated content):

```csharp
// ScriptableObject.CreateInstance is required — never use `new BaseGraph()`
BaseGraph graph = ScriptableObject.CreateInstance<BaseGraph>();
graph.EntryNodeId = "start";

var start = new StartNodeData   { Id = "start", NodeType = StartNodeData.NodeTypeId };
var end   = new EndNodeData     { Id = "end",   NodeType = EndNodeData.NodeTypeId,
                                  EndReason = EndReason.Completed };
graph.AddNode(start);
graph.AddNode(end);
graph.AddEdge(new BaseEdgeData  { Id = "e0", FromNodeId = "start", ToNodeId = "end" });

// Destroy when done to avoid memory leaks in editor tests
Object.DestroyImmediate(graph);
```

---

## 2. Initialize a context from a graph

```csharp
// Create a context and populate it from the graph's parameters
var context = new BaseContext();
context.InitFromGraph(graph);

// Read a parameter (set in the graph asset as ParameterData)
bool isComplete = context.Get<bool>("IsComplete");
int  score      = context.Get<int>("Score");
```

---

## 3. Subscribe to parameter changes

```csharp
void OnScoreChanged(object newValue)
{
    Debug.Log($"Score changed to: {(int)newValue}");
}

context.OnParameterChanged("Score", OnScoreChanged);

context.Set<int>("Score", 100);   // → fires OnScoreChanged(100)

// Unsubscribe when done
context.OffParameterChanged("Score", OnScoreChanged);
```

---

## 4. Register node executors

```csharp
// Implement an executor for a built-in node type
public class StatementExecutor : INodeExecutor
{
    public string NodeType => StatementNodeData.NodeTypeId;  // "graphcore/statement"

    public void Execute(BaseNodeData node, BaseContext context)
    {
        // Display the node's content (payload) — lib-specific
        Debug.Log($"Executing statement: {node.SerializedPayload}");
    }

    // Undo has a default no-op — no override needed
}

// Build the registry
var registry = new NodeExecutorRegistry();
registry.Register(new StatementExecutor());

// Downstream lib registers its own executor without touching graphcore
registry.Register(new MyLib.DialogueLineExecutor());
```

---

## 5. Start and drive a linear graph

```csharp
var runner = new BaseRunner();

// Subscribe to events
runner.OnNodeCompleted += node =>
{
    Debug.Log($"Node completed: {node.NodeType}");
    runner.Proceed();   // auto-advance for linear graphs
};

runner.OnEnded += reason =>
{
    Debug.Log($"Graph ended: {reason}");
};

// Start execution
runner.Start(graph, context, registry);
// → fires OnNodeEntered, executor.Execute(), OnNodeCompleted for the entry node
// → caller's OnNodeCompleted handler calls Proceed() to advance
```

---

## 6. Handle choices (ChoiceNodeData)

```csharp
runner.OnNodeCompleted += node =>
{
    if (node is ChoiceNodeData choiceNode)
    {
        // Present choices to the player, then select one
        BaseChoice chosen = choiceNode.Choices[playerSelection];
        runner.ChooseById(chosen.Id);
    }
    else
    {
        runner.Proceed();
    }
};
```

---

## 7. Use history for undo/backtrack

```csharp
// Go back one step
runner.GoBack();

// Jump back to the nearest checkpoint
runner.GoBackToCheckpoint();
// Works across SubGraph boundaries — the full graph stack is restored
```

---

## 8. Handle SubGraph nesting

SubGraph navigation is automatic — `BaseRunner` pushes/pops the graph stack when it
encounters `SubGraphNodeData`. The `InheritParentContext` flag controls context sharing:

```csharp
// SubGraphNodeData.InheritParentContext = true  → sub-graph shares parent context
// SubGraphNodeData.InheritParentContext = false → sub-graph gets its own fresh context

// Cycle detection is automatic:
try
{
    runner.Start(cyclicGraph, context, registry);
}
catch (GraphCycleException ex)
{
    Debug.LogError($"Cycle detected involving graph: {ex.CyclicGraphId}");
}
```

---

## 9. Extend BaseContext in a downstream lib

```csharp
// In your lib's Runtime assembly
public class DialogueContext : BaseContext
{
    public string CurrentSpeaker { get; set; }
    public int    DialogueFlags  { get; set; }

    public override BaseContext DeepClone()
    {
        var clone = (DialogueContext)base.DeepClone();  // copies base parameters
        clone.CurrentSpeaker = CurrentSpeaker;
        clone.DialogueFlags  = DialogueFlags;
        return clone;
    }
}

// Use it like any BaseContext — BaseRunner accepts it transparently
var ctx = new DialogueContext();
ctx.InitFromGraph(graph);
runner.Start(graph, ctx, registry);
```

---

## What's next

| Feature | Command |
|---------|---------|
| Generate implementation tasks | `/speckit-tasks` |
| Graph editor UI | Future feature |
| Dialogue system lib | Future feature (uses execution runtime) |
