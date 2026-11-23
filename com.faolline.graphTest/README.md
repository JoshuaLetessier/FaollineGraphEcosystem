# com.faolline.graphTest

**Version**: 0.3.1 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.44.0, `com.faolline.graphstandard` ≥ 0.18.0, `com.faolline.graphlogging` ≥ 0.2.0

**Internal verification package — not for distribution.**

Three things in one dev-only package:
- an integration test suite that exercises the graphcore editor and runtime surface through a concrete
  test graph type;
- a **sandbox** for trying graphcore by hand: the TestGraph editor window (**Faolline ▸ Open TestGraph
  Editor**) and six sample graphs;
- the ecosystem-wide **assembly dependency guard**, `DependencyMatrixTests` (see "Architecture" below).

It is **not** part of the consumable ecosystem: it is intentionally absent from the module selector
whitelist and nothing depends on it. Kept in the repo for development and CI only.

---

## What it tests

- **TestGraph / TestGameContext** — concrete graph and context subclasses used as test fixtures
- **Test actions & conditions** — `TestLogAction`, `TestSet{Bool,Int,Float,String}Action`,
  `TestRecipeAction`, `Test{Bool,Int,Float,String}Condition`, collection/signal conditions and the
  always-true/false doubles. They are verification scaffolding on the raw-string key channel, not the
  canonical nodes (those live in graphcore / graphstandard)
- **Sample builders** — menu items that generate fully-wired sample graphs for manual inspection
- **Editor integration** — `TestGraphEditorWindow` exercises the graph editor surface
- **Architecture** — `Tests/EditMode/Architecture/DependencyMatrixTests.cs` re-reads every ecosystem
  asmdef and checks its references against an `Allowed` allowlist keyed by assembly tier (see
  `../ARCHITECTURE.md`). This is the ecosystem's compiler-enforced dependency-tier guard: asmdef
  references already make an illegal `using` a compile error, but this test makes adding an illegal
  asmdef *reference* a test failure, so the tier rules (verticals never reference each other, external
  dependencies live only in adapter assemblies, Runtime never references Editor) survive future edits

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
    TestGraph.cs                ← Concrete graph asset for test fixtures
    TestGameContext.cs          ← Concrete context for test fixtures (TestGameContext : BaseContext)
    TestContextKeys.cs          ← Typed variable key constants (the Test* actions/conditions deliberately stay on
                                   the raw-string channel — graphcore's islands escape hatch — never VariableDef)
    FlowSampleDriver.cs         ← Sample driver exercising the Flow execution paradigm
    ReactiveSampleDriver.cs     ← Sample driver exercising the Reactive execution paradigm
    Actions/                    ← Test-only action implementations (recording, no real effects)
    Choices/
      TestChoice.cs              ← Test choice implementation
    Conditions/                 ← Test-only condition implementations
      ComparisonOperator.cs      ← Shared operator enum for the numeric/string conditions
    Nodes/
      TestStatementNodeData.cs   ← Test statement node data
  Editor/
    Window/
      TestGraphEditorWindow.cs   ← Graph editor window for manual inspection
    Graph/
      TestGraphView.cs           ← GraphView surface for TestGraph
    Edges/
      TestEdgeView.cs            ← Edge view for the test graph
    Inspector/
      TestNodeInspectorView.cs   ← Node inspector view for the test graph
    Nodes/                      ← Node-view implementations (5 files: Choice, End, Start, SubGraph, TestStatement)
    Samples/                    ← Menu-driven sample graph generators (SampleGraphBuilder, ReactiveFlowSampleBuilder)
  Samples/                     ← Generated sample graph assets (SampleAuthoringGraph, SampleChildGraph,
                                  SampleCompleteGraph, SampleFlowFork, SampleHistoryStress, SampleReactiveProgression)
  Tests/
    EditMode/                  ← NUnit test assembly (21 test files)
      Architecture/              ← DependencyMatrixTests — see "Architecture" above
      Editor/                    ← Editor-surface tests (graph view, inspector, node views, window session)
      Runtime/                   ← Runtime coverage (actions, conditions, history, signals, sub-graphs, typed context)
```
