# Consuming the ecosystem from a Clean Architecture project

This guide is for a game project structured as `Domain / Application / Infrastructure / Presentation /
DI` (asmdef-enforced layers, a DI container such as VContainer, no service locator) that wants to build
on the Faolline graph packages. It answers one question: **where do the libs sit in your layer diagram,
and how do you wire them?** For the ecosystem's own internal layering, see
[`ARCHITECTURE.md`](ARCHITECTURE.md); for installation, see [`INSTALL.md`](INSTALL.md).

## Two postures

**A — Strict:** the ecosystem is an Infrastructure detail. Your Domain never sees a `Faolline.*` type;
every lib service is hidden behind a gateway you define (`IQuestJournalGateway`, `IDialogueGateway`,
…). Maximum substitutability, maximum ceremony — you re-abstract a library that is already made of
abstractions.

**B — Pragmatic (recommended):** the graph assets *are* your game's flow/narrative domain language —
the same status you give your own ScriptableObjects. Lib services register directly in your
`LifetimeScope`; your Presenters subscribe to the libs' seams; you only write a gateway where you
genuinely foresee substitution. The libs are built for this: no singletons, no service locator,
constructor-injected plain classes, ports for everything scene- or IO-facing.

You can mix per area: posture B for flow/dialogue (deeply coupled to your content anyway), posture A
for persistence (where "swap the backend" is a real scenario — and the lib already gives you the port:
`IGraphSaveStore`).

**A note on `graphcore.Runtime.Core`:** since 0.38.0, `graphcore` is two Runtime assemblies — an
engine-agnostic `Runtime.Core` (`noEngineReferences`, the run-state model: `BaseContext`, signals) under
`Runtime` (nodes, actions, conditions, and every graph/def asset). Whether this changes anything for you
comes down to one question per use case: **does it touch an asset, or only the context primitives?**

Services that need a graph asset — `QuestEvaluator`, `DialoguePlayer`, anything taking a
`QuestGraph`/`DialogueGraph` — stay Unity-typed regardless of the split, so posture A's gateway ceremony is
exactly as necessary (or unnecessary) for them as before. But a use case that only reads/writes context
state — raises a signal, sets a variable, checks a collection, through the generated `GraphSignals`/
`GraphVariables` string constants — can now sit in a strictly `noEngineReferences` Application assembly
with a `BaseContext` constructor parameter and nothing to unflag. That's a real, new capability, not a
cosmetic one: it's genuinely unit-testable in plain C#, no domain reload, no Unity Test Framework needed
for that slice.

Two things worth keeping in mind if you lean on this:

- **`SignalDef`/`VariableDef`/`CollectionDef` are assets**, so a pure-Application use case necessarily works
  through the generated string constants, never the Def types themselves — which makes systematic constant
  generation (not ad-hoc string literals) load-bearing, not just tidy, for keeping that code pure.
- **The compiler guarantee is real; independence isn't.** Referencing `Runtime.Core` from Application is
  still taking a dependency on a 0.x library — `noEngineReferences` proves the assembly boundary, it
  doesn't make your Application layer free of the ecosystem. That's a legitimate trade, just not a free one.

See `ARCHITECTURE.md`'s Foundation tier for exactly what lives in each assembly.

## Layer mapping

