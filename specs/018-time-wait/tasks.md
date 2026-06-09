---
description: "Task list for 018-time-wait (P5 — host-fed time wait in BaseRunner)"
---

# Tasks: P5 — Time (host-fed wait / timeout)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD). EditMode only, batchmode (no `-quit`; re-run after source change; verify XML).
Branch `018-time-wait` (P1–P4 included). Mirrors P1 await-signal.

## Phase 1: US1 + US2 — Time wait (Priority: P1) 🎯 MVP

- [X] T001 [P] [US1] Write `TimeWaitRunnerTests` in `com.faolline.graphcore/Tests/EditMode/Execution/TimeWaitRunnerTests.cs`: entering a `WaitDuration=2` node ⇒ `State==WaitingForTime`, `OnWaitingForTime(node,2)` fired, no `OnNodeCompleted` (INV-1); `Tick(1)` keeps waiting, another `Tick(1)` advances (INV-2); a single `Tick(5)` overshoot advances (INV-3); `Tick(0)`/negative never advances (INV-4); `Tick` while NodeReady is a no-op (INV-5); a node with both `WaitDuration` and `AwaitSignalName` waits on the SIGNAL (INV-6); `Proceed`/`ChooseById` inert while time-waiting (INV-7); `WaitDuration=0` ⇒ no hold, identical flow (INV-9); step-back into a timed node re-arms the countdown (INV-8). Confirm RED.
- [X] T002 [US1] Add `float WaitDuration { get; set; }` (+ `[SerializeField] private float _waitDuration;`, default 0) to `com.faolline.graphcore/Runtime/Nodes/BaseNodeData.cs`, XML doc.
- [X] T003 [US1] Append `WaitingForTime = 5` to `com.faolline.graphcore/Runtime/Execution/RunnerState.cs` (XML doc: Tick-only advance; Proceed/Choose inert).
- [X] T004 [US1] Wire the time wait in `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs` (depends on T002,T003): in `EnterCurrentNode`, AFTER the await-signal branch and BEFORE the `NodeReady`/`OnNodeCompleted` lines, add `if (node.WaitDuration > 0f) { _state = WaitingForTime; _waitRemaining = node.WaitDuration; OnWaitingForTime?.Invoke(node, node.WaitDuration); return; }`. Add `private float _waitRemaining;`, `event Action<BaseNodeData,float> OnWaitingForTime`, and `public void Tick(float deltaSeconds)` (no-op unless WaitingForTime; ignore dt<=0; decrement; `ExitAndAdvance()` when <=0). XML docs. Confirm T001 GREEN.

## Phase 2: Back-compat + Finalize

- [X] T005 Run the ENTIRE existing 612-test suite UNCHANGED via batchmode; confirm green (no wait ⇒ 0.5.0, SC-002).
- [X] T006 Bump `com.faolline.graphcore/package.json` `0.5.0` → `0.6.0`.
- [X] T007 [P] Verify XML docs on the new public API; `[GraphCore]` prefix; validate quickstart; full batchmode green (612 + new).

## Dependencies

- T001 (RED) → T002/T003 (independent) → T004 (GREEN). T005-T007 last.

## Notes

- Only `BaseNodeData.cs`, `RunnerState.cs`, `BaseRunner.cs`, `package.json` change; one new test file.
- The new field/state/method/event are append-only; no-wait path unchanged (the non-breakage gate).
