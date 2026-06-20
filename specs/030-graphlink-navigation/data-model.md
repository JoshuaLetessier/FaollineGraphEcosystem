# Data Model — GraphLink cross-reference + editor navigation

## Entity: `GraphLinkNodeData` (graphcore Runtime)

A non-executing node that documents an association from its host graph to another graph.

| Field | Type | Notes |
|---|---|---|
| `NodeType` (const) | `string` | `"graphcore/graph-link"`. Identifies the node; no magic strings at call sites. |
| `TargetGraph` | `BaseGraph` | Serialized reference to the associated graph (any kind). MAY be null (renders "missing"). Never a typed lib graph (Principle VII/II). |
| `Note` | `string` | Optional author annotation/label. |
| *(inherited)* `Id`, `Title`, `Position`, … | from `BaseNodeData` | Standard node fields. `OnEnter/OnExit` actions are present on the base type but are NEVER run for a GraphLink. |

**Validation / rules**:
- No execution state. The node carries no conditions/actions semantics of its own.
- `TargetGraph` is allowed to be the host graph itself or to point at another GraphLink — purely cosmetic, no
  cycle concern (nothing executes).
- Append-only: this is a NEW `BaseNodeData` subclass; it adds no fields to `BaseNodeData`.

**State transitions (runtime)**: none. On enter → immediate pass-through advance (no pause, no actions, no
executor). Off-path → never entered.

## Entity: `GraphEditorWindowRegistry` (graphcore Editor)

A static, opt-in map from a concrete graph type to the editor that opens it.

| Member | Signature | Notes |
|---|---|---|
| `Register` | `void Register(Type graphType, Action<BaseGraph> opener)` | Called by a lib editor (`[InitializeOnLoadMethod]`). Last registration for a type wins; null args are ignored with a `[GraphCore]` warning. |
| `Open` | `void Open(BaseGraph graph)` | Resolves an opener for `graph.GetType()` (walking base types); invokes it. Fallback when none/`graph==null`: `Selection.activeObject = graph; EditorGUIUtility.PingObject(graph)` + `[GraphCore]` diagnostic. Never throws. |
| `TryGetOpener` | `bool TryGetOpener(Type graphType, out Action<BaseGraph> opener)` | Lookup helper (used by tests + the node view). |
| `Clear` | `void Clear()` | Test hook (mirrors `NodeTypeColorRegistry.Clear`). |

**Rules**:
- graphcore holds only `Type → delegate`; it never names a downstream type. The mapping is supplied by the libs.
- Resolution walks base types so a lib MAY register for a base graph type.

## Relationships

```text
Host BaseGraph
  └─ GraphLinkNodeData ──TargetGraph──▶ any BaseGraph (QuestGraph / DialogueGraph / GameFlowGraph / …)
                                            ▲
GraphLinkNodeView ──double-click──▶ GraphEditorWindowRegistry.Open(TargetGraph)
                                            │ resolves Type→opener (registered by the lib's editor)
                                            └─ opens the target in its window  (or pings/selects on miss)
```
