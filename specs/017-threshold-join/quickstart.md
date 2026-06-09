# Quickstart — P4 Generic threshold Join (k-of-N)

How to configure per-node join thresholds on the `ReactiveEvaluator` (graphstandard 0.2.0). One parameter k
spans AND / OR / N-of-M.

## 1. Default is AND (unchanged from P3)

```csharp
// D requires A, B, C. No config ⇒ k defaults to N=3 ⇒ AND.
var eval = new ReactiveEvaluator(graph, ctx, "completed");
// D is Available only when A, B, and C are all Completed.
```

## 2. Configure thresholds (k-of-N)

```csharp
var thresholds = new Dictionary<string, int>
{
    ["D"] = 1,   // OR     — D available after ANY one of A/B/C
    ["E"] = 2,   // 2-of-N — E available after any two of its prerequisites
    // (unlisted nodes keep the default AND)
};
var eval = new ReactiveEvaluator(graph, ctx, "completed", thresholds);
```

| Required count k | Meaning |
|------------------|---------|
| k = N (default)  | AND — all prerequisites |
| k = 1            | OR — any one prerequisite |
| 1 < k < N        | N-of-M — any k of N |
| k ≤ 0            | ungated — Available unless Completed |
| k > N            | never auto-available — Locked until the host completes it |

## 3. Everything else is the same as P3

The threshold is honored by `MarkCompleted` cascades, `OnNodeAvailable`/`OnNodeCompleted`, `Start()`, and
the reversible `Reevaluate()` — no extra calls. Example: a "region" node configured with k=2 over three
member nodes fires its available event exactly when the second member completes, and re-locks if a member
is un-completed and you `Reevaluate()`.

## 4. Key rules

- **Default = AND** when a node isn't listed — zero behavior change from P3.
- **One parameter** k covers AND / OR / N-of-M; k≤0 (open) and k>N (host-only) are well-defined.
- **graphcore untouched**; thresholds are evaluator configuration, not a node field.

## 5. Verify

graphstandard EditMode tests cover OR, N-of-M, default-AND-unchanged, k≤0, k>N, lifecycle integration, and a
game-like "region available when ≥ N members complete" scenario — all headless.
