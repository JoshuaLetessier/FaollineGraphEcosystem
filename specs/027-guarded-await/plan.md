# Implementation Plan: guarded await — re-armable signal resume conditions (slice 8)

**Branch**: `027-guarded-await` | **Date**: 2026-06-11 | **Spec**: [spec.md](spec.md)

## Summary

A foundation capability: an awaiting node may carry optional **resume conditions**; a matching `RaiseSignal`
resumes the parked node only if all pass, else the raise is **ignored and the node stays parked** (re-arm).
graphcore: add `BaseNodeData.ResumeConditions` (mirrors `EntryConditions`; default empty) and gate
`BaseRunner.ResumeIfAwaiting` on those conditions. graphstandard: a fluent `GraphNodeBuilder.ResumeWhen(...)`.
gameflow untouched (its `RaiseSignal` routes through the runner, which enforces the gate). graphcore + graphstandard
`0.6.0 → 0.7.0`; all existing suites stay green; no resume conditions ⇒ byte-for-byte current behavior.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.0. **Primary Dependencies**: graphcore (`BaseNodeData`, `BaseRunner`,
`BaseCondition`, `RunnerState`); graphstandard builder depends on graphcore `0.7.0`. **Storage**: none.
**Testing**: NUnit EditMode — gate-false signal stays parked; gate-true resumes; AND of multiple; null-skip; no
conditions = immediate (back-compat); wrong name ignored; builder `ResumeWhen` reproduces the gate. **Target
Platform**: Unity runtime + Editor. **Project Type**: foundation additive (one optional field + a gated branch)
+ a builder method. **Constraints**: append-only (graphcore `AwaitSignalName`/`EntryConditions`/`RaiseSignal`
unchanged; pre-existing assets identical); gameflow untouched; `[GraphCore]`/`[GraphStandard]` prefixes; XML
docs. **Scope**: `BaseNodeData.cs` + `BaseRunner.cs` (graphcore) + `GraphNodeBuilder.cs` (graphstandard) + tests
+ READMEs/CHANGELOGs + two MINOR bumps.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability (NON-NEGOTIABLE) | ✅ PASS | graphcore additive `0.6.0 → 0.7.0`: one optional node field (default empty) + a gated resume branch. Pre-existing await flows (no resume conditions) behave identically (US2, tested). `AwaitSignalName`/`EntryConditions`/`RaiseSignal` signatures unchanged. All suites stay green. |
| II. Universal Abstractions Only | ✅ PASS | Resume conditions are universal `BaseCondition`s over the context — no domain vocabulary. The capability (re-armable guarded signal) is a substrate primitive. |
| III. Specification-First | ✅ PASS | spec approved (16/16). |
| IV. Test-Driven Development (NON-NEGOTIABLE) | ✅ PASS | Test-first: gate-false/true, AND, null-skip, back-compat, wrong-name, builder sugar. |
| V. Simplicity (YAGNI) | ✅ PASS | One field + one AND-gate reusing the entry-condition pattern; re-arm = ignore (no latch/queue); no time/entry gate. |
| VI. Typed Context Contract | ✅ PASS (N/A) | No context shape change. |
| VII. Cross-lib via SubGraph only | ✅ PASS | graphstandard builder depends only on graphcore; gameflow needs no change. |
| Dev standards | ✅ PASS | XML docs on the field, the gated path, and `ResumeWhen`; prefixes; one class per file (edited). |

**Result**: PASS — no violations, no deviations. (A foundation change, but strictly additive and back-compatible.)

## Project Structure

```text
com.faolline.graphcore/
├── package.json                                   # 0.6.0 → 0.7.0
├── Runtime/Nodes/BaseNodeData.cs                  # MODIFIED (additive): ResumeConditions
├── Runtime/Execution/BaseRunner.cs                # MODIFIED: ResumeIfAwaiting gated; ResumeConditionsPass helper
└── Tests/EditMode/...                             # NEW guarded-await tests

com.faolline.graphstandard/
├── package.json                                   # 0.6.0 → 0.7.0 (dep graphcore 0.7.0)
├── Runtime/Builder/GraphNodeBuilder.cs            # MODIFIED (additive): ResumeWhen(params BaseCondition[])
└── Tests/EditMode/Builder/...                     # builder ResumeWhen coverage (extend or new)

# com.faolline.graphgameflow/ : UNCHANGED.
```

**Structure Decision**: the gate lives in graphcore next to the existing await park. `ResumeConditions` mirrors
`EntryConditions` byte-for-byte (serialized list + property). `ResumeIfAwaiting` gains an AND-gate via a private
`ResumeConditionsPass(node)` that mirrors the entry-condition loop (null-skip-with-warning). The builder sugar is
a one-liner mirroring `When`.

## Phase 0 — Research

See [research.md](research.md): R1 mirror `EntryConditions` for the field (proven pattern, serialization,
tolerance); R2 gate in `ResumeIfAwaiting` with ignore-not-consume (re-arm) — the semantic differentiator vs
gating an outgoing edge; R3 host override (`Advance`/GoTo) stays ungated; R4 builder `ResumeWhen` mirrors `When`.

## Phase 1 — Design & Contracts

[data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [quickstart.md](quickstart.md).

## Implementation Sequencing (TDD)

1. **Tests (test-first)** — graphcore EditMode (new file): park an await node (set `AwaitSignalName`) + a resume
   condition reading a context bool/collection; (a) raise matching signal, condition false → still
   `WaitingForSignal`, no advance; (b) set condition true, raise again → advances (re-arm); (c) two conditions →
   AND; (d) null in list → skipped, not a failed gate; (e) empty list → immediate resume (back-compat);
   (f) wrong name → ignored. Plus graphstandard builder test: `Await(name).ResumeWhen(cond)` reproduces (a)/(b).
   Confirm RED (`ResumeConditions`/`ResumeWhen` missing).
2. **Implement**: graphcore `BaseNodeData.ResumeConditions` (serialized list + property, mirroring
   `EntryConditions`); `BaseRunner.ResumeIfAwaiting` → resume only if name matches AND `ResumeConditionsPass(node)`;
   add the private helper (AND, null-skip-with-warning). graphstandard `GraphNodeBuilder.ResumeWhen(params
   BaseCondition[])` appending to `Node.ResumeConditions`. XML docs. Confirm GREEN.
3. **Finalize**: full suite via batchmode (graphcore + graphstandard + gameflow EditMode green; PlayMode 9 green);
   bump both `0.7.0` (graphstandard dep → graphcore 0.7.0); READMEs (graphcore await section: the resume-gate +
   re-arm note; graphstandard builder: `ResumeWhen`) + CHANGELOGs; verify append-only.

## Complexity Tracking

> No violations — empty. (A foundation edit, justified: a universal re-armable signal gate, strictly additive
> and back-compatible; US2 guarantees pre-existing behavior.)
