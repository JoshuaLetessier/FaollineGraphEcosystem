# Phase 0 — Research: dialogue render bridge

## R1 — Extract a runner-agnostic presenter; the player delegates

**Decision**: Move `DialoguePlayer`'s resolution (`BuildLineStep`, `BuildChoiceStep`, `ResolveChecked`,
`ResolveSpeakerName`, missing-key/strict handling) into a new `DialoguePresenter` keyed only on a node + a
`BaseContext` + the providers. `DialoguePlayer` keeps its own runner and API but builds steps **through** an
internal presenter.

**Rationale**: The resolution never needs the runner — only the current node, the context, and the providers.
The runner-ownership is what blocked hosting. Extracting it makes the resolution reusable for a node owned by
*any* runner (the host's), while the player's behavior is preserved by delegation (guarded by the existing
dialogue suite).

**Alternatives**: *a `DialoguePlayer` ctor overload taking an external runner* — rejected: the player carries
advance/choose/back/save semantics tied to owning its runner; a host already owns those via its driver. A pure
presenter is the smaller, cleaner brick. *duplicate the resolution in gameflow* — rejected: couples gameflow to
dialogue (Constitution VII) and duplicates logic.

## R2 — Driver pauses on a choice + exposes ChooseById

**Decision**: In `GraphFlowDriver.HandleNodeCompleted`, auto-advance only when `_autoAdvance && node is not
ChoiceNodeData`. Add `public void ChooseById(string id)` → `_runner.ChooseById(id)` under the running guard.

**Rationale**: A `ChoiceNodeData` inherently needs a deliberate pick; auto-advancing it by "first passing edge"
is the round-6 footgun. `ChoiceNodeData` is a graphcore type, so the driver gates on it without knowing anything
about dialogue (layering preserved). `ChooseById` mirrors the existing `Advance`/`RaiseSignal` re-exposure.

**Alternatives**: *keep auto-resolving choices, add an opt-out flag* — rejected: the safe default is to pause a
choice; no existing flow relies on auto-resolution (verified). *expose the runner instead of ChooseById* —
rejected: the consumer already had to reach into `Runner`; a first-class method is the fix.

## R3 — Line pacing stays a consumer AutoAdvance toggle (no graphcore flag)

**Decision**: To pause on lines for reading, the consumer sets `AutoAdvance = false` while rendering the
dialogue and drives `Advance()` per line. No node-level pause flag is added.

**Rationale**: This is universal and already worked at round-6; a `PauseForInput` flag would reopen graphcore
for marginal convenience. Deferred (Out of Scope). Choices pause regardless via R2.

## R4 — Layering: the consumer is the integration point

**Decision**: gameflow and dialoguesystem gain no dependency on each other. The bridge is consumer code: read
`driver.Runner.CurrentNode`, resolve via the presenter, render, and drive `Advance()`/`ChooseById()`.

**Rationale**: Constitution VII — cross-lib composition is the consumer's job; the libs only provide reusable
bricks so the glue is ~10 lines, not ~40.
