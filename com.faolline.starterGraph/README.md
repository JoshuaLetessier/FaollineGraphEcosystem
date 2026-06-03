# com.faolline.starterGraph

**Internal verification package — not for distribution.**

A concrete, minimal subclass of `com.faolline.graphcore` used to exercise the full editor and runtime
surface (a reference "starter" graph implementation + sample assets). It validates that graphcore can
be specialized by a downstream lib.

It is **not** part of the consumable ecosystem: it is intentionally absent from the module selector
whitelist (`com.faolline.graphcore/Editor/GraphEcosystemModules.json`) and nothing depends on it, so a
consumer project never receives it. Kept in the repo for development and CI only.

Depends on: `com.faolline.graphcore`.
