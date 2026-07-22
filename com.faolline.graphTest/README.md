# com.faolline.graphTest

**Version**: 0.2.1 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.35.0, `com.faolline.graphstandard` ≥ 0.17.0

**Internal verification package — not for distribution.**

Integration test suite for the Faolline Graph Ecosystem. Exercises the graphcore editor and runtime
surface through concrete test graph types, sample builders, and NUnit test assemblies.

It is **not** part of the consumable ecosystem: it is intentionally absent from the module selector
whitelist and nothing depends on it. Kept in the repo for development and CI only.

---

## What it tests

- **TestGraph / TestContext** — concrete graph and context subclasses used as test fixtures
- **Test actions & conditions** — `TestAction`, `TestCondition`, and typed variants that record
  invocations for assertion (not real logic — pure verification scaffolding)
- **Sample builders** — menu items that generate fully-wired sample graphs for manual inspection
- **Editor integration** — `TestGraphEditorWindow` exercises the graph editor surface

---

## Running the tests

1. Open **Window ▸ General ▸ Test Runner** in Unity.
2. Select the **EditMode** tab.
3. Run all tests under `Faolline.GraphTest` and `Faolline.GraphCore.Tests`.

All tests run headless (no Play mode required). The test assemblies use `[assembly: InternalsVisibleTo]`
to access internal graphcore APIs where needed.

---

## Architecture

```
com.faolline.graphTest/
  Runtime/
    TestGraph.cs              ← Concrete graph asset for test fixtures
    TestContext.cs             ← Concrete context for test fixtures
    TestContextKeys.cs         ← Typed variable key constants (the Test* actions/conditions deliberately stay on
                                  the raw-string channel — graphcore's islands escape hatch — never VariableDef)
    Actions/                   ← Test-only action implementations (recording, no real effects)
    Conditions/                ← Test-only condition implementations
  Editor/
    TestGraphEditorWindow.cs   ← Graph editor window for manual inspection
    SampleBuilders/            ← Menu-driven sample graph generators
  Tests/
    EditMode/                  ← NUnit test assemblies (graphcore + graphstandard coverage)
```
