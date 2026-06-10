# Feature Specification: gameflow driver boot configuration seam (slice 5)

**Feature Branch**: `024-driver-boot-seam`

**Created**: 2026-06-10

**Status**: Draft

**Input**: User description: "Let a consumer inject a pre-seeded context and a populated node-executor registry
into the GraphFlowDriver at boot, so they can prepare shared state and custom executors before the flow
starts. Today Boot() creates a fresh empty context + empty registry internally and only exposes the context
after boot. The round-3 dogfooding finding — the seam the planned Reactive/Flow hosting needs."

## Overview

Three rounds of dogfooding have matured the gameflow runtime; the final, forward-looking finding is that the
driver's boot is **closed**: `Boot()` creates a fresh empty context and an empty executor registry internally,
and only exposes the context **after** boot. So a consumer cannot **pre-seed** the shared context (collections,
parameters, services) or **register custom node executors** before the flow starts. That hook is exactly what
the next growth step needs — hosting a Reactive progression / Flow abilities on the **same** shared context the
driver runs.

This slice opens that seam with a single additive `Boot(context, registry)` overload. The existing
parameterless `Boot()` is unchanged. graphgameflow `0.4.0 → 0.5.0`; graphcore and graphstandard untouched.

## User Scenarios & Testing *(mandatory)*

**Actors**

- **Game integrator** — prepares shared state (a completed-set collection, parameters, a service object) and
  registers custom node executors, then boots the driver on that prepared context.
- **(transitive) the next slice** — Reactive/Flow hosting will wire its engines onto this same shared context.

### User Story 1 - Boot the driver on a pre-seeded context (Priority: P1) 🎯 MVP

An integrator builds a context, seeds it (e.g. a collection of completed objectives, a starting parameter, a
service reference), then boots the driver with it. The flow runs on **that** context — a flow action reads the
seeded state.

**Why this priority**: Pre-seeding shared state is the core of the seam and the prerequisite for hosting a
progression/ability system that shares the driver's context.

**Independent Test**: seed a value/collection on a context, `Boot(context, …)`, run a flow whose action reads
that value — it sees the seeded state.

**Acceptance Scenarios**:

1. **Given** a context seeded before boot, **When** the driver is booted with it, **Then** the running flow's
   actions observe the seeded state (it is the live context).
2. **Given** a provided context, **When** the driver boots, **Then** the driver does **not** re-initialise it
   from the graph's declared parameters (a pre-seeded declared parameter is preserved, not overwritten).
3. **Given** a provided context with no scene loader set, **When** the driver boots, **Then** the driver fills
   in its own scene loader so scene-load actions still work; a context that already has one keeps it.

### User Story 2 - Boot with custom node executors registered (Priority: P1)

An integrator registers executors for custom node types and boots the driver with that registry, so those
nodes execute their logic when the flow enters them.

**Why this priority**: Custom executors are the other half of "prepare before start"; together with the
seeded context they let a real game extend node behavior.

**Independent Test**: register an executor for a node type, `Boot(…, registry)`, run a flow that enters such a
node — the executor runs.

**Acceptance Scenarios**:

1. **Given** a registry with an executor for a node type, **When** the driver is booted with it and the flow
   enters such a node, **Then** that executor is invoked.
2. **Given** no registry provided, **When** the driver boots, **Then** it uses an empty registry (current
   behavior) and statement/await nodes still run.

### User Story 3 - The no-argument boot is unchanged (Priority: P1)

Everything that booted with `Boot()` before behaves exactly the same: a fresh context (scene loader set,
initialised from the graph) and an empty registry.

**Why this priority**: Append-only guarantee — the seam must not change any existing behavior.

**Independent Test**: a flow that ran under `Boot()` before runs identically; the same warnings fire on
no-graph / no-start / already-running.

**Acceptance Scenarios**:

1. **Given** an assigned graph, **When** `Boot()` (no args) is called, **Then** the driver creates a fresh
   context initialised from the graph and an empty registry, exactly as before.
2. **Given** the boot guards (no graph / no valid start / already running), **When** either boot form is
   called, **Then** the same `[GraphGameFlow]` warnings fire and the driver stays inert.

