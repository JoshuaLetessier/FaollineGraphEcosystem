# Contract: Addressables adapter (Lot 4 — graphgameflow.addressables)

**Package**: `com.faolline.graphgameflow.addressables` 0.4.0 → 0.5.0
**Availability**: only meaningful/testable in an environment with Addressables installed — the
package already has this dependency (mirrors `AddressablesSceneLoader`).

## Runtime API surface

```csharp
namespace Faolline.GraphGameFlow.Addressables
{
    public class AddressablesGraphCatalog : IGraphCatalog
    {
        public void Resolve(string graphId, Action<BaseGraph> onResolved, Action<string> onFailed);
    }

    public class PreloadNextChapterAction : BaseAction
    {
        // [SerializeField] AssetReferenceT<BaseGraph> _nextChapter — own Addressables drawer
        public override void Execute(BaseContext context);
    }
}
```

## Editor API surface

```csharp
namespace Faolline.GraphGameFlow.Addressables.Editor
{
    public sealed class AddressablesGraphKeyProvider : IGraphKeySourceProvider
    {
        // mirrors AddressablesSceneKeyProvider exactly; [InitializeOnLoadMethod] self-registers
    }
}
```

## Compatibility / dependency contract

- `com.faolline.graphcore` MUST show zero reference (direct or transitive) to
  `com.unity.addressables` after this lot — verified by asmdef inspection (spec constraint,
  Constitution II). Only `com.faolline.graphgameflow.addressables` (already an Addressables-
  dependent package) gains new members.
- `PreloadNextChapterAction`'s `AssetReferenceT<BaseGraph>` MUST NOT appear in
  `AssetDatabase.GetDependencies` of the graph asset that owns the action (spec SC-001/SC-007) —
  this is the build-dependency contract this whole lot exists to prove end-to-end, and MUST be
  verified via a real Addressables Build + Analyze pass (spec background: silent failure mode).

## Behavioral contract (spec User Story 4)

- `PreloadNextChapterAction.Execute` triggers the load and returns immediately (synchronous
  `void`, no change to `BaseAction`'s contract) — the actual asset load happens on Addressables'
  own async machinery, exactly like `AddressablesSceneLoader`.
- Both usage forms from the spec must work unmodified against this action:
  1. **Early trigger + reboot**: action fires early in a chapter; by the time the chapter's
     `OnEnded` fires, the resolved `BaseGraph` is available for `driver.Graph = ...; Boot(...)`.
  2. **Park-on-signal**: the action optionally raises a configurable completion `SignalDef`
     (mirroring `AddressablesSceneLoader.LoadCompletedSignal`), letting a node use the existing
     `AwaitSignalNames` mechanism to park until the preload completes — no new runner state.
