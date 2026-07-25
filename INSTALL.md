# Installing the Faolline Graph Ecosystem

The ecosystem ships as separate UPM packages. A consuming project only ever gets the packages it
installs (plus their declared dependencies) — the sample/verification packages
(`com.faolline.starterGraph`, `com.faolline.graphTest`), the reference package
(`com.faolline.dialoguesystem~`), and the dev folders (`specs/`, `.specify/`, `memory/`, `.claude/`)
are never pulled into a consumer project.

## Recommended: the module selector

1. Install the base package once — **Package Manager ▸ + ▸ Add package from git URL**:
   ```
   https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphcore#master
   ```
   For a reproducible install (recommended once you're building against the ecosystem rather than just
   trying it), use a tag instead of `#master` — e.g. `...?path=com.faolline.graphcore#v1.3.0`.
2. Open **Window ▸ Faolline ▸ Graph Ecosystem Modules**.
3. Tick the packages you want (e.g. *Graph Dialogue System*) and click **Apply**. Dependencies are
   added automatically; UPM rewrites your `manifest.json` for you. Un-tick + Apply to remove.

The offered list is the whitelist in
`com.faolline.graphcore/Editor/GraphEcosystemModules.json` — samples/tests are deliberately not listed.
Every module the selector adds is pinned to whatever ref `com.faolline.graphcore` was itself actually
installed at (read back from its resolved package id) — so once step 1 uses a tag, every module ticked in
step 3 follows the same tag automatically, not the `master` this file shows as the bootstrap default. The
window's status line names the ref it's about to use before you click Apply.

## Manual alternative (no selector)

Add the git URLs you need directly (UPM does **not** auto-resolve git dependencies, so add every package
you need, dependencies included):

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphcore#master
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphlocalization#master
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphdialoguesystem#master
```

Pin `#master` to a tag (e.g. `#v0.1.0`) for reproducible installs.

## Package dependency graph

```
com.faolline.graphcore             (no deps)               — nodes, edges, runner, context
com.faolline.graphlocalization     (no deps)               — locale tables, CSV/Unity provider
com.faolline.graphstandard         → graphcore              — reactive/flow engines, collections
com.faolline.graphdialoguesystem   → graphcore, graphlocalization — dialogue graph, speakers, UI
com.faolline.graphgameflow         → graphcore, graphsave   — scene driver, triggers, scene loader
com.faolline.graphgameflow.addressables → graphgameflow, com.unity.addressables — Addressables scene loader bridge
com.faolline.graphquest            → graphcore, graphstandard, graphlocalization — quest DAG, journal
com.faolline.graphsave             → graphcore              — snapshot persistence (neutral)
com.faolline.graphsave.savesystem  → graphsave, savesystem.core — UnitySaveSystem bridge
```

Internal / not distributed:

```
com.faolline.starterGraph          → graphcore              — minimal template (fork this)
com.faolline.graphTest             → graphcore, graphstandard — integration tests
```

## Which package do I need?

| I want to…                                    | Install                                     |
|------------------------------------------------|----------------------------------------------|
| Build and run any graph (nodes, edges, runner) | `graphcore`                                  |
| Localize node text / speaker names             | `graphlocalization`                          |
| Write dialogue trees with speakers & choices   | `graphdialoguesystem` (pulls graphcore + loc) |
| Drive a game flow from a scene (driver, triggers) | `graphgameflow`                           |
| Load scenes by Addressable key instead of Build Settings | `graphgameflow.addressables` (needs `com.unity.addressables`) |
| Use reactive (k-of-N) or flow (fork/join) engines | `graphstandard`                           |
| Model quests as objective DAGs with rewards    | `graphquest` (pulls standard + loc)          |
| Save / restore a running graph                 | `graphsave` (+ `graphsave.savesystem` for UnitySaveSystem) |

## Authoring paths

The ecosystem supports three ways to author graphs:

1. **Code-first (builders)** — construct graphs programmatically with the fluent `GraphNodeBuilder` /
   `QuestBuilder` / dialogue builder APIs. Best for tests, procedural content, and CI pipelines.
2. **Asset editor (visual)** — create graph assets via the graph editor windows
   (**Window ▸ Faolline ▸ …**). Best for designers and narrative content.
3. **Hybrid** — create a graph asset in the editor, then manipulate it from code at runtime
   (e.g. inject nodes, read parameters). Works naturally — assets are ScriptableObjects.
