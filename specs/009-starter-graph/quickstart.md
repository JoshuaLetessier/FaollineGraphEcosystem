# Quickstart: starterGraph

**Feature**: `009-starter-graph` | **Date**: 2026-05-30

Walkthroughs to validate the starter, one per user story. Each maps to the spec's Independent Test.

## US1 — Runtime foundation (headless)

1. In an EditMode test, create a `StarterGraph` with Start → Statement(set `Score=5`) → edge gated by `StarterIntCondition(Score>=3)` → End; run via `BaseRunner` → the branch is taken.
2. Set typed values on a `StarterContext`, clone it → the clone is a `StarterContext` with the same bool/int/float/string values.
3. Evaluate each condition with a missing/wrong-typed key → false + warning, no throw.

## US2 — Full editor

1. Open the starter editor (double-click a `StarterGraph` asset, or via the menu).
2. Context menu → add a Start, Statement, Choice, SubGraph, and End node.
3. Inspector: edit the Statement label; set the End reason; on the Choice add two choices with labels + conditions; on the SubGraph assign a target graph and toggle inherit-context; in the parameter panel add an Int and a String parameter.
4. Wire Start → Statement → Choice → (branches) → End; **Run** → it logs nodes, pauses at the Choice; **Choose** offers the condition-passing choices; pick one → execution resumes; use **GoBack** / **Checkpoint** / **Continue** to navigate.

## US3 — Robustness & ergonomics

1. **Save** the graph, close/reopen the window → all edges are drawn (choice + sub-graph edges included).
2. Open a second `StarterGraph` asset → it opens in its own window titled by the asset name (two windows side by side).
3. On a SubGraph node, assign the **current** graph as its target → refused with a cycle message, field reverts.
4. Add/remove a choice → ports update live and other choices' edges stay connected.
5. Menu → generate the sample graph → open it and **Run**: it descends a sub-graph, pauses at a choice (typed conditions filter options), and ends — demonstrating the whole starter.

## Regression gate

After each user story, run the full EditMode suite (Unity Test Runner) — all prior + new tests green (SC-008).
