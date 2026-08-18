# TODO — deferred design work

Larger design questions parked here on purpose: they change core semantics and need **real usage by
several consumers** before a direction is locked in. Not bugs, not small adds — do not "just implement" these
without that feedback.

---

## Signal scoping (contexts / channels)

**Status:** deferred — needs multi-user feedback before choosing a model.
**Origin:** Cryptique 4-region dogfood (2026-06-20), finding #5.

### The observation

Signals (`BaseContext.RaiseSignal` / `OnSignal` / `HasSignalBeenRaised`) are **global to the context**. There
is no scoping by graph, by sub-graph instance, or by channel. For a single flow this is fine and simple. But:

- Two sub-graphs (or two instances of the same sub-graph) that raise/await the **same signal name** hear each
  other — there is no isolation.
- A signal raised deep in one branch is visible to every subscriber on the same context, whatever its intent.

### What already mitigates it (do NOT mistake for a solution)

- `SubGraphNodeData.InheritParentContext` / `OpensScope` already give **coarse** isolation: a fresh-context
  sub-graph does not share signals with its parent at all (that's a boundary, not a scope). The
  `GraphValidator` even warns when that boundary would deadlock an await (graphcore 0.24.0).
- `RaiseSignal<T>(name, payload)` carries a typed payload — an embryo of a "channel", but not addressing.
- **`SignalPayloadMatchesCondition` (graphcore 0.36.0)** — a `BaseCondition` for
  `BaseNodeData.ResumeConditions` that gates a resume on the raised signal's last string payload matching
  an expected value, so a homonymous raise meant for a different instance is ignored (the node "stays
  parked and re-armable", per `BaseRunner.ResumeConditions` semantics) instead of falsely resuming. Also
  handles a node awaiting MORE than one signal name (e.g. a completion signal plus a failure signal on
  `AwaitSignalNamesExtra`): put one instance per awaited name — each implements
  `IResumeSignalAwareCondition` and abstains (passes) on any raised name that isn't its own `Signal`, so
  `BaseRunner`'s AND-across-`ResumeConditions` composes them as the intended OR instead of one condition
  vetoing a resume it had no opinion on. `MatchMode` (`Exact`/`StartsWith`) covers payload formats like
  the `"{sceneName}: {reason}"` failure signals from `AsyncSceneLoader`/`AddressablesSceneLoader`.

None of these give *fine-grained* scoping (per-instance / named channel within a shared context) as a
general mechanism — they are targeted workarounds for the payload-carrying case, not a scoping model.

### Confirmed real-world case (2026-07-23, consumer report)

A concrete repro was worked through for the proximity-streaming pattern (world cut into tiles, additive
loaded/unloaded by player proximity — see this project's own streaming guide, "Streaming par proximité"):
several tile/zone flows, each its own `GraphFlowDriver`/`BaseRunner` instance, sharing one `GameFlowContext`
(the normal DI setup so every tile can still read shared state — inventory, quest progress). All tiles go
through one shared `AsyncSceneLoader` (needed for its FIFO queue and "preload in the direction of travel"
behavior), which exposes exactly one `LoadCompletedSignal` per instance. Two tiles parked on that same
signal name — tile B resumes the instant tile A's unrelated load completes, believing its own scene is
ready when it hasn't even started loading. Verified against the actual code, not just theorized:
`BaseRunner.ResumeIfAwaiting` (`Contains(node.AwaitSignalNames, name)`) matches on name only, and
`BaseContext.OnSignal` subscriptions are context-scoped, so every runner sharing that context receives the
broadcast regardless of which runner's `RaiseSignal` triggered it.

This is exactly the case the "≥2 independent consumers" bar in this file was waiting for — it is not a
contrived demo, and the two documented mitigations (`OpensScope`, name-namespacing by convention) both
fail it specifically: `OpensScope` would cut the tile off from the shared state it needs to read, and
namespacing by convention doesn't work when tile names are assigned procedurally at runtime, not authored
up front. `SignalPayloadMatchesCondition` (above) closes this specific case without committing to a
scoping model — it's a point fix, not evidence the deferred design work above is no longer needed.

### Theoretical stress test (2026-07-23): this was actually two problems

Before writing more code, the three candidate models below were stress-tested on paper against the
confirmed case above and the ecosystem's other nesting patterns. Result: **"signal scoping" was hiding
two distinct problems** that the original "two sub-graphs (or two instances of the same sub-graph)"
phrasing conflated without saying so.

| | Sibling-runner (the confirmed case above) | Re-entrant/nested sub-graph |
|---|---|---|
| Shape | two **independent** `BaseRunner` instances, no execution-tree relationship, sharing one `BaseContext` by DI choice | **one** runner's `_graphStack`, the same or a related sub-graph reached twice (nesting or replay) |
| Who knows the difference | only the caller (game code) — "this is tile Nord" is not derivable from graph structure | the runtime itself — parent/child is right there in `_graphStack` |
| Which candidate model can even apply | **(b) explicit channel only.** (a) implicit instance-scope and (c) hierarchical fall-through are BOTH defined over the graph execution tree — two sibling runners have no tree relationship for either to hook into. Scoping "by instance" here requires information only the caller has, which is (b) by another name. | (a)/(c) — natural extension of the existing `OpensScope` local-context overlay, which is already exactly "child gets its own scope, reads through to parent" |
| Status | **Resolved** (`SignalPayloadMatchesCondition` + `IResumeSignalAwareCondition`, 0.36.0) — and will PERMANENTLY need an explicit mechanism no matter what scoping model (if any) is ever built for the other problem | **Open, deliberately not fixed yet** — see below |

The re-entrant/nested case turned out to already have a live, undocumented bug to point at:
`BaseContext`'s local-context overlay (`OpensScope`) is a **flat overlay, not a stack** —
`BeginLocalContext` silently discards an already-open local context if a second `OpensScope` sub-graph
is reached while the first is still open (same context object, via `OpensScope`/`InheritParentContext`
hops). Zero test coverage, zero validator check, zero dogfood report before this session — confirmed
genuinely unhit in practice so far.

**Decision: don't build the real fix (a proper scope stack) now.** The ecosystem is headed toward a
non-linear/parallel execution engine (a Behavior Tree, per the execution-paradigms direction) whose
scoping needs are structurally different from this narrow case — real concurrent branches (a `Parallel`
composite) need a scope **tree**, not a LIFO stack, and BT memory is typically per-node, not
per-subtree like `OpensScope`. Fixing the flat overlay now, shaped only around Linear's rare nested-
`OpensScope` case, risks freezing something a BT engine would have to redesign anyway — the exact
"commits to a model expensive to change later" trap this file already warns about for signal scoping
itself. **Shipped instead (graphcore 0.36.1), cheap and non-committal:** `GraphValidator` now warns at
authoring time when a graph nests `OpensScope` sub-graphs (through any depth of
`InheritParentContext` hops), and the gap is documented directly on
`SubGraphNodeData.OpensScope`. The real scope-stack design is parked until a non-linear engine is
actually being designed — then design it ONCE as shared substrate (covering both that engine's needs
and a retrofit of `OpensScope`), not twice.

### Why the remaining (Signal-scoping-proper) piece is still deferred

The sibling-runner problem is closed for good (payload+condition is the permanent answer regardless of
scoping). What's left un-decided is only whether SIGNALS (not variables) should ever gain a scoping
mechanism at all for the nested/re-entrant case — and per the table above, that would piggyback on
whatever scope-stack substrate eventually gets built for a non-linear engine, not be designed standalone
for signals today. Committing to a model now (graph-scoped? explicit named channels? hierarchical
fall-through?) before that substrate exists freezes semantics that are expensive to change later.

### What we need before building the real scope-stack substrate

- A non-linear/parallel execution engine (Behavior Tree or similar) actually in design, so the stack's
  shape (tree vs. LIFO, per-node vs. per-subtree memory) is derived from real requirements instead of
  guessed from Linear's one rare, currently-unused nested-`OpensScope` case.
- At that point, re-evaluate whether Signals should plug into it too (they currently don't participate
  in the local-context overlay at all — a deliberate design choice, not an oversight, since signals are
  transient/global by design today).

### Non-goals for now

- No API surface added speculatively.
- Consumers that need per-instance signal disambiguation today use `SignalPayloadMatchesCondition`
  (permanent, not a stopgap, for the sibling-runner case) or the existing `InheritParentContext` /
  `OpensScope` boundary (for the nested-runner case, now with a validator warning against the one way
  it currently breaks).

---

## Mixed scene-loader flows (Build Settings + Addressables in one flow)

**Status:** closed — not a real gap. Dropdown ergonomics SHIPPED (graphgameflow 0.16.0).
**Origin:** design discussion, 2026-07-24.

### The observation

`GameFlowContext.SceneLoader` is a single field (`GameFlowContext.cs:20`) — one active `ISceneLoader` for the
whole context. `LoadSceneAction.Execute` and `UnloadSceneAction` both resolve it fresh from the running
context (`LoadSceneAction.cs:47`), so every scene action in a flow is interpreted the same way: all
Build-Settings scene names, or all Addressable keys, never a per-action choice. There's no way today to have
one `LoadSceneAction` in a flow resolve as a Build-Settings name while another resolves as an Addressable key.

### Why this stopped mattering (user, 2026-07-24)

The one place a real project actually needs both loading mechanisms at once is the mandatory single
Addressables bootstrap scene (Addressables requires one Build-Settings-resolved scene to initialize itself
before any Addressable content can load). But that boot scene is never a `LoadSceneAction`/`UnloadSceneAction`
target in the first place — it's the scene that starts everything (including the `GraphFlowDriver` itself),
not one this ecosystem's own graphs load or unload. So the "mixed flow" case this section was written for
doesn't actually occur in practice: a project either drives its whole flow off Build Settings, or off
Addressables past the boot scene — never both from inside the same graph. Per-action/hybrid loader
resolution is therefore **not just deferred, it's a non-problem** — closing this instead of leaving it parked.

### What already works around it (if a real case ever shows up)

Two separate `GraphFlowDriver`/`GameFlowContext` instances — one per sub-system (e.g. core scenes on Build
Settings, downloadable chapters on Addressables) — each with its own loader. This is two flows, not a mix
within one flow, but it's the already-working path and needs zero lib changes.

### What shipped instead (ergonomics, not the mix)

`SceneNameFieldDrawer` (graphgameflow 0.16.0): a "Build Settings / Addressable" toolbar on both
`LoadSceneAction`/`UnloadSceneAction` inspectors. In Addressable mode, a dropdown lists registered
Addressables-group scene addresses, plus a "Mark as Addressable" button (mirrors "Add to Build Settings") to
promote a plain project scene to an Addressable entry in one click. Gated behind a `FAOLLINE_ADDRESSABLES`
Version Define so the core package adds no hard dependency when Addressables isn't installed.

### Non-goals

- No per-action or hybrid-fallback loader resolution — closed as a non-problem, not deferred.
- No change to `GameFlowContext.SceneLoader` single-field model.

---

## Known bugs (confirmed, not design questions — just not yet fixed)

Unlike the sections above, these are plain bugs with an obvious fix; they're parked here only because
they haven't been prioritized yet, not because they need consumer feedback.

### `GameFlowNodeInspectorView` never shows registered graph-inspector extensions

**Status:** resolved, 2026-08-18 (graphgameflow 0.18.1) — local `SetGraph`/`_graph`/`_serializedGraph`
dropped entirely; the class now uses the inherited base `Graph`/`SerializedGraph` directly, confirmed in
current source.
**Origin:** manual doc review session, 2026-08-17 — noticed the "Category Groups" foldout
(`GraphCategoryGroupInspectorExtension`, see `EXTENSIBILITY.md`) never appears on a gameflow graph's
no-selection panel, traced to the real cause below.

`com.faolline.graphgameflow/Editor/Inspector/GameFlowNodeInspectorView.cs` declares its own
`public void SetGraph(BaseGraph graph)` (sets a private `_graph` field) that **hides** — does not
override, never calls `base.SetGraph(graph)` — `BaseNodeInspectorView.SetGraph`. Since
`GameFlowGraphEditorWindow` holds `_inspector` typed as the concrete `GameFlowNodeInspectorView` (not
`BaseNodeInspectorView`) and calls `_inspector?.SetGraph(graph)`, this always resolves to the hiding
method — the base class's protected `Graph` property is never set, stays null forever for any gameflow
graph. `BuildNoSelectionContent()`'s `if (Graph != null)` guard therefore always fails silently, so
**no `GraphSectionDelegate` registered via `InspectorExtensionRegistry.RegisterGraphSection` ever
renders for a gameflow graph** — not just `GraphCategoryGroupInspectorExtension`, any such extension,
present or future.

Verified isolated to graphgameflow: `graphdialoguesystem`, `graphquest`, `graphTest`, and
`starterGraph`'s node-inspector views all rely on the inherited base `SetGraph`/`Graph` correctly — no
local shadowing — so their no-selection panels work as designed.

**Fix**: have `GameFlowNodeInspectorView.SetGraph` call `base.SetGraph(graph)` too (or drop the local
override entirely if `_graph`/`_serializedGraph` can be derived from the base `Graph` property instead).

### `graphcore/README.md` documents a `BaseGraph` creation menu that doesn't exist

**Status:** resolved, 2026-08-18 (graphcore 0.43.1) — README's Data Layer section corrected, confirmed in
current text: no more false menu-path claim.
**Origin:** manual doc review session, 2026-08-17.

README's Data Layer section says "Create via `Assets > Create > GraphCore > Base Graph`." Checked
`Runtime/Graph/BaseGraph.cs`: there is no `[CreateAssetMenu]` on the class at all — its own comment says
why: `// No [CreateAssetMenu] — consumers create typed graphs (DialogueGraph, GameFlowGraph, etc.), not
raw BaseGraph.` So the menu path the README describes doesn't exist and was presumably removed
deliberately at some point without the README being updated. The user's instinct ("prevent creating a
raw BaseGraph, no use for it") is already the actual shipped behavior — just the doc is stale.

**Fix**: correct the README's Data Layer section — drop the false menu-path claim, note instead that
`BaseGraph` is meant to be subclassed per domain (as every T2 vertical already does) and constructed via
`ScriptableObject.CreateInstance<T>()` on that subclass, not created raw from a menu.

---

## Future ideas (unscoped — flesh out before acting)

### `graphlogging` — due for a future upgrade

**Status:** open, no specifics yet.
**Origin:** doc review session, 2026-08-17 — flagged in passing while reviewing the package's README,
no concrete gap identified at the time.

Noted as a placeholder only — revisit and fill in what the upgrade should actually cover before treating
this as actionable.

### `BaseEdgeData.Condition` — redondant/mort dans tous les éditeurs shippés

**Statut :** résolu, 2026-08-18 — verdict inverse de la première passe (voir historique ci-dessous) ;
`BaseEdgeData.Condition` n'apporte rien d'utilisable sur un graphe Linear aujourd'hui. Décision : pas de
suppression du champ (romprait `graphstandard`/Flow, réel consommateur, + la sérialisation d'assets
existants), pas de flag d'opt-in par lib non plus (sur-ingénierie pour un périmètre d'un seul champ / un
seul vrai consommateur / un seul endroit d'UI concerné) — juste corriger le point de friction réel :
l'UI de `graphdialoguesystem` induisait en erreur sur ce que fait le champ. Fixé la même session :
`DialogueNodeInspectorView.BindEdge` (`Editor/Inspector/DialogueNodeInspectorView.cs:67-92`) porte
maintenant un tooltip + une note explicite ("No alternate routing") clarifiant qu'une condition d'edge
qui échoue sur un graphe dialogue bloque comme une `EntryCondition` sur le node cible, sans redirection.
**Origine :** session de revue doc, 2026-08-17 — flag initial vague en lisant `BaseEdgeData` dans le
README de graphcore. Creusé en session du 2026-08-18 suite à une question utilisateur sur la redondance
avec les conditions de nœud/choix ; l'utilisateur a contesté à raison une première réponse trop rapide
(voir ci-dessous), ce qui a mené à relire `BaseRunner.SelectEdge` et les ports des node views.

**Verdict, vérifié dans le code :**

1. **Tous les nodes réguliers ont un output `Port.Capacity.Single`.** Vérifié sur Start/Statement/
   SubGraph dans graphgameflow, graphdialoguesystem, graphTest ET starterGraph — jamais plus d'une edge
   sortante possible à l'auteurage. Donc la boucle de `BaseRunner.SelectEdge`
   (`Runtime/Execution/BaseRunner.cs:844-859`, "parcourt les edges dans l'ordre, prend la première qui
   passe") ne voit jamais qu'une seule edge candidate dans un graphe réellement dessiné dans un éditeur.
   Sur cette edge unique, condition qui échoue → `OnStuck`, exactement le même effet qu'une
   `EntryCondition` qui échoue sur le node cible (juste vérifié à un point différent du pipeline : sortie
   du node source, après ses `OnExitActions`, plutôt qu'entrée du node cible). **Redondant, pas un
   mécanisme distinct.**
2. **Sur une edge de choix, `edge.Condition` n'est jamais lu.** `SelectEdge` a deux branches : avec
   `forcedId` (le chemin de `ChooseById`, donc TOUT node Choice) elle matche sur `edge.Id`/`PortName` et
   retourne l'edge sans jamais regarder `edge.Condition` (`BaseRunner.cs:846-850`). C'est
   `BaseChoice.Condition` qui fait tout le travail de filtrage là — `BaseEdgeData.Condition` est du code
   mort sur ces edges-là.
3. **`BaseChoice.Condition` couvre déjà le "si X alors ici, sinon là"** — liste ordonnée de choix, chacun
   sa condition ; rien n'oblige le code appelant à attendre un joueur, `ChooseById` peut être invoqué
   automatiquement sur le premier choix disponible. La distinction "manuel (choix) vs automatique (edge)"
   de la première passe était une convention d'usage, pas une contrainte imposée par le runtime.
4. **La seule vraie utilisation distincte du champ dans tout le repo** est
   `graphstandard/Runtime/Flow/FlowRunner.cs:219` (`if (edge.Condition != null &&
   !edge.Condition.Evaluate(_context)) continue;`) — mais sémantique fork/join (livre un token sur
   *toutes* les edges qui passent, pas premier-qui-passe exclusif), et ce package est code-first, sans
   éditeur visuel du tout (cf. [[authoring-is-code-first]]).

**Historique de la clarification (pour ne pas relire tout le fil) :** une première réponse affirmait à
tort (a) que `graphcore` n'avait aucun inspecteur d'edge du tout — faux, `graphdialoguesystem` en a un
fonctionnel et testé (`DialogueNodeInspectorView.BindEdge`, `FR-021`) — puis (b) que edge-Condition et
choix/entry-conditions étaient trois mécanismes non-redondants avec un tableau à l'appui — l'utilisateur
a contesté ce tableau sur les deux points (choix = peut aussi faire du if/else automatique ; nodes
réguliers = jamais plus d'une sortie de toute façon), ce qui a mené à la relecture de `SelectEdge` et des
ports ci-dessus, confirmant que le tableau était faux.

**Non-actionable** — pas de suppression du champ (romprait la sérialisation de graphes existants et le
fork/join de graphstandard qui, lui, l'utilise réellement), mais aucun travail d'outillage éditeur à
prévoir dessus : rien à généraliser puisqu'il n'apporte rien sur les nodes réguliers/choix.

### `graphgameflow` naming collides with the "Flow" engine — but has nothing to do with it

**Status:** resolved, 2026-08-18.
**Origin:** doc review session, 2026-08-17 — user question while reading graphstandard's README engine
table ("if this table is right, isn't graphgameflow badly named?").

Verified: `com.faolline.graphgameflow/package.json` does **not** depend on `com.faolline.graphstandard`
at all (only `graphcore`, `graphsave`, `graphlogging`) — it structurally cannot use `FlowRunner`. Grepped
the whole package: zero `.cs` references to `FlowRunner`/`ReactiveEvaluator`; the only two hits are prose
mentions in `README.md`/`CHANGELOG.md` describing an *optional consumer-side integration pattern*
(composing a `ReactiveEvaluator`/`FlowRunner` on the same `GameFlowContext` from your own game code), not
graphgameflow using them itself. `GraphFlowDriver.cs` confirms it's built entirely on graphcore's
**Linear** `BaseRunner`. This matches graphstandard's own README table, which lists "scene-flow" under
the **Linear** row, not Flow.

Net: "GameFlow" and the graphstandard "Flow" engine (`FlowRunner`) are an unrelated name collision — pure
coincidence, not a design relationship. A full rename was assessed and rejected: `com.faolline.graphgameflow`
is the actual UPM package id (renaming breaks every consumer's manifest/git-URL pin), a satellite package
(`com.faolline.graphgameflow.addressables`) embeds the name in its own id, ~150 files reference the string
across 8 asmdefs/namespace usage/docs, and the package's own central type is already called
`GraphFlowDriver` — renaming the package alone wouldn't even remove the "Flow" word from its most visible
API, so a real fix would also mean a breaking type rename. **Fix shipped instead:** an explicit
disambiguation note added to both READMEs (graphgameflow's and graphstandard's) so a reader doesn't draw
the same reasonable-but-wrong inference this session did — no rename, no breaking change.

### Looping game-shell pattern (graphstandard README) — revisit

**Status:** resolved, 2026-08-18 — held up under audit, one doc gap fixed.
**Origin:** doc review session, 2026-08-17 — user flagged this section for a closer look, no specifics
given yet on what needs reconsidering.

The section describes modeling a menu→play→win→menu loop as a cyclic Linear graph with no End node
(runner loops forever, no `OnEnded`, small `HistoryDepth` recommended since `GoBack` across the loop
isn't meaningful). Audited against `graphcore`'s `BaseRunner.cs`: the "never ends, no `OnEnded`" claim is
exact (`OnEnded` only fires on reaching an `EndNodeData`), and history is genuinely bounded (auto-trims
oldest entry at `HistoryDepth`, default 20) — no memory-leak risk on an infinite loop. One undocumented
nuance found: once `GoBack`/`GoBackToCheckpoint` hits the `HistoryDepth` boundary, `BaseRunner` warns
**once per run** then silently no-ops on further out-of-range calls — a player spamming "back" past that
point gets no further signal. Added a note about this to both READMEs (graphstandard's and
graphgameflow's) next to the looping-shell guidance; no code change needed.

### Revisit the three execution engines (Linear / Reactive / Flow) together

**Status:** resolved, 2026-08-18 — refresher happened in conversation, folded in the two items above.
**Origin:** doc review session, 2026-08-17 — user wants a dedicated pass to get re-familiarized with how
the three engines (`BaseRunner`/Linear in graphcore, `ReactiveEvaluator`/Reactive and `FlowRunner`/Flow in
graphstandard) relate, likely folding in the two items directly above.
