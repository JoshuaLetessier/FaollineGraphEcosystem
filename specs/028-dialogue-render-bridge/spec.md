# Feature Specification: dialogue render bridge — runner-agnostic presenter + host choose/choice-pause (slice 9)

**Feature Branch**: `028-dialogue-render-bridge`

**Created**: 2026-06-12

**Status**: Draft

**Input**: Round-6 dogfooding headline. Embedding a dialogue as a **SubGraph** of a gameflow host worked for
*data + shared context* (the dialogue's outcome wrote straight into the host completed-set), but there was **no
render bridge**: when the host's `GraphFlowDriver`/`BaseRunner` descends into the dialogue subgraph it has no
dialogue rendering. `DialoguePlayer` resolves text/speaker/options but **owns its own runner** and can only
drive a `DialogueGraph` as its own root — it cannot *render* a dialogue a different runner owns. So the consumer
re-implemented ~40 lines of resolution. Also `GraphFlowDriver` exposes `Advance()` but **not `ChooseById`** (the
consumer reached into `driver.Runner`), and `AutoAdvance` **auto-resolves a `ChoiceNodeData`** (takes the first
passing edge), so the consumer toggled AutoAdvance by node type.

## Overview

Make "embed a *rendered* dialogue as a subgraph of a gameflow host" a ~10-line composition instead of a 40-line
rewrite, **without coupling the two libraries** (gameflow ⊥ dialoguesystem; both on graphcore — the consumer is
the integration point, per Constitution VII). Each lib ships the reusable brick it owns:

- **dialoguesystem** — extract the resolution into a **runner-agnostic `DialoguePresenter`**: given a dialogue
  node + a context + the providers, it produces the resolved `LineStep`/`ChoiceStep`. It works on the current
  node of **any** runner. `DialoguePlayer` keeps its public API and now delegates resolution to a presenter (no
  behavior change).
- **gameflow** — `GraphFlowDriver.ChooseById(id)` (re-exposed like `Advance`), and `AutoAdvance` **no longer
  auto-resolves a choice** (a `ChoiceNodeData` requires a deliberate pick; it pauses for `ChooseById`).

Line pacing stays a consumer concern (toggle `AutoAdvance` off while rendering a dialogue, drive `Advance()` per
line) — universal, no new lib surface and no graphcore change. graphcore is untouched; dialoguesystem
`0.2.0 → 0.3.0`; gameflow `0.5.0 → 0.6.0`.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Game integrator** — runs a host flow (gameflow) that embeds a dialogue subgraph and wants to *render* it
  (show lines, present choices, take a pick) without re-implementing the dialogue system's text resolution.

### User Story 1 - Resolve a dialogue node owned by any runner (Priority: P1) 🎯 MVP

An integrator's host runner is parked on a dialogue line or choice node (inside an embedded subgraph). They ask
a presenter to resolve that node into a displayable step — speaker name, localized text, choice options with
availability — exactly as `DialoguePlayer` would, but for a node the presenter does **not** own.

**Why this priority**: This is the missing reusable brick; it removes the ~40-line rewrite.

**Independent Test**: drive a `DialogueGraph` with a plain `BaseRunner` (not a `DialoguePlayer`); when it is on a
line node, `presenter.ResolveLine(node, ctx)` yields the resolved `LineStep`; on a choice node,
`presenter.ResolveChoice(node, ctx)` yields the options with availability — matching what `DialoguePlayer`
produces for the same graph.

**Acceptance Scenarios**:

1. **Given** a presenter built with a localization provider (+ optional assets/speaker lookup), **When** it
   resolves a line node against a context, **Then** it returns a `LineStep` with the resolved speaker name,
   localized + interpolated text, expression key, and voice (when an asset provider is given).
2. **Given** a choice node, **When** the presenter resolves it, **Then** it returns a `ChoiceStep` whose options
   carry the resolved label and an availability computed from each option's condition against the context.
3. **Given** the same graph, **When** resolved by the presenter vs. played by a `DialoguePlayer`, **Then** the
   produced steps are equivalent (the player delegates to the same resolution).

### User Story 2 - DialoguePlayer behavior is unchanged (Priority: P1)

Everything that used `DialoguePlayer` behaves exactly as before; it now delegates resolution to a presenter
internally.

**Why this priority**: Append-only guarantee for the dialogue lib.

**Independent Test**: the existing dialogue playback suite stays green; `MissingKeys`/`OnMissingKey`,
strict-mode behavior, and the emitted steps are identical.

**Acceptance Scenarios**:

1. **Given** the existing `DialoguePlayer` API and tests, **When** the resolution is delegated to a presenter,
   **Then** all observable behavior (steps, missing-key tracking, strict modes) is unchanged.

### User Story 3 - Pick a choice and pause on choices from the host driver (Priority: P1)

An integrator drives the host flow; when it reaches a choice node it must **pause** for a deliberate pick and be
able to **select** a branch by id from the driver, without reaching into the runner.

**Why this priority**: Auto-resolving a choice is a footgun; a host needs to pause and choose.

**Independent Test**: a host graph with a choice under `AutoAdvance = true` pauses at the choice (does not
auto-pick); `driver.ChooseById(optionId)` advances along that branch; a non-choice chain still auto-advances.

**Acceptance Scenarios**:

1. **Given** `AutoAdvance = true` and a flow that reaches a `ChoiceNodeData`, **When** the choice is entered,
   **Then** the driver does **not** auto-advance it (it stays ready for a pick).
2. **Given** a paused choice, **When** `driver.ChooseById(optionId)` is called, **Then** the flow advances along
   the matching branch.
3. **Given** a non-choice (single-successor) chain under `AutoAdvance`, **When** it runs, **Then** it still
   auto-advances to the end exactly as before.

### Edge Cases

- **Non-dialogue node** passed to the presenter → it returns no step (null), so a host can call it for every
  entered node and act only on dialogue ones.
- **A choice with no available option** → the presenter surfaces an all-unavailable `ChoiceStep` (the host
  decides what to do), consistent with how the player flags a dead-end.
- **Line pacing** → the host pauses lines by having `AutoAdvance` off while in the dialogue; this slice does not
  add a node-level pause flag (deferred).
- **`ChooseById` while not on a choice / unknown id** → a safe no-op (mirrors the runner's existing tolerance),
  ideally with a diagnostic.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The dialogue lib MUST provide a **presenter** that resolves a dialogue line node and a dialogue
  choice node — using a localization provider, optional asset provider, optional speaker lookup, and a strict
  mode — into the same `LineStep` / `ChoiceStep` the player emits, taking the node and a context as inputs
  (no runner ownership).
- **FR-002**: The presenter MUST resolve: localized + interpolated line text, speaker display name, expression
  key, and voice asset (when an asset provider is supplied) for lines; and per-option label + availability
  (from each option's condition) for choices. Missing-key handling MUST honor the strict mode (permissive /
  audit / strict) exactly as today, and expose the missing keys.
- **FR-003**: `DialoguePlayer` MUST delegate its resolution to the presenter and keep its entire public API and
  observable behavior unchanged (steps, `MissingKeys`/`OnMissingKey`, strict modes, save/restore, back).
- **FR-004**: `GraphFlowDriver` MUST expose a `ChooseById(id)` that selects a choice branch on the underlying
  runner (guarded like `Advance`: no-op when not running), so a host need not reach into `Runner`.
- **FR-005**: When `AutoAdvance` is enabled, the driver MUST NOT auto-advance a **choice** node (a `ChoiceNodeData`
  pauses for a deliberate `ChooseById`); it MUST continue to auto-advance non-choice nodes exactly as before.
- **FR-006**: graphcore MUST be unchanged. dialoguesystem and gameflow MUST stay append-only/source-compatible
  (only new members + an internal delegation + the choice-pause refinement); existing suites MUST stay green;
  both libs bump a MINOR. The stale dialoguesystem README version header MUST be corrected.
- **FR-007**: Dev standards — `[GraphDialogue]` / `[GraphGameFlow]` prefixes; one class per file (new presenter);
  XML docs on the presenter and the new driver members; READMEs + CHANGELOGs updated (the presenter + the
  hosted-render pattern; the driver `ChooseById` + the choice-pause behavior).

### Key Entities

- **DialoguePresenter**: a runner-agnostic resolver — providers + strict mode in, `LineStep`/`ChoiceStep` out for
  a given node + context.
- **Choice pause**: the driver behavior where a choice node halts auto-advance and waits for `ChooseById`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An integrator can render a host-owned dialogue subgraph by resolving the host runner's current node
  through the presenter — no re-implementation of text/speaker/option resolution.
- **SC-002**: The host driver pauses on choices and selects a branch via `ChooseById`; non-choice flows are
  unchanged.
- **SC-003**: `DialoguePlayer` and all existing dialogue/gameflow suites behave identically; graphcore untouched;
  both libs ship the new MINORs.
- **SC-004**: The round-6 ~40-line resolution rewrite collapses to a ~10-line consumer composition (presenter +
  `AutoAdvance` toggle + `Advance`/`ChooseById`).

## Assumptions

- **The consumer is the integration point.** gameflow and dialoguesystem stay decoupled; the bridge is the
  consumer wiring the host runner's current node to the presenter (Constitution VII). The libs only supply the
  reusable bricks.
- **Line pacing via `AutoAdvance` toggle** is acceptable for the MVP; a universal node-level "pause for input"
  flag (graphcore) is deferred.
- **Choice-pause is a fix, not a regression.** Auto-resolving a choice by "first passing edge" is a footgun; no
  existing gameflow flow/test relies on it (verified).

## Out of Scope *(deferred)*

- A universal `PauseForInput` node flag in graphcore (line auto-pause without an `AutoAdvance` toggle).
- A ready-made dialogue **UI** view bound to an external runner (the presenter resolves; rendering stays the
  consumer's / the existing UI package's concern).
- Stable authored node ids; localization manifest-root configurability; the dialogue code-first builder (other
  round-5/6 items).
- Any graphcore change.
