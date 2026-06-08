---
description: "Task list for 014-signals (P1 — host→runtime signal injection)"
---

# Tasks: P1 — Signals (host→runtime event injection)

**Input**: Design documents from `specs/014-signals/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED — the constitution mandates TDD (Red-Green-Refactor); every behaviour task is preceded
by a failing EditMode test. EditMode only (headless). Run via Unity 6000.3 batchmode (editor CLOSED;
delete `Temp/UnityLockfile` if stale) or Coplay `run_tests`.

**Organization**: by user story. US1 (context signal channel) is the foundational MVP slice; US2
(await/resume) builds on it; US3 (payload read) builds on US1's last-signal store.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different file, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3 (omitted for Setup / Foundational / Back-compat / History / Polish)

## Path Conventions

graphcore lib root: `com.faolline.graphcore/`. Sandbox: `com.faolline.graphTest/`. All paths below are
repository-relative.

---

## Phase 1: Setup

**Purpose**: folders for the new files

- [X] T001 Create folders `com.faolline.graphcore/Runtime/Signals/` and `com.faolline.graphcore/Tests/EditMode/Signals/` (confirm `com.faolline.graphcore/Tests/EditMode/Execution/` already exists).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the shared payload value type used by US1 (delivery) and US3 (read)

**⚠️ CRITICAL**: blocks US1 and US3.

- [X] T002 Create the `SignalArgs` readonly struct in `com.faolline.graphcore/Runtime/Signals/SignalArgs.cs`: `Name`, `HasPayload`, `PayloadBoxed`, `GetPayload<T>()` (throws `InvalidOperationException` when `!HasPayload`, `InvalidCastException` on type mismatch), internal constructor, XML docs, `[GraphCore]` namespace. No behaviour beyond accessors (its branches are asserted by US1/US3 tests).

**Checkpoint**: payload type available — US1 and US3 can begin.

---

## Phase 3: User Story 1 — Host raises a named signal into a running graph (Priority: P1) 🎯 MVP

**Goal**: the host can raise a named, optionally-payloaded transient signal; N subscribers are notified;
no subscriber = no-op.

**Independent Test**: subscribe to `itemCollected`; raise it with `"key"`; assert the handler fired once
with `HasPayload==true` and `GetPayload<string>()=="key"`; raise an unsubscribed name → no effect, no throw.

### Tests for User Story 1 (write FIRST, confirm FAIL) ⚠️

- [X] T003 [P] [US1] Write `SignalChannelTests` in `com.faolline.graphcore/Tests/EditMode/Signals/SignalChannelTests.cs` covering: single-subscriber delivery with payload (INV-C1, INV-SA2); broadcast to N subscribers (FR-002); zero-subscriber raise is a no-op and updates last-signal, never throws (INV-C2, SC-005); no-payload raise yields `HasPayload==false`/`PayloadBoxed==null` (INV-SA2, INV-C5); re-entrant raise / subscribe-unsubscribe during delivery iterates a snapshot (INV-C3); null/empty name on `RaiseSignal`/`OnSignal`/`OffSignal` logs `[GraphCore]` and no-ops (INV-C4); `RaiseSignal<T>` with an unsupported `T` throws `ArgumentException` (INV-C3 parity with `Set<T>`). Confirm RED.

### Implementation for User Story 1

- [X] T004 [US1] Implement the signal channel in `com.faolline.graphcore/Runtime/Graph/BaseContext.cs`: private `_signalSubs` (name→`List<Action<SignalArgs>>`) and `_lastSignals` (name→`SignalArgs`); `RaiseSignal(string)`, `RaiseSignal<T>(string,T)` (validate `T ∈ {bool,int,float,string}` like `Set<T>`; build `SignalArgs`; store into `_lastSignals`; deliver over a snapshot copy), `OnSignal`/`OffSignal` (null/empty-name guarded with `[GraphCore]` warnings). Do NOT touch `_params`, `DeepClone`, `GetAllParameters`, `CopyValuesFrom`. XML docs on all new public members. Confirm T003 GREEN.

**Checkpoint**: US1 fully functional and independently testable (pub/sub + payload, no runner needed).

---

## Phase 4: User Story 2 — A graph pauses on a node awaiting a signal and resumes when it arrives (Priority: P1)

**Goal**: a node flagged `AwaitSignalName` holds execution on entry; a matching `BaseRunner.RaiseSignal`
resumes it via normal edge selection.

**Independent Test**: `start → [await "doorOpened"] → end`; run → `State==WaitingForSignal`, not ended;
`runner.RaiseSignal("doorOpened")` → advances and ends; a non-matching name leaves it waiting.

**Depends on**: US1 (the runner's `RaiseSignal` delegates delivery to `BaseContext.RaiseSignal`).

### Tests for User Story 2 (write FIRST, confirm FAIL) ⚠️

- [X] T005 [P] [US2] Write `AwaitSignalRunnerTests` in `com.faolline.graphcore/Tests/EditMode/Execution/AwaitSignalRunnerTests.cs` covering: entering an awaiting node sets `State==WaitingForSignal`, fires `OnWaitingForSignal(node,name)`, and does NOT fire `OnNodeCompleted` (INV-B1); matching `RaiseSignal` advances and ends (INV-B2); resume honours conditional edge selection identical to `Proceed` (INV-B1/FR-005); non-matching name delivers to subscribers but keeps waiting (INV-B3); `Proceed`/`ChooseById` are inert while waiting (INV-B4). Confirm RED.

### Implementation for User Story 2

- [X] T006 [P] [US2] Add the append-only `string AwaitSignalName` field + property to `com.faolline.graphcore/Runtime/Nodes/BaseNodeData.cs` (`[SerializeField] private string _awaitSignal = string.Empty;`, setter coerces null→`""`, XML doc). Default `""` ⇒ not awaiting (INV-N1/N2).
- [X] T007 [P] [US2] Append `WaitingForSignal = 4` to `com.faolline.graphcore/Runtime/Execution/RunnerState.cs` with XML doc noting `Proceed`/`ChooseById` are no-ops in this state (INV-R1/S2).
- [X] T008 [US2] Wire await/resume in `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs` (depends on T006, T007): in `EnterCurrentNode`, after `OnNodeEntered`, branch on non-empty `AwaitSignalName` → `_state=WaitingForSignal`, fire new `OnWaitingForSignal` event, `return` (skip `OnNodeCompleted`); on the normal path set `_state=NodeReady` immediately before `OnNodeCompleted` (idempotent — R3). Add `event Action<BaseNodeData,string> OnWaitingForSignal` and `RaiseSignal(string)` / `RaiseSignal<T>(string,T)` (null/empty-name `[GraphCore]` warn; delegate delivery to `_context.RaiseSignal`; then if `State==WaitingForSignal` and `CurrentNode.AwaitSignalName==name` call `ExitAndAdvance()`). XML docs. Confirm T005 GREEN.

**Checkpoint**: US1 + US2 = the MVP (external event drives graph progression).

---

## Phase 5: User Story 3 — Graph logic reads a signal's payload (Priority: P2)

**Goal**: conditions/actions read the last payload delivered for a named signal.

**Independent Test**: raise `itemCollected` with `"key"`; a condition reading the last payload sees `"key"`
and selects the matching branch; a no-payload raise is detectable.

**Depends on**: US1 (`_lastSignals` store).

### Tests for User Story 3 (write FIRST, confirm FAIL) ⚠️

- [X] T009 [P] [US3] Write `SignalPayloadReadTests` in `com.faolline.graphcore/Tests/EditMode/Signals/SignalPayloadReadTests.cs`: `TryGetLastSignal` returns the most recent `SignalArgs` (INV-C5); `false`+default when none seen; `HasPayload` distinguishes scalar vs none (INV-SA2); `GetPayload<T>()` typed read and mismatch throw (INV-SA3). Confirm RED.

### Implementation for User Story 3

- [X] T010 [US3] Add `bool TryGetLastSignal(string name, out SignalArgs args)` to `com.faolline.graphcore/Runtime/Graph/BaseContext.cs` (null/empty-name guarded; reads `_lastSignals`), XML doc. Confirm T009 GREEN.

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Back-compat lock (the non-breakage gate)

**Purpose**: prove the foundation is unbroken (SC-002, SC-003, FR-008).

- [X] T011 [P] Write `SignalBackCompatTests` in `com.faolline.graphcore/Tests/EditMode/Execution/SignalBackCompatTests.cs`: a graph with all `AwaitSignalName` empty and no `RaiseSignal` calls behaves identically to 0.3.0 (INV-B5); signals never appear in `GetAllParameters()`, `DeepClone()`, or `CopyValuesFrom()` output (INV-C6); a deep-cloned context carries parameters but no signal subscriptions/last-signals.
- [X] T012 Run the ENTIRE pre-existing graphcore EditMode suite UNMODIFIED via batchmode (editor closed) and confirm 100% green — this is the constitution's non-breakage gate (SC-002). Record pass count.

---

## Phase 7: History / step-back

**Purpose**: FR-012 — returning to an awaiting node re-arms its wait.

- [X] T013 [P] Write `SignalHistoryTests` in `com.faolline.graphcore/Tests/EditMode/Execution/SignalHistoryTests.cs`: after resuming past an awaiting node, `GoBack` restores to it and `State==WaitingForSignal` again (re-armed, INV-B6); `GoBackToCheckpoint` across an await boundary; assert no signal data leaked into the snapshot.
- [X] T014 Make T013 GREEN. Expectation: no code change needed (re-entry re-arms via `EnterCurrentNode`); if a gap surfaces, fix WITHOUT adding signal data to `HistoryEntry`/`GraphExecutionState` snapshots (R5). Document the outcome in a one-line note.

---

## Phase 8: graphTest exercising (FR-013)

**Purpose**: stress the full surface in the sandbox, end-to-end.

- [X] T015 [P] In `com.faolline.graphTest/`, add a demo scenario: a graph whose node sets `AwaitSignalName`, an EditMode test that runs it, asserts it holds, then `runner.RaiseSignal(...)` resumes to End (mirrors quickstart §1–2).
- [X] T016 [P] In `com.faolline.graphTest/`, add a payload-reading condition (reads `TryGetLastSignal`) and an EditMode test exercising broadcast to multiple subscribers + a payload-driven branch (mirrors quickstart §3).

---

## Phase 9: Polish & Finalize

- [X] T017 Bump `com.faolline.graphcore/package.json` version `0.3.0` → `0.4.0` (semver MINOR).
- [X] T018 [P] Verify XML docs on ALL new public API (`SignalArgs`, `BaseContext.RaiseSignal/OnSignal/OffSignal/TryGetLastSignal`, `BaseNodeData.AwaitSignalName`, `RunnerState.WaitingForSignal`, `BaseRunner.RaiseSignal/OnWaitingForSignal`) and that all misuse warnings carry the `[GraphCore]` prefix.
- [X] T019 Full batchmode EditMode run of graphcore + graphTest (editor closed), all green; record totals. Re-confirm SC-002/SC-004.
- [X] T020 [P] Validate `quickstart.md` snippets compile and behave as documented; fix drift if any.

---

## Dependencies & Execution Order

- **Setup (T001)** → no deps.
- **Foundational (T002 SignalArgs)** → after T001; BLOCKS US1 and US3.
- **US1 (T003→T004)** → after T002. MVP slice.
- **US2 (T005→T006/T007→T008)** → after US1 (runner `RaiseSignal` delegates to context delivery). T006 and T007 are independent of each other; T008 depends on both + US1.
- **US3 (T009→T010)** → after US1 (`_lastSignals`).
- **Back-compat (T011, T012)** → after US1+US2+US3 (full surface present).
- **History (T013→T014)** → after US2.
- **graphTest (T015, T016)** → after US2 (T015) and US3 (T016).
- **Polish (T017–T020)** → last; T019 after everything.

## Within each story

- The test task is written and confirmed FAILING before its implementation task.
- T006 (node field) and T007 (enum) before T008 (runner wiring).

## Parallel Opportunities

- T006 [P] and T007 [P] (different files) can run together, before T008.
- The test-authoring tasks T003 / T005 / T009 / T011 / T013 are in different files ([P]) but each must precede its own implementation.
- T015 [P] and T016 [P] (graphTest) can run together once US2/US3 are done.
- T018 [P] and T020 [P] are independent polish tasks.

## Implementation Strategy

### MVP (US1 + US2)

1. T001 → T002 (Setup + SignalArgs).
2. US1: T003 (RED) → T004 (GREEN) — pub/sub + payload delivery.
3. US2: T005 (RED) → T006/T007 → T008 (GREEN) — await/resume.
4. Back-compat gate T012 (existing suite green). **STOP & VALIDATE** — this is the demonstrable MVP: an external signal drives a graph.

### Incremental

5. US3 (T009→T010) — payload-aware branching.
6. History (T013→T014) — step-back re-arm.
7. graphTest (T015/T016) — sandbox proof (FR-013).
8. Finalize (T017–T020) — version bump, docs, full green.

## Notes

- Commit after each GREEN task or logical group.
- The non-breakage gate (T012) is non-negotiable: the entire pre-existing suite must pass UNMODIFIED.
- No `MonoBehaviour`/`UnityEvent` in Runtime; one class per file; `[GraphCore]` prefix; XML docs on new public API.
