# Faolline Graph Ecosystem

A modular graph-based toolkit for Unity: a headless data layer + execution runtime
(`com.faolline.graphcore`), and a set of domain packages built on top — dialogue, quests, game-flow
orchestration, save/restore, localization — all sharing the same three context primitives.

**Status**: private, unpublished, actively developed. Not on OpenUPM; install via git URL (below).

---

## Install

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphcore#master
```

Then open **Window ▸ Faolline ▸ Graph Ecosystem Modules** to add the other packages with one click. See
[`INSTALL.md`](INSTALL.md) for the full guide (module selector, manual URLs, dependency graph, "which
package do I need").

---

## Packages

| Package | Version | What it is |
|---|---|---|
| [`com.faolline.graphcore`](com.faolline.graphcore/) | 0.35.0 | Base package — nodes, edges, runner, and the three context primitives (Variables / Signals / Collections) |
| [`com.faolline.graphlocalization`](com.faolline.graphlocalization/) | 0.7.0 | Locale tables + CSV/Unity localization providers (no dependencies) |
| [`com.faolline.graphstandard`](com.faolline.graphstandard/) | 0.17.0 | Reactive (k-of-N) and Flow (fork/join) execution engines, domain-neutral standard nodes |
| [`com.faolline.graphdialoguesystem`](com.faolline.graphdialoguesystem/) | 0.17.0 | Dialogue graphs — speakers, branching choices, localized text, headless playback |
| [`com.faolline.graphgameflow`](com.faolline.graphgameflow/) | 0.10.0 | The `MonoBehaviour`/scene adapter — drives a graph live in a Unity scene |
| [`com.faolline.graphquest`](com.faolline.graphquest/) | 0.11.0 | Quests as objective DAGs, prerequisite gating, one-shot rewards |
| [`com.faolline.graphsave`](com.faolline.graphsave/) | 0.7.0 | Neutral run-snapshot model + a pluggable save-store contract |
| [`com.faolline.graphsave.savesystem`](com.faolline.graphsave.savesystem/) | 0.1.3 | Bridges graphsave to `com.faolline.savesystem.core` (UnitySaveSystem) |

Each package has its own `README.md` (usage + API) and `CHANGELOG.md` (version history).

Internal-only (never pulled into a consumer project — see [`INSTALL.md`](INSTALL.md)):
[`com.faolline.starterGraph`](com.faolline.starterGraph/) (template to fork for a new domain package) and
[`com.faolline.graphTest`](com.faolline.graphTest/) (graphcore integration test suite).

---

## The three context primitives

Everything a running graph reads or writes is one of three sharply distinct primitives — read
[`com.faolline.graphcore`'s README](com.faolline.graphcore/README.md#the-three-context-primitives) for the
full comparison, but in short:

- **Variable** — a durable, typed value that changes over time (hp, score, a flag). Governed via a
  `VariableDef` asset or a raw string key.
- **Signal** — a transient wake-event + a durable "has this ever fired" latch. The **only** primitive that
  can resume a parked node. Governed via a `SignalDef` asset or a raw string.
- **Collection** — a durable named set of items, each with a quantity (inventory, visited rooms). Governed
  via `CollectionDef`/`CollectionEntry` assets or raw strings.

Mixing these up is the most common early mistake — a Variable write is *quiet* (nothing reacts unless
something subscribed), a Signal write can *wake a parked node*. The split is deliberate, not incidental.

---

## Authoring paths

1. **Code-first (builders)** — fluent `GraphNodeBuilder` / `QuestBuilder` / dialogue builder APIs. Best for
   tests, procedural content, CI.
2. **Asset editor (visual)** — build graph assets in the editor windows (**Window ▸ Faolline ▸ …**). Best
   for designers and narrative content.
3. **Hybrid** — author a graph asset, then manipulate it from code at runtime. Works naturally since every
   graph is a `ScriptableObject`.

---

## Repository layout

This repository is rooted at `Assets/FaollineGraphEcosystem` (a Unity project's asset folder) — each
`com.faolline.*` directory is an independent UPM package with its own `package.json`, `README.md`, and
`CHANGELOG.md`. `specs/`, `.specify/`, and `memory/` are development-only (spec-kit design docs and
project notes) and are never pulled into a consumer project.
