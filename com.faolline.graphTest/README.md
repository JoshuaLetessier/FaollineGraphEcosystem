# com.faolline.graphTest

**Version**: 0.4.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.starterGraph` ≥ 0.6.1, `com.faolline.graphcore` ≥ 0.44.0, `com.faolline.graphstandard` ≥ 0.18.0, `com.faolline.graphlogging` ≥ 0.2.0

**Internal verification package — not for distribution.**

Three things in one dev-only package:
- an integration test suite that exercises the graphcore editor and runtime surface through the
  **starterGraph template's** graph, choice, statement node, editor window, view and inspector;
- a **sandbox** for trying graphcore by hand: six sample graphs (they are `StarterGraph` assets, so
  double-clicking one opens the StarterGraph editor, **Faolline ▸ Open StarterGraph Editor**);
- the ecosystem-wide **assembly dependency guard**, `DependencyMatrixTests` (see "What it tests" below).

graphTest owns no graph type or editor of its own: it builds on `com.faolline.starterGraph`. The
template therefore stays covered by this whole suite, not only by its own small one, and every graphcore
API change lands once in the template instead of twice.

It is **not** part of the consumable ecosystem: it is intentionally absent from the module selector
whitelist and nothing depends on it. Kept in the repo for development and CI only.

---

## What it tests

- **The starterGraph template** — `StarterGraph`, `StarterChoice`, `StarterStatementNodeData`, and the
  editor surface (`StarterGraphEditorWindow`, `StarterGraphView`, `StarterNodeInspectorView`,
  `StarterEdgeView`, the node views) under graphcore's runtime and editor behavior
- **TestGameContext** — a typed context subclass used as a fixture (typed properties, clone keeps the subtype)
- **Test actions & conditions** — `TestLogAction`, `TestSet{Bool,Int,Float,String}Action`,
  `TestRecipeAction`, `Test{Bool,Int,Float,String}Condition`, collection/signal conditions and the
  always-true/false doubles. They are verification scaffolding on the raw-string key channel, not the
  canonical nodes (those live in graphcore / graphstandard)
- **Sample builders** — menu items (**Faolline ▸ Create Sample TestGraph …**) that generate fully-wired
  sample graphs for manual inspection
- **Architecture** — `Tests/EditMode/Architecture/DependencyMatrixTests.cs` re-reads every ecosystem
  asmdef and checks its references against an `Allowed` allowlist keyed by assembly tier (see
  `../ARCHITECTURE.md`). This is the ecosystem's compiler-enforced dependency-tier guard: asmdef
  references already make an illegal `using` a compile error, but this test makes adding an illegal
  asmdef *reference* a test failure, so the tier rules (verticals never reference each other, external
  dependencies live only in adapter assemblies, Runtime never references Editor) survive future edits

### Naming note

Inside any `Faolline.*` namespace other than `Faolline.StarterGraph`, the bare name `StarterGraph`
resolves to the *namespace* `Faolline.StarterGraph`, not to the class. graphTest code therefore uses the
alias `using StarterGraphAsset = Faolline.StarterGraph.StarterGraph;`.

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
    TestGameContext.cs          ← Typed context fixture (TestGameContext : BaseContext)
    TestContextKeys.cs          ← Typed variable key constants (the Test* actions/conditions deliberately stay on
                                   the raw-string channel — graphcore's islands escape hatch — never VariableDef)
    FlowSampleDriver.cs         ← Sample driver exercising the Flow execution paradigm
    ReactiveSampleDriver.cs     ← Sample driver exercising the Reactive execution paradigm
    Actions/                    ← Test-only action implementations (recording, no real effects)
    Conditions/                 ← Test-only condition implementations
      ComparisonOperator.cs      ← Shared operator enum for the numeric/string conditions
  Editor/
    Samples/                    ← Menu-driven sample graph generators (SampleGraphBuilder, ReactiveFlowSampleBuilder)
  Samples/                     ← Generated sample graph assets (StarterGraph assets: SampleAuthoringGraph,
                                  SampleChildGraph, SampleCompleteGraph, SampleFlowFork, SampleHistoryStress,
                                  SampleReactiveProgression)
  Tests/
    EditMode/                  ← NUnit test assembly (21 test files)
      Architecture/              ← DependencyMatrixTests — see "What it tests" above
      Editor/                    ← Editor-surface tests on the template (graph view, inspector, node views, window session)
      Runtime/                   ← Runtime coverage (template types, actions, conditions, history, signals, sub-graphs, typed context)
```
