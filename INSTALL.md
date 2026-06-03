# Installing the Faolline Graph Ecosystem

The ecosystem ships as separate UPM packages. A consuming project only ever gets the packages it
installs (plus their declared dependencies) — the sample/verification packages
(`com.faolline.starterGraph`, `com.faolline.graphTest`), the reference package
(`com.faolline.dialoguesystem~`), and the dev folders (`specs/`, `.specify/`, `memory/`, `.claude/`)
are never pulled into a consumer project.

## Recommended: the module selector

1. Install the base package once — **Package Manager ▸ + ▸ Add package from git URL**:
   ```
   https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphcore#master
   ```
2. Open **Window ▸ Faolline ▸ Graph Ecosystem Modules**.
3. Tick the packages you want (e.g. *Graph Dialogue System*) and click **Apply**. Dependencies are
   added automatically; UPM rewrites your `manifest.json` for you. Un-tick + Apply to remove.

The offered list is the whitelist in
`com.faolline.graphcore/Editor/GraphEcosystemModules.json` — samples/tests are deliberately not listed.

## Manual alternative (no selector)

Add the git URLs you need directly (UPM does **not** auto-resolve git dependencies, so add every package
you need, dependencies included):

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphcore#master
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphlocalization#master
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=Assets/FaollineGraphEcosystem/com.faolline.graphdialoguesystem#master
```

Pin `#master` to a tag (e.g. `#v0.1.0`) for reproducible installs.

## Package dependency graph

```
com.faolline.graphcore            (no deps)
com.faolline.graphlocalization    (no deps)
com.faolline.graphdialoguesystem  → graphcore, graphlocalization
```
