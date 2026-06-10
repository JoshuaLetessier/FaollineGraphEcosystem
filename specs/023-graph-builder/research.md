# Phase 0 — Research: code-first graph ergonomics

## R1 — Builder shape: `GraphBuilder<TGraph>` + `GraphNodeBuilder` handles

**Decision**: A generic `GraphBuilder<TGraph> where TGraph : BaseGraph, new()`. `AddStart/AddStatement/
AddChoice/AddSubGraph/AddEnd` each return a `GraphNodeBuilder` (a fluent handle wrapping the created
`BaseNodeData`). The handle exposes `Title/At/OnEnter/OnExit/When/Await/Wait/Checkpoint/Choice/AsEntry/To`
(each returns the handle for chaining). Edges via `builder.Edge(from, to[, portName])` or `from.To(to)`. Ids
are auto-GUID; position auto-assigns by add-order column when not set. `Build()` adds everything to a fresh
`TGraph`, sets `EntryNodeId` (from `AsEntry`, else the first Start), and returns it.

**Rationale**: Node-handle builders are the readable, well-known pattern (you name a node once and wire it),
and the generic over `TGraph` produces the exact asset type a consumer needs (e.g. `GameFlowGraph`) while only
ever touching graphcore's universal node/edge types — no domain vocabulary, graphcore untouched.

**Alternatives**: *a single chained string-id API* — rejected: stringly-typed wiring is error-prone. *a
gameflow-specific builder* — deferred (out of scope); the universal builder + `LoadSceneAction` already cover
the consumer's need.

## R2 — Choice edges keyed by choice title

**Decision**: `node.Choice(title, condition = null)` records a `BaseChoice` (auto-GUID id, `Title = title`).
`Edge(choiceNode, target, choiceTitle)` resolves the edge's `PortName` to that choice's **id** by matching its
title; for a non-choice node `portName` defaults to `"out"`. An unknown choice title logs `[GraphStandard]`
and falls back to the literal string.

**Rationale**: Choice routing in graphcore is by edge `PortName == choice.Id`; the builder lets authors wire
by the human title they just gave, hiding the GUID. Mirrors how the editor presents choices (title on the
port, id as the routing key).

## R3 — Driver time-remaining computed driver-side (no graphcore change)

**Decision**: `GraphFlowDriver` adds `IsWaitingForTime` (= running && `Runner.State == WaitingForTime`),
`WaitTotal`, and `WaitRemaining`. On `OnWaitingForTime(node, duration)` the driver records `total = duration`,
`elapsed = 0`; in `Tick(dt)` it adds `dt` to `elapsed` while time-waiting; `WaitRemaining = max(0, total −
elapsed)` and `WaitTotal` are reported only while `IsWaitingForTime` (else 0).

**Rationale**: graphcore exposes the time-wait *state* and the *duration* (via the event) but not the private
remaining counter; the driver already pumps the same `Tick`, so it reconstructs the remaining time exactly
without any graphcore API. Symmetric with the slice-3 `IsWaitingForSignal`/`CurrentAwaitSignal`. Clamped at
zero; guarded by `IsWaitingForTime` so it self-resets when the wait resolves or another node is entered.

**Alternatives**: *add `BaseRunner.WaitRemaining` to graphcore* — rejected: touches graphcore for something
the host can compute; keep graphcore untouched.

## R4 — Cyclic no-End shell is already supported → documentation only

**Decision**: No runtime change. Document (gameflow README + builder docs) that a cyclic Linear graph with no
End node is a supported game-shell pattern: the runner follows the single outgoing edge on each advance, the
flow never ends (`IsRunning` stays true, no `OnEnded`), history is bounded by `BaseGraph.HistoryDepth`, and a
small depth is appropriate for a forever-looping shell (`GoBack` across the loop is not meaningful).

**Rationale**: The Linear runner already traverses cycles; the only gap the consumer hit was *doubt* about
whether it was intended. A note removes the doubt at zero code cost. A `Restart()`/goto affordance is
unnecessary (the loop edge is the idiom) and deferred.

## R5 — Persist util: sub-assets for in-memory actions only

**Decision**: `GraphAssetBuilder.Save(BaseGraph, path)` (graphstandard Editor): `CreateAsset(graph, path)`,
then for every node's `OnEnterActions`/`OnExitActions`/`EntryConditions` (and each choice's `Condition`),
`AddObjectToAsset` each object **that is not already a persisted asset** (`AssetDatabase.Contains` check), then
`SaveAssets`. Returns the saved graph.

**Rationale**: Mirrors the internal sample but as a documented public util; the "already an asset" guard
avoids double-adding a shared/condition asset and matches what a hand-roller would (eventually) get right.
