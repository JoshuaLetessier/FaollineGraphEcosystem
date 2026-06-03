# com.faolline.graphTest

**Internal verification package — not for distribution.**

A concrete verification package over `com.faolline.graphcore` that exercises the editor and runtime
surface (test/sample graph types and scaffolding). Used to keep graphcore honest as it evolves.

It is **not** part of the consumable ecosystem: it is intentionally absent from the module selector
whitelist (`com.faolline.graphcore/Editor/GraphEcosystemModules.json`) and nothing depends on it, so a
consumer project never receives it. Kept in the repo for development and CI only.

Depends on: `com.faolline.graphcore`.
