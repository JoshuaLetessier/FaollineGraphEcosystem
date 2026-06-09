# Phase 0 — Research: P5 Time

## R1 — Host-fed `Tick` vs. an internal clock

**Decision**: The runner owns no clock; the host calls `Tick(float deltaSeconds)`. The runner only
subtracts fed time from the held node's remaining duration.

**Rationale**: Pause (don't tick), slow-motion (scaled dt), fast-forward (large dt), and "stop while a menu
is open" all fall out for free with no extra API. A runner-owned clock would couple it to a frame loop and
fight Unity's `Time.timeScale`/pause — exactly what the research flagged (R3 of the engines doc).

**Alternatives considered**: *internal `DateTime`/coroutine clock* — rejected: not headless-testable
deterministically, and re-implements pause/scale the host already controls.

## R2 — `WaitDuration` on `BaseNodeData` vs. a dedicated wait node

**Decision**: An append-only `float WaitDuration` on `BaseNodeData` (default 0 = no wait), exactly mirroring
P1's `AwaitSignalName`.

**Rationale**: Universal node metadata (like checkpoints, await-signal); any node can pause. Append-only,
back-compatible, no new node type/executor/editor surface (YAGNI).

**Alternatives considered**: *a dedicated `WaitNode`* — rejected for the same reasons P1 rejected an
await-node: heavier surface for a property of "this node pauses here".

## R3 — Await-signal precedence + re-arm on re-entry

**Decision**: If a node sets both `AwaitSignalName` and `WaitDuration`, the **await-signal wins** (the
existing P1 branch is checked first; the time branch second). Step-back re-arms the wait (re-entry restarts
the full countdown); a partial countdown is not persisted in the MVP.

**Rationale**: One hold mechanism per node keeps semantics clear; signal-first matches the existing branch
order and is documented. Re-arm-on-re-entry mirrors P1 and needs no history-snapshot change.

**Alternatives considered**: *persist the partial countdown in history/save* — deferred; it adds snapshot
fields for a marginal benefit.

## R4 — `WaitingForTime` state + inert manual advance

**Decision**: Append `RunnerState.WaitingForTime`. `Proceed`/`ChooseById` keep their `!= NodeReady` guard, so
they are inert while time-waiting — only `Tick` advances it. `Tick` is a no-op unless the runner is
`WaitingForTime`.

**Rationale**: Append-only enum value; reuses the same state-guard discipline as P1's `WaitingForSignal`.
