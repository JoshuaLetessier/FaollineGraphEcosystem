# Phase 0 — Research: collection primitives + reactive-hosting pattern

## R1 — Promote into the real lib, keep the graphTest fixtures as test reference

**Decision**: Add `AddToCollectionAction`, `CollectionContainsCondition`, `CollectionCountAtLeastCondition` to
`com.faolline.graphstandard` (namespace `Faolline.GraphStandard`). Leave `TestCollectionContainsCondition` /
`TestCollectionCountCondition` in `graphTest` unchanged.

**Rationale**: graphTest is a demonstration/fixture lib; a consumer cannot author production graphs with test
types. The standard lib is where domain-neutral, authorable primitives belong (its mandate). Keeping the
graphTest copies avoids touching the foundation's test fixtures (append-only, suite-green) and preserves their
role as the reactive-engine test reference.

**Alternatives**: *move the types out of graphTest* — rejected: churns the foundation's tests for no gain.
*reference graphTest from consumers* — rejected: a test asmdef is not a production dependency.

## R2 — `CollectionCountAtLeastCondition` (threshold), not a comparison-operator enum

**Decision**: The count condition carries only a `key` + an `int threshold` and is satisfied when
`CollectionCount(key) >= threshold`. It does **not** promote graphTest's full `ComparisonOperator` enum.

**Rationale**: The pattern needs exactly "k done unlocks this" — a `>=` gate. YAGNI: the other operators have no
caller. A single-purpose, self-describing condition (`…AtLeast`) is clearer to authors than a generic
operator+value pair. Equality/less-than gates can be added later if a real use appears.

**Alternatives**: *promote the generic `CollectionCountCondition` with `ComparisonOperator`* — rejected (YAGNI,
more surface). *expose both* — rejected (two ways to do one thing).

## R3 — The completion → re-derivation bridge is `OnCollectionChanged → Reevaluate`

**Decision**: The reactive-hosting pattern routes a collection write to re-derivation with
`context.OnCollectionChanged(completedKey, _ => evaluator.Reevaluate())` — a two-line subscription the consumer
writes. No signal convention, no change to `ReactiveEvaluator`, no change to the driver.

**Rationale**: graphcore's `BaseContext` already raises a per-key change event (`OnCollectionChanged`), so the
`AddToCollectionAction` write naturally notifies any listener. The evaluator's `Reevaluate()` is idempotent
(state derives only from the current set), so a redundant fire is harmless. This keeps the slice to *primitives +
documentation* and defers any turnkey wrapper until dogfooding justifies it (Out of Scope).

**Alternatives**: *have the action call `evaluator.MarkCompleted`* — rejected: couples a universal action to the
reactive engine (Constitution II). *a driver signal convention `"done:<id>"`* — deferred to the optional gameflow
host wrapper (Out of Scope). *evaluator self-subscribes to the context* — deferred: would change the engine
(graphstandard append-only; not needed for this slice).

## R4 — Empty / zero / absent semantics

**Decision**:
- `AddToCollectionAction`: empty/whitespace key OR value → no-op (no exception, flow continues).
- `CollectionContainsCondition`: absent collection ⇒ `CollectionContains` returns false ⇒ not satisfied.
- `CollectionCountAtLeastCondition`: absent collection ⇒ count 0; threshold `0` ⇒ always satisfied; positive
  threshold ⇒ not satisfied on an empty/absent collection.

**Rationale**: graphcore's collection API already returns false/0 for absent keys, so the conditions are
total functions with no special-casing. The action's empty-input no-op matches the configuration-tolerant style
of the other standard primitives (a half-configured asset must not throw at runtime).

**Alternatives**: *throw on empty config* — rejected: brittle authoring; a warning-and-skip is the house style.