### Edge Cases

- **`Boot(null, null)`** → behaves exactly like `Boot()` (fresh context + empty registry).
- **`Boot(context, null)`** → uses the provided context, a fresh empty registry.
- **`Boot(null, registry)`** → a fresh context (initialised from the graph), the provided registry.
- **A provided context already carrying a scene loader** → the driver does not override it.
- **Boot guards** apply identically to both forms (no graph / no start / already running).
- **Re-boot while running** → the same "already running" warning, no double-boot, for both forms.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The driver MUST provide an additive boot that accepts a caller-supplied context and node
  executor registry, used to run the flow.
- **FR-002**: When a context is supplied, the driver MUST run the flow on **that** context (the live one a
  flow action reads/writes) and MUST NOT re-initialise it from the graph's declared parameters (the caller
  owns the seeding).
- **FR-003**: When a supplied context has no scene loader, the driver MUST fill it with its own so scene-load
  actions work; a supplied context that already has one MUST keep it.
- **FR-004**: When a registry is supplied, the driver MUST start the runner with it so custom node executors
  are active; when none is supplied, it MUST use an empty registry.
- **FR-005**: When no context is supplied, the driver MUST create a fresh context, set its scene loader, and
  initialise it from the graph — identical to the existing no-argument boot.
- **FR-006**: The existing parameterless `boot` MUST be unchanged in behavior; all prior boot guards (no
  graph / no valid start / already running → the same `[GraphGameFlow]` warnings, stay inert) MUST apply to
  both forms.
- **FR-007**: graphcore and graphstandard MUST be unchanged; the slice-1..4 driver API MUST stay append-only
  and source-compatible (only a new overload + a shared internal path are added; all other members unchanged).
  The existing 667 EditMode + 9 PlayMode tests MUST stay green. The package MUST bump `0.4.0 → 0.5.0`.
- **FR-008**: Dev standards — `[GraphGameFlow]` prefix; one class per file; C# `Action<T>`; XML docs; README +
  CHANGELOG updated with the seam and a note that it is the foundation for hosting a progression/ability
  system on the shared context.

### Key Entities

- **Provided context**: the caller-built, pre-seeded shared blackboard the flow runs on.
- **Provided registry**: the caller-populated set of node executors active during the run.
- **Boot seam**: the additive boot that accepts both, alongside the unchanged no-argument boot.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An integrator can boot the driver on a context they seeded beforehand, and the running flow
  observes that seeded state.
- **SC-002**: An integrator can register a custom node executor and have it invoked when the flow enters its
  node.
- **SC-003**: A supplied context is not re-initialised from the graph (pre-seeded declared parameters survive)
  and gets a scene loader only when it lacks one.
- **SC-004**: The no-argument boot is byte-for-byte the prior behavior; all prior boot guards still fire.
- **SC-005**: graphcore/graphstandard untouched; the prior 667 EditMode + 9 PlayMode tests stay green;
  gameflow ships `0.5.0` with README + CHANGELOG updated.

## Assumptions

- **An overload, not a hook.** The chosen shape is `Boot(context, registry)` (with nulls falling back to the
  current behavior), not an `OnConfiguring`/`OnBeforeStart` event — the overload covers the need and is
  simpler.
- **The caller owns a supplied context's initialisation.** Providing a context means "I seeded it"; the driver
  does not `InitFromGraph` over it (which would overwrite seeded declared parameters). The no-context path
  keeps initialising from the graph.
- **The scene loader is filled only when absent**, so a supplied context without one still loads scenes, while
  a caller who set their own loader keeps it.
- **This slice is only the seam.** The actual Reactive/Flow hosting helpers are the next slice; here we just
  make the context/registry injectable.

## Out of Scope *(deferred)*

- The Reactive-progression / Flow-ability hosting helpers on the driver (the next slice; this is their seam).
- An `OnConfiguring` / `OnBeforeStart` event (the overload covers the need).
- A typed `GameFlowContext` subclass with domain keys (consumer-side, per the Typed Context Contract).
- Save / load.
- Any change to graphcore or graphstandard.
