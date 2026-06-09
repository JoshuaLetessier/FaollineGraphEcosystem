# Quickstart — running a flow across scenes

## The cross-scene pattern (the important one)

One `GraphFlowDriver` runs **one graph that spans scenes**: `… → [LoadScene A, Single] → await → [LoadScene B,
Single] → …`. A single-mode load tears down the current scene — so the driver **must outlive scenes**, or it
is destroyed mid-flow and nothing can advance it.

1. Put the driver on a GameObject and tick **Persist Across Scenes**.
2. Assign your graph; press Play. The driver survives each load and runs the whole flow.
3. Scene scripts in the loaded scenes reach it via the static accessor:

```csharp
var flow = GraphFlowDriver.Active;          // the persistent driver, no singleton of your own
flow.OnWaitingForSignal += (n, sig) => { /* a door is now interactable, etc. */ };
flow.OnWaitingForTime   += (n, secs) => { /* a timed beat started */ };

// recover a wait that fired during the scene load (you subscribed late):
if (flow.IsWaitingForSignal && flow.CurrentAwaitSignal == "door")
    EnableDoor();

void OnDoorOpened() => flow.RaiseSignal("door");
```

> You can also place the driver on your own `DontDestroyOnLoad` bootstrap object (e.g. shared with a save
> system); then it already persists and the flag is optional.

## Bigger games: decompose into SubGraphs

One flat graph is fine for a small flow. As it grows, model a **room / dialogue / ability as a SubGraph node**
(graphcore's `SubGraphNodeData` → a `BaseGraph`) and keep the master flow lean. The substrate already supports
nested graphs with cycle detection.

## Boot control (tests / explicit setup)

Turn off **Boot On Start** to configure the driver and boot it yourself (no auto-boot, no "already running"
warning):

```csharp
driver.BootOnStart = false;     // Start() will not boot
driver.Graph = graph;
driver.SceneLoader = myLoader;  // e.g. a recording loader in a test
driver.Boot();                  // explicit
```

In EditMode tests that need persistence honored, create the GameObject **inactive**, set the flags, then
activate it (so `Awake` reads the flag): `go.SetActive(false); …; go.SetActive(true);`.
