# Public API / UX Contract — gameflow editor authoring (0.2.0)

Runtime namespace `Faolline.GraphGameFlow`; editor namespace `Faolline.GraphGameFlow.Editor`. `[GraphGameFlow]`
log prefix; XML docs on public members; node-view styling via USS.

## Runtime (additive)

```csharp
[CreateAssetMenu(menuName = "GraphGameFlow/Game Flow Graph", fileName = "NewGameFlowGraph")]
public class GameFlowGraph : BaseGraph { }

// LoadSceneAction (slice 1) gains:
[CreateAssetMenu(menuName = "GraphGameFlow/Actions/Load Scene", fileName = "NewLoadSceneAction")]
public sealed class LoadSceneAction : BaseAction { /* unchanged behavior */ }
```

**Contract**: `GameFlowGraph` is a `BaseGraph` (assignable to `GraphFlowDriver` unchanged). Both types are
creatable from **Assets ▸ Create ▸ GraphGameFlow ▸ …**.

## Editor — Create menu

| Menu path | Result |
|-----------|--------|
| Assets ▸ Create ▸ GraphGameFlow ▸ Game Flow Graph | a `GameFlowGraph` asset |
| Assets ▸ Create ▸ GraphGameFlow ▸ Actions ▸ Load Scene | a `LoadSceneAction` asset |
| Faolline ▸ Open GraphGameFlow Editor | opens the editor window |
| Faolline ▸ GraphGameFlow ▸ Create Reference Scene-Flow Sample | generates the runnable sample asset |

## Editor — window behavior

- Double-clicking a `GameFlowGraph` asset opens/focuses the gameflow editor window showing it.
- Right-click the canvas → **Add Start / Statement / Choice / SubGraph / End Node** at the cursor; drag to
  connect ports; the first Start added becomes the graph's entry node.
- **Save** (toolbar / Ctrl+S) persists nodes/edges to the asset; **Validate** runs `GraphValidator`.
- Selecting a node shows the inspector: **Node Properties** (title, checkpoint, color, entry conditions,
  on-enter / on-exit actions — drop a `LoadSceneAction` here), a **Flow** foldout (await-signal name, wait
  duration), and End / SubGraph / Choice sections per node type.

## Sample contract

The sample is a `GameFlowGraph`: `start → [enter: Load Scene "A"] → await "advance" → [enter: Load Scene "B"]
→ end`, the two Load Scene actions attached as sub-assets. Driven by a `GraphFlowDriver` it walks
A → (park) → B → end on `RaiseSignal("advance")`.

## Semver / compatibility

- gameflow **0.1.0 → 0.2.0** (MINOR, additive): a new creatable asset type + an editor + a Create attribute.
- The slice-1 runtime API is unchanged and source-compatible. graphcore/graphstandard untouched; the 654
  EditMode + 8 PlayMode tests stay green.
