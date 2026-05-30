# Implementation Plan: EditMode Test Suite

**Branch**: `004-editmode-test-suite` | **Date**: 2026-05-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-editmode-test-suite/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Build the five `Execution/` EditMode test fixtures that cover `BaseRunner`'s full state machine,
`BaseContext`'s typed blackboard, history snapshot integrity, sub-graph push/pop and context
semantics, and runtime cycle detection via `GraphCycleException`. All tests use NUnit with no
shared mutable state, no disk assets, and no PlayMode dependency.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.x

**Primary Dependencies**: `com.unity.test-framework` (NUnit 3 via Unity), existing
`com.faolline.graphcore.Tests.EditMode.asmdef` which already references both Runtime and Editor assemblies

**Storage**: N/A — tests build `ScriptableObject` instances entirely in memory

**Testing**: Unity EditMode Test Runner, NUnit `[Test]` / `[SetUp]` / `[TearDown]`

**Target Platform**: Unity Editor (EditMode only). No PlayMode, no scene loading, no `UnityEditor` namespace.

**Project Type**: Test assembly completing the EditMode suite for a Unity runtime library

**Performance Goals**: Full suite < 5 seconds; each test < 50 ms

**Constraints**: No `ScriptableObject` leaks; no shared mutable state between tests;
no disk assets; all `ScriptableObject` instances destroyed in `[TearDown]`

**Scale/Scope**: 5 fixture classes, ~45 test methods in `Tests/EditMode/Execution/`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Foundation Stability | ✅ Pass | Test assembly only — no Runtime public API changes |
| II. Universal Abstractions Only | ✅ Pass | Tests cover only graphcore types; no domain-specific knowledge |
| III. Specification-First | ✅ Pass | `spec.md` complete and approved before plan created |
| IV. Test-Driven Development | ✅ Pass | This feature IS the test suite. For any missing test: confirm it fails before writing the assertion, then implement |
| V. Simplicity (YAGNI) | ✅ Pass | No abstraction beyond shared `BuildChainGraph` / `Track` helpers; inner stub classes only |
| VI. Cross-lib Compatibility | ✅ Pass | `SubGraphTests` and `CycleDetectionTests` validate the mandatory runtime stack and cycle checks from Constitution Principle VI |

**Post-design re-check**: No violations. All five fixtures stay in the existing
`com.faolline.graphcore.Tests.EditMode.asmdef` — no new assembly definition required.

## Project Structure

### Documentation (this feature)

```text
specs/004-editmode-test-suite/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (test fixture structure)
├── quickstart.md        # Phase 1 output (how to run and extend tests)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code

```text
Tests/EditMode/Execution/
├── BaseRunnerTests.cs           # State machine: transitions, actions, executor, stuck, choices
├── BaseContextTests.cs          # Typed get/set, events, DeepClone
├── HistoryTests.cs              # GoBack, GoBackToCheckpoint, depth cap, unlimited
├── SubGraphTests.cs             # Push/pop, inherit/isolate context, OnEnd propagation
└── CycleDetectionTests.cs       # Direct cycle, indirect, valid acyclic, exception payload

Tests/EditMode/
└── com.faolline.graphcore.Tests.EditMode.asmdef   # Existing — no changes needed
```

**Structure Decision**: All five fixtures go in `Tests/EditMode/Execution/` — the existing
directory. The canonical names (`BaseRunnerTests`, `BaseContextTests`, etc.) supersede the
earlier `BaseRunnerLinearTests` / `ExecutionBaseContextTests` naming; those files are
consolidated into the five single-responsibility fixtures specified here.

## Complexity Tracking

No constitution violations requiring justification.
