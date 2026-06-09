# Quickstart — authoring a gameflow in the editor

## Fastest path: the sample

1. **Faolline ▸ GraphGameFlow ▸ Create Reference Scene-Flow Sample** — generates a ready-to-run gameflow
   graph asset (start → load A → await "advance" → load B → end) and pings it.
2. Add a **GraphFlowDriver** to a GameObject, assign the generated graph, press **Play**.
3. Scene A loads; the flow waits; call `RaiseSignal("advance")` (from a button/script) → scene B loads.

## Authoring from scratch

1. **Assets ▸ Create ▸ GraphGameFlow ▸ Game Flow Graph** → a `GameFlowGraph` asset.
2. Double-click it → the gameflow editor window opens.
3. Right-click the canvas → **Add Start / Statement / End Node**; drag from output to input ports to connect.
   The first Start becomes the entry node automatically.
4. Select a node → in the inspector:
   - **Node Properties ▸ On Enter Actions**: click `+`, drop in a **Load Scene** action asset
     (create one via Assets ▸ Create ▸ GraphGameFlow ▸ Actions ▸ Load Scene, set its Scene Name + Mode).
   - **Flow ▸ Await Signal**: type a signal name (e.g. `advance`) to make the node wait until the scene
     raises it; **Wait Duration** for a timed hold.
5. **Save** (Ctrl+S). Assign the graph to a `GraphFlowDriver` and press Play.

## What you can author

- The universal node set: **Start, Statement, Choice, SubGraph, End**.
- Per node: title, checkpoint, entry conditions, on-enter / on-exit **actions** (scene loads and any other
  `BaseAction`), an **await-signal** name, a **wait duration**.
- Scene change is always an **action** on a node, never a node type.

## Notes

- The editor reuses graphcore's canvas (copy/paste with new GUIDs, groups, validation) — it behaves like the
  StarterGraph editor.
- Running a flow is the `GraphFlowDriver` in Play (there is no in-editor runner in this slice).
