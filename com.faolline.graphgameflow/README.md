# com.faolline.graphgameflow

**Version**: 0.1.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` 0.6.0

The **orchestrator / host layer** of the Faolline graph ecosystem. graphcore and graphstandard are strictly
**headless** (no `MonoBehaviour`, no scene knowledge); graphgameflow is the adapter that **runs** those graphs
inside a live Unity scene. It is the one ecosystem layer where Unity-specific vocabulary
(`MonoBehaviour`, `SceneManager`, the per-frame tick, graph assets) is intentionally allowed — that binding is
its reason to exist.

> Slice 1 (this version): the host bridge (`GraphFlowDriver`) + the first standard scene action
> (`LoadSceneAction`) + a Linear scene-flow. README body filled at finalize (task T018).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
