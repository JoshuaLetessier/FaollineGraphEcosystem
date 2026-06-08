# Phase 0 — Research: P1 Signals

All decisions respect graphcore's constitution (append-only / semver MINOR / universal abstractions /
TDD / simplicity). No NEEDS CLARIFICATION remained from the spec (semantic choices were fixed as
Assumptions; the one implementation fork — channel vs context-write — is resolved here as R1).

## R1 — Signal mechanism: dedicated channel vs. overloading `OnParameterChanged`

**Decision**: A **separate, dedicated signal channel** on `BaseContext` (`RaiseSignal`/`OnSignal`/
`OffSignal`/`TryGetLastSignal` over private `_signalSubs` + `_lastSignals`), reusing the *pattern* of the
existing parameter-subscriber plumbing (a `Dictionary<string, List<…>>` fired over a snapshot for
re-entrancy safety) but not its *storage*.

**Rationale**:
- A signal is a **transient event**, not stored state. If a raise were modelled as `Set<T>(name, value)`,
  the payload would land in `_params` and therefore be (a) returned by `GetAllParameters()` — the save
  surface (R5/spec R5), (b) deep-cloned into every history snapshot, (c) restored by `CopyValuesFrom`.
  That pollutes the parameter space and **blurs the exact distinction the spec calls out**.
- `Set<T>` cannot express a **payload-less** signal (it requires a typed value), violating FR-001/FR-007's
  "no payload" case and the "distinguish no-payload from scalar" requirement.
- `OnParameterChanged` delivers a bare `object` (the new value); it carries no signal *name* to the
  handler and no "has payload" bit, so US3's "read what happened, detect absence" needs extra structure
  anyway.
- Reusing the proven snapshot-on-fire iteration keeps Principle V satisfied (no new infrastructure
  pattern); the only new thing is a second, semantically-distinct dictionary.

**Alternatives considered**:
- *Overload `OnParameterChanged` / model signal as a notifying write*: rejected — persists transient data
  into saved/cloned state, cannot represent "no payload", conflates two concepts (the ≈80%-plumbing-reuse
  is a false economy once payload semantics and save/history exclusion are accounted for).
- *Signal bus as a brand-new class outside `BaseContext`*: rejected — the runner already holds a
  `BaseContext`; putting the channel there means zero new wiring and automatic inheritance by typed
  context subclasses, with no extra type to thread through `BaseRunner`.

## R2 — Await marker: field on `BaseNodeData` vs. dedicated node type

**Decision**: A single append-only **`string AwaitSignalName`** on `BaseNodeData` (default `""`; empty ⇒
not awaiting).

**Rationale**:
- Universal: *any* node can hold for a signal, exactly like `EntryConditions`/`OnEnterActions`/
  `IsCheckpoint` are universal lifecycle metadata already living on `BaseNodeData`.
- Append-only & back-compat: a new serialized field defaulting to empty leaves every pre-existing asset
  byte-for-byte unchanged (FR-008/FR-011); no migration.
- Simplicity (Principle V): no new node type, no new executor, no new editor node-view, no registry entry.

**Alternatives considered**:
- *Dedicated `AwaitSignalNodeData` + executor + node-view*: rejected — heavier surface for a behaviour
  that is a property of "this node pauses here", not a distinct node kind; would also force authors to
  insert wait nodes rather than annotate an existing one.
- *Await encoded as an `EntryCondition`*: rejected — entry conditions gate *whether* a node is entered
  (failing → `OnStuck`); awaiting is *holding after entry until an external event*, a different lifecycle
  point. Conflating them would overload `OnStuck` semantics.

## R3 — Waiting state, event, and authoritative state-setting

**Decision**: Append **`RunnerState.WaitingForSignal = 4`** and a new event
`event Action<BaseNodeData, string> OnWaitingForSignal`. In `EnterCurrentNode`, after `OnNodeEntered`,
branch: if `AwaitSignalName` is non-empty → set `_state = WaitingForSignal`, fire `OnWaitingForSignal`,
and **return before `OnNodeCompleted`**. On the normal path, set `_state = NodeReady` **immediately
before** `OnNodeCompleted` (idempotent for all existing flows).

**Rationale**:
- Appending an enum value is append-only safe: existing code branches on specific values
  (`if (_state != NodeReady) return;` in `Proceed`/`ChooseById`), so a new value simply makes manual
  `Proceed`/`ChooseById` no-ops while waiting — the desired behaviour (only a matching signal advances an
  awaiting node).
