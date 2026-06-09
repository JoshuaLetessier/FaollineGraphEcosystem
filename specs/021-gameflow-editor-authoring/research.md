# Phase 0 — Research: gameflow editor authoring

## R1 — Reuse `AddBaseNodeSection`; add only a Flow foldout for await/wait

**Decision**: The `GameFlowNodeInspectorView` calls graphcore's `BaseNodeInspectorView.AddBaseNodeSection`
(which already renders `_title`, `_isCheckpoint`, `_entryConditions`, `_onEnterActions`, `_onExitActions`,
color) and adds one small **Flow** foldout with bound PropertyFields for `_awaitSignal` and `_waitDuration`.

**Rationale**: Attaching a `LoadSceneAction` to a node is just adding it to the `On Enter Actions` list —
which the base section already exposes as an editable object list. So US2's "attach a Load Scene action" is
**free** from the base. The only gap is the two newer `BaseNodeData` fields (`_awaitSignal`, `_waitDuration`)
the base section predates; the gameflow inspector adds them. Reusing the base honors Constitution V (don't
reimplement graphcore).

**Alternatives**: *a bespoke actions editor in gameflow* — rejected: duplicates the base section. *touch
graphcore to add await/wait to the base section* — rejected: graphcore must stay untouched; the field is
gameflow-relevant enough to surface in gameflow's inspector.

## R2 — Author the universal node types directly; name via `_title`

**Decision**: gameflow authors graphcore's universal node types directly (`StartNodeData`,
`StatementNodeData`, `ChoiceNodeData`, `SubGraphNodeData`, `EndNodeData`). Node naming uses the existing
`BaseNodeData._title` (surfaced by `AddBaseNodeSection`); no `GameFlowStatementNodeData` label subclass.

**Rationale**: starterGraph added `StarterStatementNodeData` only for a `_label`; `_title` now covers naming
universally, so a subclass is unneeded complexity (YAGNI). Fewer types, and the driver already runs these
universal types.

**Alternatives**: *mirror starter's statement-label subclass* — rejected as redundant with `_title`.

## R3 — No in-editor runner toolbar; Save + Validate only

**Decision**: The window toolbar is the base **Save** button plus a **Validate** button (graphcore's
`GraphValidator`). No in-editor "Run/Continue/Choose" execution preview.

**Rationale**: gameflow's run path is the `GraphFlowDriver` in Play — that is the product. starterGraph's
in-editor runner is a verification aid for the core; gameflow does not need to duplicate it for slice 2, and
an in-editor runner would have to handle await-signal / scene-load specially (scene loads in edit mode are
undesirable). YAGNI; can be added later.

**Alternatives**: *mirror starter's Run/GoBack toolbar* — deferred (extra surface, edit-mode scene-load
hazard). *no toolbar at all* — rejected: Validate is cheap and useful for catching a missing Start / dangling
edge before pressing Play.

## R4 — `GameFlowSampleBuilder` mirrors `StarterSampleBuilder`

**Decision**: A static `[MenuItem("Faolline/GraphGameFlow/Create Reference Scene-Flow Sample")]` that builds a
`GameFlowGraph` asset (start → load-A statement → await-"advance" statement → load-B statement → end) with two
`LoadSceneAction` sub-assets (`AssetDatabase.AddObjectToAsset`) attached to the two statement nodes' enter
lists, GUID node/edge ids, saved to a uniquely-named asset path (`AssetDatabase.GenerateUniqueAssetPath`) so
re-running never clobbers an existing sample.

**Rationale**: Direct copy of the proven `StarterSampleBuilder` shape (sub-asset actions keep the graph
portable). Unique path satisfies the "re-run produces a fresh asset" edge case.

**Alternatives**: *a plain (non-sub-asset) action* — rejected: sub-assets keep the sample self-contained.

## R5 — Test boundary: data/attributes/sample-run are TDD'd; views are not unit-tested

**Decision**: EditMode tests cover: `GameFlowGraph` is a `BaseGraph` and carries `[CreateAssetMenu]`;
`LoadSceneAction` carries `[CreateAssetMenu]`; `GameFlowSampleBuilder` produces the exact reference structure
(five nodes, four edges, the await node, two attached `LoadSceneAction`s) AND, when that asset is driven by a
`GraphFlowDriver` with a recording loader, walks A → await → B → end. The window / graph view / node views /
inspector are validated by compiling and by the sample opening in the window — not unit-tested.

**Rationale**: This is exactly how the sibling package editors (starterGraph, dialogue) are validated — their
windows/views are not unit-tested; their data and sample builders are. Pointer-driven canvas interaction is
not meaningfully unit-testable. The TDD-mandatory surface (data + sample behavior) is fully test-driven.

**Alternatives**: *headless tests that instantiate the window and simulate clicks* — rejected: brittle, and
not the ecosystem norm.

## R6 — `GameFlowGraph : BaseGraph` keeps the driver unchanged

**Decision**: `GameFlowGraph` is a trivial `BaseGraph` subclass carrying only `[CreateAssetMenu]`. The
slice-1 `GraphFlowDriver._graph` stays typed as `BaseGraph`, so a `GameFlowGraph` is assignable with no
runtime change.

**Rationale**: The editor window needs a concrete asset type to target (`[OnOpenAsset]`, Create menu), as
every sibling package has (`StarterGraph`, `DialogueGraph`). Keeping the driver field as `BaseGraph` preserves
flexibility (a plain `BaseGraph` still runs) and means zero slice-1 runtime change.

**Alternatives**: *retype the driver to `GameFlowGraph`* — rejected: a slice-1 runtime change, and less
flexible. *no graph subclass (author a bare `BaseGraph`)* — rejected: no Create-menu entry and the window
can't target a type, contradicting the sibling-editor pattern and the user's "create it" need.
