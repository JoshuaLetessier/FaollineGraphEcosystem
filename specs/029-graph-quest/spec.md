# Feature Specification: Quest library (com.faolline.graphquest) — v1

**Feature Branch**: `029-graph-quest`

**Created**: 2026-06-14

**Status**: Draft

**Input**: User description: "A domain library above graphcore (and graphstandard's Reactive engine) modeling quests and objectives with state tracking, prerequisite gating, and reward hooks — all derived from the shared context. Two quest shapes (reactive objective DAGs and staged/sequential chains) share one objective/state/reward model. Authoring is code-first (fluent builder); the visual editor is deferred."

## User Scenarios & Testing *(mandatory)*

The "user" of this library is a **consumer developer** building a game on the Faolline graph ecosystem. They author quests in code (a fluent builder) and let the library derive quest/objective progress from the game's shared blackboard (the execution context), without writing their own state machine.

### User Story 1 - Declare a quest and read its progress from the context (Priority: P1)

A developer declares a quest with a few objectives, each with a completion rule expressed against the shared context (e.g. "the `herbs` collection contains 3 items", "`bossDefeated` is true"). At runtime they seed/update the context as the player acts, ask the library for the current state of each objective and of the quest, and get back `Locked` / `Active` / `Completed` / `Failed` — derived purely from the context, with no bespoke tracking code.

**Why this priority**: This is the core value and the minimum viable product — the shared objective/state model plus context-driven derivation. Everything else (gating, rewards, persistence, host seam) layers on top. Without it there is no quest library.

**Independent Test**: Declare a single quest with two or three objectives via the builder, seed a context so some conditions hold and others don't, evaluate, and assert each objective and the quest report the expected state. Fully testable headlessly with no editor and no host.

**Acceptance Scenarios**:

1. **Given** a quest whose objective `find` completes when the context holds a flag, **When** the context does not yet hold that flag, **Then** `find` is `Active` and the quest is `Active` (in progress).
2. **Given** the same quest, **When** the context is updated so the flag holds, **Then** `find` is `Completed`.
3. **Given** a quest whose objectives are all completed, **When** evaluated, **Then** the quest is `Completed`.
4. **Given** an objective with both a completion condition and a fail condition, **When** the fail condition holds, **Then** the objective is `Failed` (and a failed required objective fails the quest).
5. **Given** the same context evaluated twice, **When** evaluated again with no context change, **Then** the reported states are identical (deterministic / replay-safe).

---

### User Story 2 - Gate objectives and quests with prerequisites (Priority: P2)

A developer expresses ordering and unlocking: an objective (or a whole quest) stays `Locked` until its prerequisites are satisfied, then becomes `Active`. Prerequisites can form a **linear chain** ("do A, then B, then C" — a staged quest) or an **arbitrary directed acyclic graph** ("C unlocks only after both A and B" — a reactive progression). Both are expressed in the same builder and derived by the same evaluation.

**Why this priority**: Gating is what makes a list of objectives a *quest* (structured progression) rather than an unordered checklist. It is the unifying mechanism behind the two requested quest shapes (staged chain = a chain of prerequisites; reactive DAG = arbitrary prerequisites).

**Independent Test**: Declare a quest where objective `B` lists `A` as a prerequisite; with a context where `A` is not yet completed, assert `B` is `Locked`; complete `A` in the context, re-evaluate, and assert `B` becomes `Active`. Repeat with a diamond DAG (D requires B and C, which both require A) to prove non-linear gating.

**Acceptance Scenarios**:

1. **Given** objective `B` requires `A`, **When** `A` is not `Completed`, **Then** `B` is `Locked`.
2. **Given** objective `B` requires `A`, **When** `A` becomes `Completed`, **Then** `B` becomes `Active`.
3. **Given** a diamond where `D` requires `B` and `C`, **When** only `B` is completed, **Then** `D` stays `Locked`; **When** `C` also completes, **Then** `D` becomes `Active`.
4. **Given** a quest with an unlock prerequisite, **When** the prerequisite is unmet, **Then** the quest is `Locked` and none of its objectives are evaluated as `Active`.
5. **Given** a declared prerequisite topology that contains a cycle, **When** the quest is built, **Then** the build is rejected with a clear diagnostic (cycles cannot be satisfied).

---

### User Story 3 - Fire reward hooks exactly once on completion (Priority: P3)

A developer attaches a reward hook to a quest or objective. When that quest/objective transitions into `Completed`, the library fires the hook once so the consumer can grant the reward (the consumer supplies the actual effect; the library only owns *when* it fires). The hook must not fire again on subsequent re-evaluations of the same completed state.

**Why this priority**: Rewards are the payoff of a quest, but they depend on the state machine (US1) and typically on gating (US2). They are a thin seam on top, so they come after the core.