- Making `EnterCurrentNode` authoritative about `_state` fixes a real resume bug: today `_state` is set
  by callers and merely *stays* `NodeReady`; when resuming from `WaitingForSignal` via `ExitAndAdvance`,
  the next normal node must become `NodeReady`. Setting it in `EnterCurrentNode` (right before
  `OnNodeCompleted`) is a no-op for existing paths and correct for the resume path.

**Alternatives considered**:
- *Reuse `NodeReady` and track waiting in a side flag*: rejected — `RunnerState` is the public truth of
  what the runner is doing; a hidden flag would desync `State` from reality and surprise integrators/UI.
- *Reuse the vestigial `Paused = 2`*: rejected — `Paused` is documented for sub-graph suspension; reusing
  it for signal-waiting would conflate two meanings and muddy `State` reporting.

## R4 — Payload model

**Decision**: A new public `readonly struct SignalArgs { string Name; bool HasPayload; object PayloadBoxed;
T GetPayload<T>(); }`. Subscribers are `Action<SignalArgs>`. Raising: `RaiseSignal(string name)` (no
payload) and `RaiseSignal<T>(string name, T payload)` (T validated to bool/int/float/string, mirroring
`Set<T>`). The context keeps `_lastSignals: Dictionary<string, SignalArgs>` updated on each raise; logic
reads via `TryGetLastSignal(name, out SignalArgs)`.

**Rationale**:
- One value type carries name + presence + payload, so "no payload" (`HasPayload == false`) is
  unambiguous (FR-007), and US3 reads a typed value via `GetPayload<T>()`.
- Scalar-only (bool/int/float/string) stays consistent with `BaseContext`'s supported types and avoids
  boxing arbitrary objects in v1 (spec Assumptions). `GetPayload<T>` throws `InvalidCastException` on a
  type mismatch (same contract style as `Get<T>`); `HasPayload`/`TryGetLastSignal` give the safe path.

**Alternatives considered**:
- *Deliver bare `object` (null = no payload)*: rejected — `null` is ambiguous (a `string` payload may be
  null), so "no payload" could not be distinguished (fails FR-007).
- *Two subscriber overloads (`Action` and `Action<object>`)*: rejected — more public surface and still no
  clean absence signal; one `Action<SignalArgs>` is smaller and complete.

## R5 — History / step-back interplay

**Decision**: Signals are **excluded** from history. `DeepClone`/`CopyValuesFrom` do not copy
`_signalSubs` (subscribers, like `_subs` today) nor `_lastSignals` (transient). The **wait re-arms
automatically**: history snapshots already capture `CurrentNodeId`; `GoBack`/`GoBackToCheckpoint`
re-enter the node via `EnterCurrentNode`, which re-detects `AwaitSignalName` and re-enters
`WaitingForSignal` (FR-012). No new snapshot field is needed.

**Rationale**:
- Subscriptions are live wiring, never snapshot state (mirrors the existing `_subs` exclusion).
- The "last signal" store is transient event data; restoring it on step-back would imply replaying past
  events, which contradicts transient (edge-triggered) semantics. Only the *waiting* must be restored,
  and that falls out of re-entry for free.

**Alternatives considered**:
- *Capture `WaitingForSignal` + awaited name in the snapshot*: rejected — redundant, since re-entry
  recomputes it from the node; adding snapshot fields would be unnecessary surface and a back-compat risk
  on `HistoryEntry`/`GraphExecutionState`.

## R6 — Transient (edge-triggered) delivery; latching deferred

**Decision**: v1 delivery is **transient** — a raised signal reaches current subscribers and is not
remembered for a future await. A signal raised when no node awaits it does not later satisfy an await.

**Rationale**:
- Matches the spec Assumptions and keeps the runner stateless about past events.
- The race "the event happened before the node was reached" is precisely what the **Reactive engine
  (roadmap P3)** solves by evaluating *state* (collections) rather than transient events — overlapping
  responsibility here would pre-empt P3 and violate YAGNI.
- `TryGetLastSignal` still lets logic that runs synchronously right after a raise read the payload; it is
  a convenience, not a latch (it is not consulted by the await/resume mechanism).

**Alternatives considered**:
- *Latched/"sticky" signals (remember until consumed)*: rejected for v1 — adds consume/clear semantics
  and lifetime questions that belong to the Reactive engine; deferred (spec Out of Scope).
