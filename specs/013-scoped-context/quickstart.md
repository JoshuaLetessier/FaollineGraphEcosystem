# Quickstart: Global & Local Execution Contexts

Audience: graph authors and downstream-lib developers (gameflow first). This shows how to make a sub-graph
keep its temporaries local while still reading and durably updating global state — with no nesting.

## 1. Declare your globals on the host graph

Put the values that must survive the whole run as **parameters on the host/root graph** (player gold,
story flags). They seed the **global context** at start, so they live in the global bucket.

```
HostGraph.Parameters: Gold=7 (Int), BossDefeated=false (Bool)
```

## 2. Mark a sub-graph node "opens a scope"

On the `SubGraphNodeData` that invokes your self-contained sub-flow (a "scene"), set the new flag:

```csharp
var sceneNode = new SubGraphNodeData
{
    TargetGraph = sceneGraph,
    OpensScope  = true,            // ← third behaviour: open a local context
};
```

`OpensScope = true` takes precedence over `InheritParentContext`: the scene runs on the parent context
with a fresh **local context** layered on top. Declare the scene's scratch variables as parameters on
`sceneGraph` — they seed the local bucket and are discarded when the scene ends.

```
sceneGraph.Parameters: RoomPuzzleStep=0 (Int)      // local scratch, vanishes on scene end
```

## 3. Author actions normally — routing is automatic

Generic actions only ever call `ctx.Set(key, value)` / `ctx.Get(key)`. The context routes by where the
key lives — you do **not** annotate actions:

- `Set("RoomPuzzleStep", 2)` inside the scene → key lives in local → stays local → gone on scene end.
- `Set("BossDefeated", true)` inside the scene → key lives in global → **persists** after the scene ends.
- `Get("Gold")` inside the scene → not local → falls through to global → returns `7`.
- `Set("AdHocTemp", 5)` (never declared) inside the scene → defaults to local → discarded on scene end.

## 4. The runner does the rest

With `OpensScope = true`, `BaseRunner` calls `BeginLocalContext(sceneGraph)` when it enters the scene and
`EndLocalContext()` when the scene reaches its End node — in lockstep with the graph stack. Sequential
scenes each get a fresh local context. No code in your host or scene needs to manage scopes.

## 5. Manual use (tests / advanced)

You can drive the overlay directly on any `BaseContext`:

```csharp
var ctx = new BaseContext();
ctx.Set("Gold", 7);                  // global
ctx.BeginLocalContext();             // open local overlay
ctx.Set("Step", 1);                  // local (undeclared → local)
ctx.Set("Gold", 99);                 // resolves in global → durable global write
Assert.AreEqual(99, ctx.Get<int>("Gold"));
Assert.IsTrue(ctx.Has("Step"));
ctx.EndLocalContext();               // discard local
Assert.IsFalse(ctx.Has("Step"));     // local scratch gone
Assert.AreEqual(99, ctx.Get<int>("Gold"));   // global write persisted
```

## 6. How gameflow maps onto this

GameFlow's existing Global/Scene partition becomes: **Global = the global context**, **Scene = the local
context**. A `SceneFlowNodeData` is just a `SubGraphNodeData` with `OpensScope = true`; "scene" is a
*usage* of the universal local context, not a graphcore type. The previous prefix/partition machinery in
`GameFlowContext` can be retired in favour of this core capability ([[graphgameflow]] SceneFlow rework).

## 7. Verify (headless)

EditMode only. Run via Unity 6000.3 batchmode (editor closed; delete a stale `Temp/UnityLockfile` first) or
Coplay `run_tests`. The four new test files cover overlay routing, runner lockstep, back-compat, and
step-back; the **entire pre-existing graphcore suite must stay green unmodified**.