**Independent Test**: Attach a counting hook to an objective, evaluate repeatedly while the completion condition is false (hook count 0), make it true and evaluate (count 1), then evaluate several more times with the condition still true (count stays 1).

**Acceptance Scenarios**:

1. **Given** an objective with a reward hook, **When** it transitions from `Active` to `Completed`, **Then** the hook fires exactly once.
2. **Given** a completed objective, **When** the context is re-evaluated repeatedly with the condition still satisfied, **Then** the hook does not fire again.
3. **Given** a quest with a completion reward, **When** the last required objective completes (so the quest completes), **Then** the quest reward fires exactly once.

---

### User Story 4 - Persist and restore quest progress (Priority: P4)

A developer saves the game and later loads it; quest and objective progress (including which rewards have already fired) is restored so the player resumes exactly where they left off, and already-granted rewards are not granted again.

**Why this priority**: Persistence is essential for a real game but is additive over the model; it reuses the ecosystem's existing save layer rather than inventing storage.

**Independent Test**: Evaluate a quest to a partially-completed state with one reward already fired, capture a snapshot, restore it into a fresh context/evaluator, and assert all quest/objective states match and the already-fired reward does not fire again on the next evaluation.

**Acceptance Scenarios**:

1. **Given** a quest with some objectives completed and one reward fired, **When** progress is captured and restored, **Then** every quest/objective state matches the pre-save state.
2. **Given** a restored quest whose reward already fired before saving, **When** evaluated after restore, **Then** that reward does not fire again.
3. **Given** quest progress that is fully derivable from the context, **When** the context alone is restored, **Then** quest/objective states recompute correctly (progress does not require a separate store beyond what is needed for already-fired rewards).

---

### User Story 5 - Drive quests from a host's shared context (Priority: P5)

A developer running a live game host (the gameflow driver) lets quests evaluate against the *same* shared context the host already owns, so quest progress reacts to the same blackboard the rest of the game writes to — without the quest library taking a hard dependency on the host.

**Why this priority**: This is the forward-looking integration seam that lets quests participate in a running game. It is valuable but optional for the headless MVP, and must not couple the quest core to the host.

**Independent Test**: Build a quest against a plain context, then evaluate it against a context that a host driver owns and mutates; assert quest states track the host-driven context changes. Confirm the quest core package declares no dependency on the host package.

**Acceptance Scenarios**:

1. **Given** a host that owns a shared context, **When** the host updates that context, **Then** quests evaluated against it reflect the change.
2. **Given** the quest core package, **When** its dependencies are inspected, **Then** it does not depend on the host (gameflow) package.

---

### Edge Cases

