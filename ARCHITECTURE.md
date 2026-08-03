# Architecture — tiers & dependency rules

This document names the layering that already exists in the ecosystem so it can be enforced, reviewed
against, and taught. The vocabulary here is normative: new packages and new asmdef references must fit
one of these tiers, and the whole matrix is locked by an automated test (see
[Enforcement](#enforcement)).

## The tiers

```
        T0 · Foundation        graphcore.Runtime.Core   graphlocalization
                                   ▲
                               graphcore.Runtime
                                   ▲                    ▲
        T1 · Capabilities      graphstandard  graphsave │
                                   ▲             ▲      │
        T2 · Verticals         graphquest   graphgameflow   graphdialoguesystem
                                                                 ▲
        T3 · Adapters          graphsave.savesystem   gameflow.addressables   *.Localization.Unity   dialoguesystem.UI
                               (→ UnitySaveSystem)    (→ com.unity.addressables)  (→ Unity.Localization) (→ TMP/UGUI/Input)

        Dev tooling            graphTest · starterGraph        (never shipped to consumers)
```

`graphcore` is two Runtime assemblies, not one: `graphcore.Runtime.Core` is a `noEngineReferences` leaf (zero
UnityEngine usage, checkable by the compiler, not just convention) carrying the pure-C# run-state model
(`BaseContext`, `SignalArgs`, the extensible `BaseContextTypeRegistry`/`GraphLog` seams); `graphcore.Runtime`
is the Unity engine layer above it — nodes, actions, conditions, the runner, and every ScriptableObject
asset type (`BaseGraph`, `BaseAction`, `BaseCondition`, `SignalDef`, `VariableDef`, `CollectionDef`).
Everything downstream references both (see `DependencyMatrixTests.Allowed`); nothing outside graphcore
itself is expected to reference `Runtime.Core` alone today. This is a deliberately narrow slice — see
`clean-architecture-research` history for why the runner and the asset/authoring model were NOT pulled into
Core (their own coupling to ScriptableObject types runs too deep to be worth resolving for this project).

| Tier | Members | May depend on | Never |
|---|---|---|---|
| **T0 · Foundation** | `graphcore.Runtime.Core`, `graphcore.Runtime`, `graphlocalization` | `graphcore.Runtime` → `graphcore.Runtime.Core` only; the other two: nothing (zero asmdef references) | anything outside that |
| **T1 · Capabilities** | `graphstandard`, `graphsave` | T0 | each other, T2+ |
| **T2 · Verticals** | `graphdialoguesystem`, `graphquest`, `graphgameflow` | T0, T1 | **another vertical**, external packages |
| **T3 · Adapters** | `graphsave.savesystem`, `graphgameflow.addressables`; the `Localization.Unity` and `UI` sub-assemblies | the package they adapt + **one** external dependency family | other adapters |
| **Dev tooling** | `graphTest`, `starterGraph` | T0, T1 | being installed in a consumer project |

Tier definitions:

- **Foundation** — the substrate everyone shares. `graphcore.Runtime` owns nodes, edges, the runner, and
  the graph/def assets; its one dependency, `graphcore.Runtime.Core`, owns the run-state primitives
  (`BaseContext`, signals) and is engine-agnostic (`noEngineReferences: true`) — `graphlocalization` is
  likewise a dependency-free leaf (locale tables + provider contract). `graphcore.Runtime` and
  `graphlocalization` compile against `UnityEngine` core; `graphcore.Runtime.Core` does not compile
  against `UnityEngine` at all.
- **Capabilities** — domain-neutral services above the substrate: extra execution engines and standard
  nodes (`graphstandard`), the neutral run-snapshot model + `IGraphSaveStore` port (`graphsave`).
- **Verticals** — one gameplay domain each: dialogue, quests, scene-flow. A vertical composes T0/T1
  but knows nothing about its sibling verticals; cross-domain links happen in the **consumer project**
  (via shared context primitives — signals, variables, collections — never via a compile-time edge).
- **Adapters** — the only assemblies allowed to reference something outside the ecosystem
  (Unity optional packages, UnitySaveSystem, UGUI/TMP/InputSystem). An adapter exists at one of two
  granularities, both established patterns:
  - a **separate package** when the external dependency can't be assumed installed
    (`graphsave.savesystem` → UnitySaveSystem, `graphgameflow.addressables` → `com.unity.addressables`);
  - a **sub-assembly of the package** when it adapts an optional Unity package
    (`graphlocalization/Localization.Unity`, `graphdialoguesystem/UI`).

  An adapter package can itself split Runtime/Editor like any T0–T2 package (e.g.
  `graphgameflow.addressables.Editor` plugs Addressables scene AND graph entries into `graphgameflow`'s
  `LoadSceneAction`/`UnloadSceneAction`/`GraphKeyRegistryWindow` via opt-in registries the vertical's
  Editor assembly exposes — the vertical's own Editor assembly stays external-dependency-free, per
  rule 3). Since asmdef references are not transitive, `graphgameflow.addressables.Editor` also
  references `graphcore.Runtime`/`graphcore.Runtime.Core` directly wherever it needs a T0 type
  (e.g. `BaseGraph`) that `graphgameflow.Editor` merely re-exposes rather than owns.

## The rules

1. **Dependencies point downward only** (T3 → T2 → T1 → T0), enforced by the compiler through asmdef
   references — an illegal `using` is a compile error, not a review convention.
2. **Verticals never reference verticals.** If two domains must cooperate at runtime, they do it
   through the shared context primitives in the consumer's composition, not through a package edge.
3. **External dependencies live only in adapters.** A `Runtime` assembly of T0–T2 references nothing
   but other ecosystem assemblies. Wanting an external ref in a vertical is the signal that a port +
   adapter is missing.
4. **Runtime never references Editor.** Editor assemblies sit beside their Runtime, never under it.
5. **Ports live next to the model, adapters live apart.** The interface (`IGraphSaveStore`,
   `ILocalizationProvider`, `ISceneLoader`, `IDialoguePlaybackSource`) ships in the lib that defines
   the contract; concrete integrations ship in an adapter assembly the consumer opts into.
6. **Libs ship data + seams, never concrete visuals.** In-game UI is consumer territory; the `UI`
   adapter provides optional building blocks, not a mandatory presentation layer.

## Enforcement

Two layers:

- **The compiler** — asmdef `references` arrays are the dependency arrows; nothing outside them
  resolves.
- **The dependency-matrix test** —
  [`DependencyMatrixTests`](com.faolline.graphTest/Tests/EditMode/Architecture/DependencyMatrixTests.cs)
  (in `graphTest`, EditMode) re-reads every ecosystem asmdef and checks it against a declared
  allowlist of edges. It fails when an assembly appears that isn't in the matrix, when a matrix entry
  goes stale, or when any asmdef gains a reference outside its allowed set. Adding an edge is always a
  **conscious, reviewed act**: update the matrix in the test *and this document* in the same commit.

## Adding a new package — checklist

1. Fork `com.faolline.starterGraph` (see its README).
2. Pick the tier first; that decides the allowed `references` of the Runtime asmdef.
3. External dependency needed? Put it behind a port in your Runtime and adapt it in a `*.Something`
   sub-assembly or a sibling `.bridge` package — never in the Runtime itself.
4. Declare the new assemblies in `DependencyMatrixTests.Allowed` and add them to the diagram above.
5. Add the package to `INSTALL.md`'s dependency graph and (if consumer-installable) to the module
   selector whitelist (`com.faolline.graphcore/Editor/GraphEcosystemModules.json`).

## Relationship to Clean Architecture

The ecosystem is a set of libraries, not an application, so it doesn't reproduce the classic
Domain/Application/Infrastructure/Presentation folders — it implements the same *principles* at
package granularity: compiler-enforced dependency direction (rules 1–4), ports & adapters (rule 5),
and presentation kept out of the core (rule 6). For how a game built on Clean Architecture should
consume these packages, see [`INTEGRATION.md`](INTEGRATION.md). For how a downstream package injects
its own inspector UI into the graph editor without violating rule 2/3, see
[`EXTENSIBILITY.md`](EXTENSIBILITY.md).
