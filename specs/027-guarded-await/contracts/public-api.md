# Public API Contract — guarded await (graphcore 0.7.0, graphstandard 0.7.0)

Additive. No existing member changes.

## graphcore — `BaseNodeData` (`Faolline.GraphCore`)

```csharp
public abstract class BaseNodeData
{
    public List<BaseCondition> EntryConditions { get; }   // unchanged
    public string AwaitSignalName { get; set; }           // unchanged

    /// <summary>Optional gate a matching await-signal must pass to resume this parked node. All must pass
    /// (AND); empty ⇒ no gate (resume on name match alone). A raise that fails the gate is ignored and the
    /// node stays parked (re-armable).</summary>
    public List<BaseCondition> ResumeConditions { get; }  // NEW (default empty)
}
```

## graphcore — `BaseRunner` behavior

`RaiseSignal(name)` / `RaiseSignal<T>(name, payload)`: unchanged signatures. A parked await node resumes when
the raised name matches **and** all `ResumeConditions` pass; otherwise the raise is recorded on the context but
the node stays `WaitingForSignal` (re-arm).

| Scenario | Result |
|----------|--------|
| name matches, no resume conditions | resumes (current behavior) |
| name matches, all resume conditions pass | resumes |
| name matches, a resume condition fails | ignored — stays parked, retriable |
| name does not match | ignored (regardless of conditions) |
| host `Advance`/forced GoTo | not gated by resume conditions |

## graphstandard — `GraphNodeBuilder` (`Faolline.GraphStandard`)

```csharp
/// <summary>Appends resume conditions (all must pass for a matching await-signal to resume the node).</summary>
public GraphNodeBuilder ResumeWhen(params BaseCondition[] conditions);   // NEW, mirrors When; returns this
```

## Compatibility

- **Additive only**: one new node field + a gated resume branch + one builder method. No signature changes;
  pre-existing assets (empty `ResumeConditions`) behave identically.
- **Versioning**: graphcore `0.6.0 → 0.7.0`; graphstandard `0.6.0 → 0.7.0` (dep → graphcore 0.7.0).
- **gameflow**: unchanged.