- **Objective with no completion condition**: treated as never auto-completing (stays `Active` once unlocked) unless completed by an explicit rule — surfaced as an authoring warning, not a crash.
- **Quest with no objectives**: completes immediately (or is flagged at build time) — chosen behavior must be explicit and tested.
- **Required vs optional objectives**: a quest completes when all *required* objectives are completed; optional objectives do not block completion but still track state and can carry rewards.
- **Both completion and fail conditions true at once**: a defined precedence applies (fail takes precedence) and is tested.
- **Circular prerequisites**: rejected at build time with a diagnostic (a cycle can never be satisfied).
- **Re-evaluation after going "back"**: state is recomputed from the context, so reverting the context reverts derived states (replay-safe, multidirectional) — but already-fired one-shot rewards remain fired (their fired-status is tracked, not re-derived).
- **Evaluating a `Locked` quest's objectives**: locked quests do not surface `Active` objectives; their objectives report `Locked`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST provide a Quest model (an identity, a set of objectives, an optional unlock prerequisite, a completion rule, and optional reward hooks) and an Objective model (an identity, a completion rule, optional prerequisites, an optional fail rule, an optional required/optional flag, and optional reward hooks).
- **FR-002**: The library MUST derive every quest and objective state — `Locked`, `Active`, `Completed`, `Failed` — from the shared execution context, with no bespoke per-game tracking required.
- **FR-003**: State derivation MUST be deterministic and idempotent: evaluating the same context yields the same states, and reverting the context reverts the derived states (replay-safe / multidirectional).
- **FR-004**: Completion, fail, and prerequisite rules MUST be expressed using the existing graphcore condition vocabulary; the library MUST NOT introduce a new condition language.
- **FR-005**: The library MUST support prerequisite gating at both the objective and quest level, covering linear chains AND arbitrary directed acyclic prerequisite graphs, derived by the same evaluation.
- **FR-006**: The library MUST reject (at build time, with a clear diagnostic) a prerequisite topology that contains a cycle.
- **FR-007**: A quest MUST be `Completed` when all its required objectives are `Completed`, and MUST be `Failed` when a required objective is `Failed`; optional objectives MUST track state and rewards without blocking quest completion.
- **FR-008**: The library MUST fire each reward hook exactly once, on the transition into `Completed`, and MUST NOT fire it again on subsequent re-evaluations of the same completed state.
- **FR-009**: The library MUST own only the *timing/seam* of rewards (when they fire); the concrete reward effect MUST be supplied by the consumer (reusing graphcore actions/effects); the library MUST NOT ship concrete reward content.
- **FR-010**: The library MUST provide a code-first fluent builder to declare quests, objectives, their rules, prerequisites (chains and DAGs), required/optional flags, and reward hooks — sufficient to author a complete quest without the visual editor.
- **FR-011**: The library MUST emit state-change notifications (using the project's `Action<T>` convention, not UnityEvent) so a consumer can react to a quest/objective changing state, without the library providing any in-game UI.
- **FR-012**: Quest/objective progress MUST round-trip through the ecosystem's existing save layer (graphsave): after capture+restore, all quest/objective states match, and already-fired one-shot rewards do not fire again.
- **FR-013**: The library MUST provide a seam by which a host that owns a shared context can drive quest evaluation against that same context, WITHOUT the quest core taking a hard dependency on the host package.
- **FR-014**: The quest core MUST sit above graphcore and graphstandard and MUST NOT modify either; a missing reactive/standard primitive MUST be surfaced (flagged) rather than patched into those packages from here.
- **FR-015**: All public types MUST carry XML docs; runtime logging MUST use the `[GraphQuest]` prefix; one class per file.

### Key Entities *(include if feature involves data)*

- **Quest**: a named unit of progression — an identity, its objectives, an optional unlock prerequisite, a completion rule, optional reward hooks, and a derived state.
- **Objective**: a named goal within a quest — an identity, a completion rule, optional prerequisites, an optional fail rule, a required/optional flag, optional reward hooks, and a derived state.
- **Quest/Objective State**: the derived status — `Locked`, `Active`, `Completed`, `Failed`.
- **Prerequisite**: a gating relationship (a condition over the context and/or a dependency on another objective/quest's completion) forming a chain or a DAG.
- **Reward hook**: a one-shot seam fired on completion; the consumer supplies the effect.
- **Quest evaluator / driver**: the component that derives all states from a context, raises change notifications, and tracks already-fired rewards.
- **Quest builder**: the fluent, code-first authoring entry point that produces the above declaratively.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can author a complete multi-objective quest (with gating and a reward) and read correct progress from a context entirely in code — no editor, no host — in a single short builder expression.
- **SC-002**: For any seeded context, quest/objective states are reported deterministically and identically across repeated evaluations (0 variance), and revert correctly when the context reverts.
- **SC-003**: 100% of declared prerequisite relationships are enforced — a gated objective/quest is never reported `Active` while any prerequisite is unmet — across both linear-chain and DAG topologies.
- **SC-004**: Every reward hook fires exactly once per completion: across N repeated evaluations of a completed state and across a save/restore cycle, the fire count is exactly 1.
- **SC-005**: After a capture+restore cycle, 100% of quest/objective states match their pre-save values, and no already-granted reward is granted again.
- **SC-006**: The quest core package declares no dependency on the host (gameflow) package, and neither graphcore nor graphstandard is modified by this feature (their existing test suites stay green).
- **SC-007**: A declared cyclic prerequisite topology is rejected at build time 100% of the time, with a diagnostic naming the cycle.

## Assumptions

- **Authoring is code-first for v1.** The fluent builder is the authoring surface; the visual quest-graph editor is deferred to a later feature (mirroring the dialogue/starter editors).
- **The two quest shapes share one model.** A "staged/sequential" quest is a linear chain of prerequisites; a "reactive" quest is an arbitrary prerequisite DAG. Both are the same objective/state/reward model evaluated the same way — the library does not ship two separate engines.
- **State is derived from the shared context**, not stored as the source of truth. The only progress that is not re-derivable from the context is the "already fired" status of one-shot rewards, which the save layer persists.
- **Objectives are required by default**; a consumer marks an objective optional explicitly.
- **Fail precedes complete**: if both a completion and a fail rule hold simultaneously, the state is `Failed`.
- **Reactive evaluation reuses graphstandard.** The progression DAG derivation builds on graphstandard's existing Reactive engine rather than a new evaluator; if that engine lacks a needed primitive, it is flagged for a separate graphstandard change, not patched here.
- **Persistence reuses graphsave.** Context-derived progress piggybacks on the existing context snapshot; only the minimal quest-specific bits (e.g. fired-reward markers) are added to the snapshot.
- **No in-game UI.** The library ships data + change notifications + seams only; any quest journal / tracker UI is consumer territory.
- **Dependency floors**: graphcore 0.14.0, graphstandard 0.10.1, graphsave 0.3.1; new package `com.faolline.graphquest` starts at 0.1.0. The gameflow seam is provided without a hard dependency on gameflow.
