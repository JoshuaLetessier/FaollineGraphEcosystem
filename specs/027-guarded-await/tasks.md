---
description: "Task list for 027-guarded-await (graphcore re-armable resume-condition gate on await + GraphBuilder ResumeWhen)"
---

# Tasks: guarded await — re-armable signal resume conditions (slice 8)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD), EditMode. Batchmode (no `-quit`; re-run after a source change; verify XML). Branch
`027-guarded-await` (stacks on master). **FOUNDATION change (graphcore) — strictly additive + back-compatible
(US2); gameflow UNTOUCHED.** graphcore + graphstandard append-only, both `0.6.0 → 0.7.0`.

## Phase 1: US1 + US2 — the resume gate (Priority: P1) 🎯

**Goal**: a matching await-signal resumes only if all resume conditions pass (re-arm on failure); no conditions ⇒
current behavior.

**Independent test**: gate-false → stays parked; gate-true → advances; AND; null-skip; empty ⇒ immediate; wrong
name ignored.

- [X] T001 [P] [US1] New graphcore EditMode test file `com.faolline.graphcore/Tests/EditMode/Execution/GuardedAwaitTests.cs`: build a graph Start → `room`(Await `"exit"`) → End; park on `room`; with a `ResumeConditions` reading a context bool `gateOpen`: (a) `gateOpen=false`, `RaiseSignal("exit")` ⇒ runner state still `WaitingForSignal`, no advance (INV-1); (b) set `gateOpen=true`, `RaiseSignal("exit")` ⇒ advances past `room` (INV-2, re-arm); (c) two resume conditions ⇒ AND, any false → no resume (INV-3); (d) a null entry in the list is skipped, not a failed gate (INV-4). Use existing test conditions (`Faolline.GraphTest` bool/collection conditions) or a minimal stub. Confirm RED (`ResumeConditions` missing).
- [X] T002 [P] [US2] Add to `GuardedAwaitTests.cs`: empty `ResumeConditions` ⇒ matching `RaiseSignal` resumes immediately (back-compat, INV-5); a wrong signal name is ignored regardless of conditions (INV-5). Confirm RED.
- [X] T003 [US1] Implement in graphcore: `BaseNodeData` — add `[SerializeField] private List<BaseCondition> _resumeConditions = new List<BaseCondition>();` + `public List<BaseCondition> ResumeConditions => _resumeConditions;` (XML docs, mirroring `EntryConditions`). `BaseRunner.ResumeIfAwaiting` — resume only if `node.AwaitSignalName == name && ResumeConditionsPass(node)`; add private `bool ResumeConditionsPass(BaseNodeData node)` mirroring the entry-condition loop (null-skip-with-`[GraphCore]`-warning, AND). No other change. Confirm T001/T002 GREEN.

## Phase 2: US3 — author from the code builder (Priority: P2)

- [X] T004 [P] [US3] In `com.faolline.graphstandard/Tests/EditMode/Builder/…` (extend the builder tests or a new file): `builder.AddStatement(...).Await("exit").ResumeWhen(cond)` produces a node whose `ResumeConditions` contains `cond`, and running it reproduces INV-1/INV-2. Confirm RED (`ResumeWhen` missing).
- [X] T005 [US3] Implement `GraphNodeBuilder.ResumeWhen(params BaseCondition[] conditions)` in `com.faolline.graphstandard/Runtime/Builder/GraphNodeBuilder.cs`: append non-null conditions to `Node.ResumeConditions`; return `this`; XML docs mirroring `When`. Confirm T004 GREEN.

## Phase 3: Polish

- [X] T006 Run the ENTIRE suite via batchmode: graphcore EditMode (prior + guarded-await), graphstandard EditMode (prior + builder), gameflow EditMode all green, AND PlayMode (9) green (gameflow untouched, INV-8). Record totals.
- [X] T007 [P] Bump `com.faolline.graphcore/package.json` `0.6.0 → 0.7.0` and `com.faolline.graphstandard/package.json` `0.6.0 → 0.7.0` (its `dependencies.com.faolline.graphcore` → `0.7.0`). Update graphcore README (await section: the resume-gate + re-arm note) + CHANGELOG; graphstandard README (builder: `ResumeWhen`) + CHANGELOG.
- [X] T008 [P] Verify append-only: `AwaitSignalName`/`EntryConditions`/`RaiseSignal` and all other signatures unchanged; pre-existing await flows (no resume conditions) identical; gameflow untouched; XML docs + prefixes on every new member.

## Dependencies

- **T001/T002 (graphcore tests)** → **T003 (graphcore impl)**. **T004 (builder test)** → **T005 (builder impl)**;
  T004/T005 depend on T003 (the field must exist). Polish (T006–T008) last.

## Implementation strategy

- Mirror `EntryConditions` for the field and its evaluation loop; the only new behavior is one AND-gate in
  `ResumeIfAwaiting` with **ignore-not-consume** (re-arm) semantics. Back-compat is structural: an empty list
  passes the gate vacuously, so every existing await flow is unchanged (US2). The builder sugar mirrors `When`.
