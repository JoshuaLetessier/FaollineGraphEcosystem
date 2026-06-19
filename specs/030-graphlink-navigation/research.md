# Research — GraphLink cross-reference + editor navigation

No NEEDS CLARIFICATION remained from the spec. The following decisions resolve the design choices.

## D1 — A new, distinct node type (not a flagged SubGraph)

- **Decision**: Introduce `GraphLinkNodeData : BaseNodeData` as a brand-new node type, separate from
  `SubGraphNodeData`.
- **Rationale**: `SubGraphNodeData` means "execute this graph" (the runner traverses its `TargetGraph`). A
  documentary reference must mean the opposite — "never execute". Overloading SubGraph with an "annotation
  only" flag invites runtime mistakes and muddies a clear, frozen contract. A separate type makes the
  semantics unambiguous and keeps Principle VII intact (SubGraph stays the *only* invocation mechanism).
- **Alternatives considered**: (a) reuse `SubGraphNodeData` + `IsAnnotation` flag — rejected (semantic
  ambiguity, runtime-skip branch tangled into the executed path). (b) Graph-level metadata list instead of a
  node — rejected (the user explicitly wants it VISIBLE in the canvas when opening the graph).

## D2 — Runtime contract: inert pass-through

- **Decision**: When the runner enters a `GraphLinkNodeData`, it runs no enter/exit actions and no executor,
  and immediately advances along its outgoing edge (no `NodeReady` pause). With no outgoing edge it terminates
  like any dead-end (Ended). Off the execution path it is simply never reached.
- **Rationale**: Honours FR-003/FR-004 ("never executed; if on-path, pass straight through without pausing").
  The common case (off-path) is already inert; the on-path safety-net is a tiny dispatch branch in the node
  type handling, next to the existing `SubGraphNodeData` / `EndNodeData` branches.
- **Alternatives considered**: let a GraphLink pause at `NodeReady` like a regular node — rejected (spec says
  "without pausing"; a stray pause would silently stall a flow that accidentally wired one in).

## D3 — Editor navigation via an opt-in registry (mirrors NodeTypeColorRegistry)

- **Decision**: A static graphcore-Editor `GraphEditorWindowRegistry` mapping `System.Type` (concrete graph
  type) → `Action<BaseGraph>` opener. graphcore exposes `Register(Type, Action<BaseGraph>)` and
  `Open(BaseGraph)`. Each downstream lib editor registers its window in an `[InitializeOnLoadMethod]`. If no
  opener matches the graph's type (walking base types), `Open` falls back to `Selection.activeObject` +
  `EditorGUIUtility.PingObject` and logs a `[GraphCore]` diagnostic.
- **Rationale**: Keeps graphcore free of any downstream-lib knowledge (Principle II) — exactly the pattern
  already used by `NodeTypeColorRegistry`. Graceful fallback satisfies FR-007 (never fail). Walking base types
  lets a lib register for a base graph type if desired.
- **Alternatives considered**: graphcore hard-codes "QuestGraph → quest window" — rejected (downstream coupling,
  violates II). Reflection-scan for `[CustomEditor]`-style attributes — rejected (heavier, brittle, YAGNI).

## D4 — Node view rendering + open affordance

- **Decision**: `GraphLinkNodeView : BaseNodeView` with distinct USS styling, showing `"<Kind>: <Name>"` where
  Kind is derived generically from the target graph's type name (e.g. `QuestGraph` → "Quest") and Name from the
  target's display name / asset name; a broken/missing target shows a clear "(missing)" label. Double-click
  (MouseDownEvent, clickCount == 2) calls `GraphEditorWindowRegistry.Open(TargetGraph)`.
- **Rationale**: Reuses the established `BaseNodeView` + USS conventions (no inline CSS per dev standards).
  Kind-from-type-name keeps it generic (no lib knowledge). Double-click matches the spec's "open" affordance.
- **Alternatives considered**: an explicit "Open" button only — kept as a secondary affordance is possible but
  double-click is the primary, lowest-friction interaction; a button can be added later if needed (YAGNI).

## D5 — Placement and identity

- **Decision**: `GraphLinkNodeData` lives in `com.faolline.graphcore/Runtime/Nodes`. `NodeType` const
  `"graphcore/graph-link"`. Fields: `BaseGraph TargetGraph`, `string Note`.
- **Rationale**: Universal authoring concern → graphcore. Const NodeType per dev standards (no magic strings).