| Your layer | What goes there |
|---|---|
| **Domain** | Your entities and rules. Under posture A: your gateway interfaces. Under posture B: may also hold quest/flow *identifiers* (generated `GraphSignals` / `GraphVariables` constants are plain C#) |
| **Application** | Your use cases. May orchestrate lib services (`QuestEvaluator`, `DialoguePlayer`) directly under posture B, or through your gateways under posture A |
| **Infrastructure** | Implementations of the ecosystem's ports: your `IGraphSaveStore` choice, a custom `ISceneLoader`, a custom `ILocalizationProvider` — plus your own gateways wrapping lib services |
| **Presentation** | Your Presenters/Views subscribing to lib seams: `IDialoguePlaybackSource` (lines/choices), `GraphFlowDriver` events (`OnNodeEntered`, `OnWaitingForSignal`, `OnEnded`…), quest state changes |
| **DI** | The `LifetimeScope` below — the only place that sees everything |

Graph **assets** (`DialogueGraph`, `QuestGraph`, flow graphs, `SignalDef`/`VariableDef`/
`CollectionDef`) are authored content, not code: reference them from serialized fields on your
`LifetimeScope` (or a content registry ScriptableObject) and hand them to the container as instances —
or, if the content is Addressable, see **Addressable content** below; the container still ends up with a
loaded instance, just not synchronously inside `Configure()`.

## Wiring with VContainer

The one real subtlety is **boot order**: `GraphFlowDriver.Boot()` creates the run's shared context
(`driver.Context` is null before it), and by default the driver boots itself in `Start()`
(`BootOnStart`). When the container owns the wiring, turn **Boot On Start off** in the inspector and
boot from an entry point, so everything that needs the live context is created after `Boot()`:

```csharp
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using Faolline.GraphGameFlow;
using Faolline.GraphLocalization;
using Faolline.GraphQuest;
using Faolline.GraphSave;
using Faolline.GraphSave.UnitySaveSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class GameLifetimeScope : LifetimeScope
{
    // Authored content — assigned in the inspector.
    [SerializeField] private QuestGraph  _mainQuest;
    [SerializeField] private TextAsset   _localizationCsv;

    protected override void Configure(IContainerBuilder builder)
    {
        // ── Infrastructure: adapters behind the ecosystem's ports ──
        builder.Register<ILocalizationProvider>(
            _ => new CsvLocalizationProvider(_localizationCsv.text, "fr"), Lifetime.Singleton);

        // Posture A example: persistence stays swappable behind the lib's own port.
        builder.Register<IGraphSaveStore>(
            _ => new SaveSystemGraphStore(new SaveSystem.SSJson.JsonSaveSystem<GraphRunSnapshot>()),
            Lifetime.Singleton);
        // (No UnitySaveSystem? graphsave ships JsonFileGraphSaveStore, or write your own.)

        // ── The scene driver: authored in the hierarchy, surfaced to the container ──
        builder.RegisterComponentInHierarchy<GraphFlowDriver>();

        // ── Content the wiring needs ──
        builder.RegisterInstance(_mainQuest);

        // ── Entry point: boots the flow, then builds the context-dependent services ──
        builder.RegisterEntryPoint<GameFlowBootstrap>();
    }
}

/// <summary>Boots the flow driver and wires the services that need the live run context.</summary>
public sealed class GameFlowBootstrap : IStartable
{
    private readonly GraphFlowDriver _driver;
    private readonly QuestGraph      _mainQuest;

    public GameFlowBootstrap(GraphFlowDriver driver, QuestGraph mainQuest)
    {
        _driver    = driver;
        _mainQuest = mainQuest;
    }

    /// <summary>The quest evaluator over the live run (null until <see cref="Start"/> has run).</summary>
    public QuestEvaluator Quests { get; private set; }

    public void Start()
    {
        _driver.Boot();                                        // creates driver.Context
        Quests = new QuestEvaluator(_mainQuest, _driver.Context);
    }
}
```

Notes:

- **MonoBehaviours are injected by method, never constructor.** The lib's own components
  (`GraphFlowDriver`, `AsyncSceneLoader`, `ContextTrigger`) expose their dependencies as settable
  properties (`Graph`, `SceneLoader`, …), so a `[Inject]`-annotated method on *your* component — or
  the bootstrap above — can hand them what they need before `Boot()`.
- **Anything created from `driver.Context` belongs after `Boot()`.** If a Presenter needs the
  evaluator, inject `GameFlowBootstrap` (or a small provider you register) rather than resolving
  `QuestEvaluator` directly at container-build time.
- **Standalone dialogue** follows the same shape without the driver: construct a
  `DialoguePlayer(graph, context, localizationProvider)` in a use case or bootstrap, and hand it to
  Presentation as an `IDialoguePlaybackSource`.

### Addressable content: the asset isn't there at `Configure()` time

The wiring example above assumes a direct reference (`[SerializeField] private QuestGraph _mainQuest`) —
the asset is already loaded by the time `Configure()` runs, so `builder.RegisterInstance(_mainQuest)` just
works. Swap that field for an `AssetReferenceT<QuestGraph>` and the assumption breaks: you only have a
GUID/handle at `Configure()` time, the real `QuestGraph` instance doesn't exist until
`AssetReference.LoadAssetAsync()` completes, and `Configure()` itself is synchronous — you cannot `await`
inside it.

This is not a new problem, just the existing **boot-order** subtlety with one more stage in front of it.
`Configure()` registers the *reference*, not the asset; the load happens in the same entry point that
already defers `Boot()` — and `GraphFlowDriver.Graph` is a plain settable property for exactly this reason,
so the driver's own flow graph can be assigned the same way if it's Addressable too:

A first-draft version of this ends up with three lifecycle bugs if you're not careful, all worth naming
because none of them are exotic:

1. **A bare `event Action Ready` can be missed.** `AsyncOperationHandle.Completed` fires *synchronously*,
   during `Start()`, whenever the asset is already resolved when you subscribe (a warm cache, an asset
   also referenced elsewhere) — not a rare edge case, the common one. A Presenter that subscribes to
   `Ready` in its own `OnEnable` after that has already happened never hears about it and reads a
   permanently-null result. Readiness needs to be **latched** (an awaitable that already knows the answer
   if asked late), not an event.
2. **No cancellation.** If the owning `LifetimeScope` is destroyed while the load is in flight (the player
   leaves the scene mid-load), the callback still fires later and touches a destroyed `GraphFlowDriver` —
   `MissingReferenceException`, far from the real cause. The callback must check whether it's still safe to
   act before touching anything, and something must actually short-circuit it once the scope is gone.
3. **A failure that only logs and returns leaves the game silently dead.** `Quests` stays unset forever,
   nothing downstream is told, there's no exception — just a hang. A failure has to reach whoever is
   waiting, not just the console.

```csharp
protected override void Configure(IContainerBuilder builder)
{
    // ── Register the reference, not the asset — nothing to load yet ──
    builder.RegisterInstance(_mainQuestRef);          // AssetReferenceT<QuestGraph>, assigned in the inspector

    builder.RegisterComponentInHierarchy<GraphFlowDriver>();
    builder.RegisterEntryPoint<GameFlowBootstrap>();
}

public sealed class GameFlowBootstrap : IStartable, IDisposable
{
    private readonly GraphFlowDriver _driver;
    private readonly AssetReferenceT<QuestGraph> _mainQuestRef;
    private readonly TaskCompletionSource<QuestEvaluator> _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AsyncOperationHandle<QuestGraph> _handle;
    private bool _disposed;   // not a CancellationTokenSource: nothing here accepts a token to pass one
                              // to — Addressables' Completed callback doesn't take one — so a token would
                              // be a bool wearing a synchronization handle it never uses.

    public GameFlowBootstrap(GraphFlowDriver driver, AssetReferenceT<QuestGraph> mainQuestRef)
    {
        _driver = driver;
        _mainQuestRef = mainQuestRef;
    }

    /// <summary>Latched: safe to <c>await</c> whether the caller arrives before or after loading
    /// finishes. Faults with the load error on failure; cancels if this bootstrap is disposed first.</summary>
    public Task<QuestEvaluator> Quests => _ready.Task;

    public void Start()
    {
        _handle = _mainQuestRef.LoadAssetAsync();
        _handle.Completed += OnQuestGraphLoaded;
    }

    private void OnQuestGraphLoaded(AsyncOperationHandle<QuestGraph> handle)
    {
        // The scope — and _driver with it — may already be gone. Never touch either past this point.
        if (_disposed) return;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            var error = new InvalidOperationException(
                "Failed to load main quest graph.", handle.OperationException);
            Debug.LogError(error);      // loud immediately, AND propagated below — not either/or
            _ready.TrySetException(error);
            return;
        }

        _driver.Graph = handle.Result;                     // same public property either way
        _driver.Boot();                                    // creates driver.Context
        _ready.TrySetResult(new QuestEvaluator(handle.Result, _driver.Context));
    }

    /// <summary>Called by VContainer when the owning <c>LifetimeScope</c> is destroyed.</summary>
    public void Dispose()
    {
        _disposed = true;
        _ready.TrySetCanceled();
        if (_handle.IsValid()) Addressables.Release(_handle);
    }
}
```

A Presenter consumes this the same way regardless of when it arrives: `var quests = await
_bootstrap.Quests;` — correct whether the load finished before injection or after, and it throws/cancels
instead of hanging if the load failed or the scope tore down first. That covers the *scope's* lifetime, not
the Presenter's own: the `catch` below handles the bootstrap being disposed, but if the HUD itself is
disabled or destroyed while the scope — and the load — are still very much alive, the `await` resumes
anyway and `Bind()` runs on a dead component. Same bug class the bootstrap fixes, one layer down, so the
example needs both checks, not just the one for `Dispose()`:

```csharp
private async void OnEnable()
{
    try
    {
        var quests = await _bootstrap.Quests;
        if (this == null || !isActiveAndEnabled) return;   // this component is gone, not just the scope
        Bind(quests);
    }
    catch (OperationCanceledException)
    {
        // Scope tore down before the load finished — nothing to bind, nothing to log.
    }
}
```

`destroyCancellationToken` (Unity 2022.2+) covers the same case more directly if you're already threading
tokens through — either way, an awaited continuation needs to check it's still attached to something alive
before touching it, the same rule as the bootstrap's own `_disposed` check just argued for.

If your project already uses VContainer's UniTask integration, `IAsyncStartable.StartAsync(CancellationToken)`
gives you a scope-tied token directly instead of owning a `CancellationTokenSource` yourself — same shape,
VContainer supplies the cancellation instead of you constructing it. It does **not** change the need to
expose `Quests` yourself: VContainer owns and awaits the `UniTask` your `StartAsync` returns internally: nothing
external can await that same task, so a latched property is still the only way for a Presenter to reach the
result.

Two things scale past this single-asset sketch:

- **More than one graph** (a main quest plus side quests, say) needs a key or a small registry instead of
  one `AssetReferenceT<T>` resolved by type — the container can only hold one meaningfully-resolvable
  instance per type without going to named registrations. Shape that registry to your own content
  strategy; it isn't something this doc can prescribe generically.
- **If your architecture keeps Addressables types out of your Application layer**, front this bootstrap
  behind your own port instead of taking `AssetReferenceT`/`AsyncOperationHandle` as constructor
  dependencies — the same posture-A move as `IGraphSaveStore` for persistence, just applied to content
  loading instead.

Either way, the two things that change from the direct-reference example stay: **the container gets the
reference, not the asset**, and **anything downstream of the loaded graph (`Quests`, the driver's `Graph`)
becomes genuinely asynchronous, not just boot-order-deferred**.

**A sharper risk than the loading mechanics: `SignalDef`/`VariableDef` identity.** Two things can look like
"the same asset loaded twice," and only one of them is actually a problem:

- **Bundle duplication** — the same `SignalDef` pulled into two Addressables groups with no shared
  dependency bundle — produces two separate C# instances at runtime, but both carry the identical
  serialized GUID (`SignalDef.Key`), and this ecosystem's signal/variable matching is string-keyed
  throughout (checked: the context store, `ContextTrigger`, and every vertical's gating logic all key on
  the cast-to-`string` GUID, never on the `SignalDef`/`VariableDef` reference or a dictionary keyed by it).
  Wasteful — duplicated bytes in two bundles — not incorrect: a raise from one instance still wakes a node
  awaiting the other's name.
- **Two different assets meant to be the same signal** — authored independently, different GUIDs, same
  intended concept — is the real danger, and Addressables makes it easier to fall into: content split
  across groups/labels isn't one browsable folder tree the way a purely local project is, so a second
  `DoorUnlocked` gets created because the first one wasn't visible from wherever the new content was
  authored. Two of the ecosystem's own safety nets narrow this more than they might look like they do at
  first, but neither closes it:
  - **A same-looking name is already caught.** `SignalConstantsGenerator`/`VariableConstantsGenerator`
    sanitize each display name to a C# symbol and abort generation on any collision — `Door Unlocked`,
    `door_unlocked`, and `Door-Unlocked` all sanitize to `DoorUnlocked` and would be reported and blocked,
    not silently merged. What survives is two *differently-named* Defs for the same concept
    (`DoorUnlocked` vs. `DoorOpened`) — nothing can catch that automatically, since nothing in the tooling
    knows the two names mean the same thing.
  - **A literal duplicate (Ctrl+D, file copy) is already handled, not just detected.** `SignalDef`/
    `VariableDef` implement `IStableGuidIdentity`; `StableIdDuplicateDetector` discovers both via
    `TypeCache` and regenerates the duplicate's id **on import**, automatically. This is a different case
    from the one above (identical GUID at the moment of copying, not two independently-authored GUIDs) —
    it's already closed, not a residual risk.

  For the case tooling can't close, where the Defs live still helps, but for narrower reasons than "keeps
  the constant list complete" — `AssetDatabase.FindAssets` (what the generators scan with) walks the whole
  Unity *project*, independent of Addressables groups, so the generated list is complete either way as long
  as the Defs live in the same project the generators run in; that argument only fails if remote content is
  authored in a genuinely separate Unity project. The reasons that do hold: pulling `SignalDef`/`VariableDef`/
  `CollectionDef` out of Addressables entirely, while the graphs that reference them stay remote, doesn't
  avoid duplication — Addressables still recopies them into every remote bundle that depends on them, as an
  *implicit* dependency this time, with no shared bundle to deduplicate against, i.e. the bundle-duplication
  case above minus the tooling that would have flagged it. Put them in their **own Addressables group, marked
  local (`Include in Build`), separate from the remote graph groups** instead: Addressable, so the build
  pipeline treats them as a proper shared dependency and bundles them once; local, so there is exactly one
  place they're authored, which is what actually helps a human notice `DoorUnlocked` already exists before
  authoring `DoorOpened` next to it. Check this against your Addressables Analyze Rules before relying on it.

A node awaiting a signal that never arrives under the name it expects doesn't throw or log — it just stays
parked forever. That silence is what makes this worth the discipline.

**Where the generated constants actually compile.** `SignalConstantsGenerator.Generate()`/
`VariableConstantsGenerator.Generate()` default to writing `GraphSignals`/`GraphVariables` under
`Assets/Generated/` — a plain folder with no asmdef of its own. In an asmdef-layered project that means the
generated class compiles into `Assembly-CSharp`, and no asmdef can reference back into `Assembly-CSharp` —
so none of your layers can see it as written. Use the `Generate(string outputPath)` overload to write
directly into a folder your Domain asmdef already covers instead of routing around the default:

```csharp
SignalConstantsGenerator.Generate("Assets/YourProject/Domain/Generated/GraphSignals.cs");
VariableConstantsGenerator.Generate("Assets/YourProject/Domain/Generated/GraphVariables.cs");
```

Wire that into your own `[MenuItem]` (or a build step) rather than the ecosystem's default one, which still
targets `Assets/Generated/`. Since generation is manual either way, nothing tells you the constants are
stale relative to the Defs that exist — worth a CI check (regenerate, fail on diff) if your Application
layer leans on them the way the note above describes.

### Cross-scene DI: keep `LifetimeScope` linked to every scene the flow loads

A `LoadSceneAction` (or your own `ISceneLoader`) transitions scenes on the FLOW's schedule, not yours —
VContainer's cross-scene `LifetimeScope` parenting (`LifetimeScope.EnqueueParent(...)`) only links up if
something calls it *before* the newly-loaded scene's own `LifetimeScope.Awake()` runs. Miss that and every
`[Inject]` in the new scene resolves against no parent: dependencies come back **null, silently** — no
exception, nothing in the console.

`EnqueueParent` returns `ParentOverrideScope`, a struct `IDisposable` that pushes the parent onto a static
stack; `Build()` peeks that stack for whichever `LifetimeScope` builds next. **Discard the return value and
the override never pops** — it keeps applying to every scene built afterward, not just the one you meant,
until something else happens to push a different override on top of it. Bracket it instead, across the
exact window from load start to load complete:

```csharp
IDisposable parentOverride = null;
asyncSceneLoader.SceneLoadStarted   += _  => parentOverride = LifetimeScope.EnqueueParent(currentScope.Container);
asyncSceneLoader.SceneLoadCompleted += _  => Pop();
asyncSceneLoader.SceneLoadFailed    += (_, __) => Pop();  // load threw/was cancelled — SceneLoadCompleted never fires
void Pop() { parentOverride?.Dispose(); parentOverride = null; }
```

Popping only on the success path is the same bug as not popping at all, just rarer: a failed or cancelled
load never raises `SceneLoadCompleted`, so the override stays on the stack and silently poisons every scene
built afterward — the failure that should have been visible in one place becomes a wiring bug three screens
later instead. Both loaders' `SceneLoadFailed` (`Action<string, string>`) exist precisely to give you the
other side of that branch.

The pop also has to happen at the right *moment*, not just on the right *event*: it must outlive the target
scene's `LifetimeScope.Awake()`, or the override disappears before `Build()` ever peeks it. `AsyncSceneLoader`
and `AddressablesSceneLoader` both guarantee this — `SceneLoadCompleted` fires only after the scene is
activated, i.e. after `Awake()` has already run — but that's a property of *this pair's* implementation, not
of the event names. A custom `ISceneLoader` that reuses this pattern has to preserve the same ordering as an
invariant (pop strictly after the target scene activates), not assume any loader whose event happens to be
named `Completed` satisfies it.

Because the stack is static and process-wide, this is only correct if **at most one scene transition is in
flight at a time**: two overlapping loads (preloading a second scene while the first is still building, for
instance) can push two overrides, and whichever `LifetimeScope.Build()` happens to run in that window can
peek the wrong one. Serialize scene transitions in your `ISceneLoader` (queue the next load until the
current one's `SceneLoadCompleted`/`SceneLoadFailed` has fired) — with the override scoped to a static stack
rather than to a specific scene, this isn't an edge case to shrug off, it's the condition the whole
mechanism depends on.

`UnitySceneLoader` (blocking `SceneManager.LoadScene`) raises none of these events — if any transition goes
through it, wrap the call itself in a `using (LifetimeScope.EnqueueParent(currentScope.Container))` block
in your own `ISceneLoader` (the blocking call's `Awake()` runs synchronously inside it, so the `using` scope
brackets it correctly and disposes on both the normal and the exception path for free), or standardize on
an async loader for any transition DI needs to reach into.

This is precisely what makes `GraphFlowDriver.Active` (see **Rules of thumb** below) unnecessary once set
up: with every flow-triggered scene transition keeping its `LifetimeScope` linked, a scene script's
`[Inject]` method reaches the persistent driver the same way it reaches any other dependency — there is no
"reference-less" scene script left, so there is no case left for `Active` to cover.

## Presentation: subscribe to seams, render your own visuals

The libs deliberately ship no mandatory visuals. A dialogue Presenter depends on the seam, not on the
player class:

```csharp
using Faolline.GraphDialogue;
using UnityEngine;
using VContainer;

public sealed class DialogueHudPresenter : MonoBehaviour
{
    private IDialoguePlaybackSource _source;

    [Inject]
    public void Construct(IDialoguePlaybackSource source) => _source = source;

    private void OnEnable()
    {
        _source.OnLine    += HandleLine;     // show the line in your View
        _source.OnChoices += HandleChoices;  // render buttons; each calls _source.Choose(id)
        _source.OnEnded   += HandleEnded;    // hide the HUD
    }

    private void OnDisable()
    {
        _source.OnLine    -= HandleLine;
        _source.OnChoices -= HandleChoices;
        _source.OnEnded   -= HandleEnded;
    }

    // Views stay dumb: HandleLine forwards text/speaker to a serialized View component,
    // and player input calls _source.Advance() / _source.Choose(choiceId).
    private void HandleLine(LineStep line)      { /* view.ShowLine(line); */ }
    private void HandleChoices(ChoiceStep step) { /* view.ShowChoices(step); */ }
    private void HandleEnded(EndStep end)       { /* view.Hide(); */ }
}
```

Flow HUDs work the same way against `GraphFlowDriver`'s events (`OnWaitingForSignal`,
`OnWaitingForTime` + `WaitRemaining` for countdowns, `OnEnded`), and gameplay code injects signals via
the driver rather than touching the runner.

## Posture A: a gateway, when you actually want one

Define the port in *your* Domain, in your game's vocabulary; implement it in *your* Infrastructure
over the lib service. Your use cases then depend on nothing from `Faolline.*`:

```csharp
// Domain — your vocabulary, no Faolline types.
public interface IQuestProgressGateway
{
    bool IsQuestComplete(string questId);
    event System.Action ProgressChanged;
}

// Infrastructure — the adapter over the lib (sketch; shape it to your QuestEvaluator usage).
public sealed class GraphQuestProgressGateway : IQuestProgressGateway { /* wraps QuestEvaluator */ }
```

Write these only where substitution is plausible. A gateway per lib type "for purity" re-creates the
lib's API one abstraction higher and adds a layer you must keep in sync — the classic cost the strict
posture trades for its flexibility.

## Rules of thumb

- The **container never appears inside the libs** — they take dependencies, they don't locate them.
  Keep it that way in your code too: resolve at the edges (bootstrap, Presenters), pass plain objects
  inward.
- **Cross-domain coupling goes through context primitives** (signals, variables, collections), not
  through code references between your quest/dialogue/flow modules — mirroring the ecosystem's own
  "verticals never reference verticals" rule.
- **Never `Boot()` twice / never resolve `Context` early** — the boot-order note above covers the one
  lifecycle trap.
- **`GraphFlowDriver.Active` is the one deliberate exception to "no singletons, no service locator" above** —
  a static fallback for scene scripts that have no wiring path to the persistent driver at all (a physics
  trigger dropped into a freshly-loaded scene, a UI button with no DI reach). Prefer an explicit reference
  wherever one is threadable: register the driver in your container (as the wiring example above already
  does) and inject it, or use a loader's own explicit target (`AsyncSceneLoader.SignalDriver`,
  `AddressablesSceneLoader.SignalDriver`) instead of letting a bridge component fall back to `Active`. In a
  fully DI-composed project this "no wiring path" case shouldn't occur at all — see **Cross-scene DI**
  above: keep every scene transition's `LifetimeScope` linked to its parent, and a scene script's `[Inject]`
  always reaches the driver; treat any appearance of `Active` in your own code as a wiring gap to close,
  not a normal fallback. If you'd rather this be enforced than remembered, a Roslyn analyzer banning
  `GraphFlowDriver.Active` outside your bootstrap/DI code is a small, natural addition.
- **Assets are content**: keep `Faolline` graph assets out of your Domain *code*, but don't pretend
  they're infrastructure — they're your authored game, like your prefabs.
