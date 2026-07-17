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
`LifetimeScope` (or a content registry ScriptableObject) and hand them to the container as instances.

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
- **Assets are content**: keep `Faolline` graph assets out of your Domain *code*, but don't pretend
  they're infrastructure — they're your authored game, like your prefabs.
