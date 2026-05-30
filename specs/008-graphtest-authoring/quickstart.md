# Quickstart: GraphTest — Editor Authoring Gaps

**Feature**: `008-graphtest-authoring` | **Date**: 2026-05-30

Manual validation walkthroughs in the TestGraph editor, one per user story. Each maps to the
spec's Independent Test and Success Criteria.

## US1 — End Reason (MVP)

1. Open a TestGraph, add a Start node and an End node, connect Start → End.
2. Select the End node → in the inspector, set **End Reason** to `Cancelled`.
3. **Save**, close and reopen the window → the End node's reason is still `Cancelled` (SC-002).
4. **Run** → console shows `[GraphTest] Graph ended: Cancelled` (SC-001).
5. Repeat with `Error` and `Completed`.

## US2 — SubGraph node

1. Create a **child** TestGraph: Start → Statement("child") → End. Save it as an asset.
2. In a **parent** TestGraph: Start → (Add SubGraph Node) → End. Connect them.
3. Select the SubGraph node → assign **Target Graph** = the child asset; leave **Inherit Parent Context** as desired.
4. **Save**, reload → target graph, toggle, and edges persist (SC-004).
5. **Run** → console shows the parent Start, then the child's nodes (descent), then the parent completes (SC-003).
6. Try assigning the **parent** graph as the SubGraph's target → assignment is refused with `[GraphTest] Cycle refused` and the field reverts (SC-005).
7. Clear the Target Graph and Run → execution halts with a stuck message, no exception (FR-011).

## US3 — Typed parameters

1. In the parameter panel, add an **Int** parameter `score` with default `0` (pick type via the dropdown).
2. Save/reload → `score` keeps type Int and default `0` (SC-007).
3. Build: Start → Statement with **OnEnter action** `TestSetIntAction(score = 5)` → Choice with two choices:
   - "High" gated by `TestIntCondition(score, Greater­OrEqual, 3)`
   - "Always" with no condition
   → branches → End.
4. **Run** → at the choice, **both** are offered (score = 5 ≥ 3) (SC-006).
5. Change the action to `score = 1`, re-run → "High" is filtered out of the Choose list.
6. Repeat the pattern with a **Float** parameter and `TestFloatCondition`, and a **String** parameter with `TestStringCondition` (equality).

## Regression gate

After each user story, run the full EditMode suite (Unity Test Runner) — all prior + new tests green (SC-008).
