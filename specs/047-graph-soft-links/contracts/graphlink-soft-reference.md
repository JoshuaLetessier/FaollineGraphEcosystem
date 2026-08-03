# Contract: GraphLinkNodeData soft reference (Lot 1 — graphcore)

**Package**: `com.faolline.graphcore` 0.40.1 → 0.41.0

## Public API surface

```csharp
namespace Faolline.GraphCore
{
    public class GraphLinkNodeData : BaseNodeData
    {
        public const string NodeTypeId = "graphcore/graph-link";   // unchanged

#if UNITY_EDITOR
        public BaseGraph TargetGraph { get; set; }   // unchanged signature; GUID-backed, Editor-only
#endif
        public string TargetGraphGuid { get; set; }  // NEW — plain string, available everywhere

        public string Note { get; set; }              // unchanged
    }
}
```

## Compatibility contract

- **Source compatibility**: 100% for any code compiled against `TargetGraph` from an Editor-context assembly (the only place it was ever legally dereferenced, per Constitution VII / spec constraint that this node is never touched at runtime). A hypothetical Runtime-assembly caller of `TargetGraph` — which does not exist today anywhere in the ecosystem — would fail to compile; this is intentional (see `research.md` R2) and matches the node's own documented contract ("never touches TargetGraph at runtime").
- **Binary/serialized-data compatibility**: **broken, intentionally**. `.asset` files with a serialized `_targetGraph` reference will deserialize with `_targetGraphGuid` empty (Unity ignores unknown/removed serialized fields; no exception). This is the explicitly-authorized deviation from Constitution Principle I recorded in `research.md` R8 — no data migration is provided, per explicit requester sign-off that no real project has deep usage of this field.
- **Build/dependency contract (the feature's actual point)**: after this change, `UnityEditor.AssetDatabase.GetDependencies(ownerGraphPath)` MUST NOT include a `GraphLinkNodeData` target's asset path. This is the primary acceptance gate (spec SC-001) and MUST be verified via an actual Addressables/Player build + Analyze pass, not by code inspection (spec background: the failure mode is silent).

## Validator contract (new)

`GraphValidator.Validate(graph)` gains two new issue kinds — one self-contained, one via a new generic extension seam (this package also gains `IGraphValidatorExtension`/`GraphValidatorExtensionRegistry`; see `graph-catalog-port.md` for the concrete extension `graphgameflow` registers into it):

1. **Unresolved GraphLink target** (self-contained, no seam):
   - **Trigger**: a `GraphLinkNodeData` node where `!string.IsNullOrEmpty(TargetGraphGuid)` and `TargetGraph == null`.
   - **Severity**: `Warning` (consistent with this node type's existing "isolated node" exemption — a GraphLink is annotation, never a hard failure).
   - **Must fire even when Addressables/AssetDatabase dependency scanning would see nothing wrong** — this rule is what compensates for the lost compile-time reference (spec FR-009).
2. **SubGraph crossing into a registered chapter root** (via the new `GraphValidatorExtensionRegistry` seam — `graphcore` never learns what a "chapter root" is; see `research.md` R9 for why a direct call into `graphgameflow` would violate Constitution Principle II):
   - **Trigger**: a `SubGraphNodeData` with a resolved `TargetGraph` for which any registered `IGraphValidatorExtension.CheckSubGraphTarget` returns a non-empty string.
   - **Severity**: `Warning`, using the extension's returned message verbatim.
   - With zero extensions registered (e.g. `graphgameflow` not installed), this rule is inert — matches the "opt-in, empty by default" contract every other registry in this feature follows.

```csharp
public interface IGraphValidatorExtension
{
    string CheckSubGraphTarget(BaseGraph targetGraph);   // null/empty = no opinion
}

public static class GraphValidatorExtensionRegistry
{
    public static IReadOnlyList<IGraphValidatorExtension> Extensions { get; }
    public static void Register(IGraphValidatorExtension extension);
    public static void Unregister(IGraphValidatorExtension extension);
}
```
