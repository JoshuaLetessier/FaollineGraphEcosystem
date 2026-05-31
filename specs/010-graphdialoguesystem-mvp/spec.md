# Feature Specification: graphdialoguesystem — Graph-Based Dialogue Library (MVP)

**Feature Branch**: `010-graphdialoguesystem-mvp`

**Created**: 2026-05-31

**Status**: Draft

**Input**: User description: "Nouvelle librairie com.faolline.graphdialoguesystem : un système de dialogue à graphe construit entièrement au-dessus de la fondation graphcore (extension only), suivant le pattern du template starterGraph. MVP graphe + runtime jouable. Réutilise les nœuds/édition/persistance/runtime de graphcore ; ajoute le métier dialogue (lignes parlées, choix, locuteurs, texte localisé). Conditions/effets inline natifs graphcore. Localisation via abstraction + 2 providers (CSV par défaut, adaptateur Unity Localization optionnel)."

## Overview

`com.faolline.graphdialoguesystem` is a new downstream library that lets a writer author branching,
multi-speaker dialogues as a visual graph and lets a game play them back. It is the dialogue-domain
counterpart of the validated `starterGraph` template: it adds **only** the dialogue-specific concerns
(spoken lines, speakers, localized text, choices with localized labels) on top of the shared graph
foundation, which already provides the canvas, node editing, persistence, serialization, node search,
inspector framework, validation, and the headless playback engine.

This iteration delivers a **minimum viable, playable product**: an author can build a dialogue graph
with a start, spoken lines, branching choices, sub-dialogues, and an end; attach inline
conditions/effects and localized text; save it deterministically; and a game can play it back through
to completion in any configured language — all verified headlessly by an automated test suite.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author a branching dialogue on the canvas (Priority: P1)

A narrative designer opens the dialogue editor, creates a new dialogue asset, and visually builds a
conversation: a start, one or more spoken lines (each with a speaker and a line of text), a choice
point that branches into several options, and an end. They connect the nodes by dragging edges,
rename and recolor nodes, save, reopen, and find everything exactly as they left it.

**Why this priority**: Without authoring, there is no dialogue to play. This is the foundational
slice and the primary day-to-day activity of the tool's main user.

**Independent Test**: Create a dialogue asset, add a start → line → choice (2 options) → two ends,
connect them, set speaker/text on the line and labels on the choices, save, close, and reopen the
editor; confirm the graph round-trips with identical structure, ids, ordering, and field values.

**Acceptance Scenarios**:

1. **Given** an empty dialogue asset open in the editor, **When** the author adds a start node, a
   spoken-line node, a choice node, and an end node, **Then** each node appears on the canvas with a
   distinct, recognizable visual type and a stable unique identity.
2. **Given** a spoken-line node is selected, **When** the author sets its speaker and its line text,
   **Then** the values are shown in the inspector and persist after save/reload.
3. **Given** a choice node is selected, **When** the author adds two options and labels them, **Then**
   the canvas shows one outgoing connection point per option, each labeled, and connecting an option
   to a target node creates a visible branch.
4. **Given** a choice option already connected by an edge, **When** the author removes a different
   option from the same node, **Then** the surviving option keeps its connection (no edges are
   silently lost).
5. **Given** a saved dialogue, **When** the author closes and reopens it, **Then** node positions,
   colors, speakers, texts, choice options, ordering, and connections are identical to the saved state.
6. **Given** a dialogue is open in a window, **When** the author opens a second dialogue asset, **Then**
   it opens without disturbing or overwriting the first.

---

### User Story 2 - Play a dialogue back to the end (Priority: P1)

A gameplay programmer feeds a saved dialogue to the playback engine and drives it: the engine emits
the current speaker and localized line, waits for the player to advance, presents the available
choices at a branch, accepts a selection, and continues until the dialogue ends — exposing line,
choice, and end events the game UI can subscribe to.

**Why this priority**: A dialogue that cannot be played has no end-user value. Playback is the other
half of the MVP and must work headlessly so it is automatable and testable.

**Independent Test**: Build a small dialogue in memory (start → line → choice → line → end), start
playback, assert the first line's speaker and resolved text, advance, assert the choice set, select an
option, advance to the end, and assert the end event fires once — with no editor or scene required.

**Acceptance Scenarios**:

1. **Given** a dialogue starting at a spoken line, **When** playback starts, **Then** the engine
   reports the line's speaker and its text resolved into the active language, then waits for advance.
2. **Given** the engine is waiting on a spoken line, **When** the game advances, **Then** the engine
   proceeds to the next node following the single outgoing connection.
3. **Given** the engine reaches a choice node, **When** it presents choices, **Then** it lists each
   option with its localized label and whether the option is currently available.
4. **Given** a presented choice set, **When** the game selects an available option, **Then** the engine
   follows that option's branch to its target node.
5. **Given** an option whose condition is not met, **When** choices are presented, **Then** that option
   is reported as unavailable and selecting it does not advance the dialogue.
6. **Given** a dialogue reaches an end node, **When** the end is processed, **Then** the engine signals
   completion exactly once with the end's reason and stops.
7. **Given** a line or choice references a speaker, **When** that step is reported, **Then** the
   speaker's display name is resolved into the active language for the UI.

---

### User Story 3 - Gate branches and mutate state with inline conditions and effects (Priority: P2)

A designer makes the conversation reactive: they attach conditions to a choice option or to a node's
entry (e.g., "only if the player has the key"), and attach effects that run when a node is entered or
left (e.g., "mark quest accepted", "increase reputation"). During playback, gated options appear only
when their condition passes, and effects change the shared state that later conditions read.

**Why this priority**: Reactivity is what makes dialogue feel alive, but a linear branching dialogue
(US1+US2) is already a usable MVP, so this is the next increment rather than foundational.

**Independent Test**: Author a choice with one always-available and one condition-gated option; in
playback with the condition false, confirm the gated option is unavailable; run an effect that flips
the underlying value true; confirm that on a return visit the option becomes available.

**Acceptance Scenarios**:

1. **Given** a choice option with an attached condition, **When** the condition evaluates false at
   presentation time, **Then** the option is reported unavailable.
2. **Given** a node with an entry condition, **When** the condition fails on entry, **Then** the engine
   does not present that node's content and reports that it is stuck rather than advancing.
3. **Given** a node with an enter effect that sets a named value, **When** the node is entered, **Then**
   the value is updated in the shared dialogue state and is readable by later conditions.
4. **Given** a node with an exit effect, **When** the dialogue advances away from that node, **Then**
   the exit effect runs before the next node is entered.
5. **Given** the engine has advanced past several nodes, **When** the game steps back one step, **Then**
   the shared state is restored to its value at that earlier point.
6. **Given** a condition references a value missing from the shared state, **When** it is evaluated,
   **Then** it resolves to "not satisfied" and a diagnostic is logged rather than crashing playback.

---

### User Story 4 - Localize dialogue text across providers (Priority: P2)

A localization owner provides translations for every line, choice label, and speaker name. The same
dialogue plays in French, English, or any configured language by switching the active language, with
no change to the graph. They can use the library's built-in standalone text source, or, if the project
already uses the engine's localization system, plug that in instead — without rebuilding the dialogue.

**Why this priority**: Multi-language support is a core promise of the library and was a feature of the
previous version, but a single-language MVP (US1+US2 with text shown verbatim) is demonstrable first.

**Independent Test**: Author a dialogue whose lines reference text keys; provide translations for two
languages through the default standalone source; play once per language and confirm each step reports
the correct translated text; then swap to the optional engine-localization source and confirm the same
dialogue resolves text through it without graph changes.

**Acceptance Scenarios**:

1. **Given** a line referencing a text key with translations in two languages, **When** the dialogue is
   played with each language active, **Then** the reported line text matches that language's translation.
2. **Given** a choice option referencing a localized label, **When** choices are presented, **Then** the
   labels are shown in the active language.
3. **Given** a text key with no translation in the active language, **When** it is resolved, **Then** a
   defined fallback is returned (and a diagnostic logged) instead of empty or broken output.
4. **Given** the project uses the engine's own localization system, **When** that source is selected in
   settings, **Then** the same dialogue resolves its text through it with no edits to the graph.
5. **Given** no localization source is configured, **When** text is resolved, **Then** the system uses a
   safe default source so playback never fails for lack of configuration.

---

### Edge Cases

- A choice node with no options, or whose every option is condition-gated to unavailable, is presented
  with no selectable branch; the engine reports the player is stuck rather than advancing arbitrarily.
- A dialogue with no start/entry node defined cannot be played; starting playback fails with a clear
  diagnostic instead of silently doing nothing.
- A node references a speaker key that does not exist; the step still reports with a fallback display
  name and a diagnostic, rather than crashing.
- A sub-dialogue refers (directly or transitively) back to a dialogue already on the playback stack;
  the cyclic reference is refused before playback recurses, both at authoring time and at play time.
- An edge points to a node id that no longer exists after manual edits; loading the graph does not lose
  unrelated data, and the dangling connection is reported rather than corrupting the asset.
- Two authors’ windows are open on different dialogue assets simultaneously; saving one does not affect
  the other.
- A removed choice option leaves a dangling connection point; the connection is cleaned up and the
  remaining options’ connections are preserved.

## Requirements *(mandatory)*

### Functional Requirements

#### Authoring (US1)

- **FR-001**: The library MUST provide a dialogue asset type that an author can create from the editor's
  asset-creation menu and that carries a stable unique identity for its lifetime.
- **FR-002**: The library MUST let an author place, on a single canvas, at minimum: a start, spoken-line
  nodes, choice nodes, sub-dialogue nodes, and end nodes — each visually distinguishable by type.
- **FR-003**: A spoken-line node MUST expose an editable speaker reference and an editable localized line
  of text.
- **FR-004**: A choice node MUST let the author add and remove options, give each option a localized
  label, and (optionally) attach a condition to each option.
- **FR-005**: Each choice option MUST present exactly one outgoing connection point on the canvas, and
  connecting it MUST create a branch routed to that option specifically.
- **FR-006**: Adding or removing an option MUST preserve the connections of the other surviving options
  (no silent loss of edges).
- **FR-007**: The author MUST be able to rename and recolor nodes, mark a node as a checkpoint, and set
  the end reason on an end node.
- **FR-008**: Saving a dialogue then reloading it MUST reproduce the graph with identical node identities,
  field values, option ordering, collection ordering, and connections (deterministic round-trip).
- **FR-009**: Opening a second dialogue asset MUST NOT disturb or overwrite a dialogue already open in
  another window.
- **FR-010**: Manually editing a dialogue that contains a dangling or invalid connection MUST NOT cause
  loss of unrelated data on load; the problem MUST be surfaced rather than silently corrupting the asset.
- **FR-011**: The editor MUST be operable accessibly: keyboard-driven node creation/selection, sufficient
  visual contrast for node types, and on-screen hints for primary actions.

#### Playback (US2)

- **FR-012**: The library MUST provide a headless playback engine that can run a dialogue to completion
  with no scene, no game-loop component, and no editor present.
- **FR-013**: On starting, the engine MUST begin at the dialogue's entry node and report the first step.
- **FR-014**: For a spoken line, the engine MUST report the speaker and the line text resolved into the
  active language, then wait for an explicit advance.
- **FR-015**: For a choice, the engine MUST report each option's localized label and availability, and
  MUST advance only when an available option is selected.
- **FR-016**: Selecting a specific option MUST route playback along that option's branch.
- **FR-017**: On reaching an end, the engine MUST signal completion exactly once, carrying the end reason,
  and stop accepting advance/selection input.
- **FR-018**: The engine MUST expose subscribable events for "line ready", "choices ready", and "ended"
  so a game UI can react without polling.
- **FR-019**: The engine MUST support nested sub-dialogues: entering a sub-dialogue node plays that
  dialogue and, on its end, resumes the parent automatically.
- **FR-020**: The engine MUST detect and refuse cyclic sub-dialogue references before recursing, both at
  authoring time and at play time, with a clear diagnostic.

#### Reactivity — inline conditions & effects (US3)

- **FR-021**: The library MUST let conditions be attached inline to a choice option, to a connection,
  and to a node's entry; all attached entry conditions MUST pass for the node to be entered.
- **FR-022**: The library MUST let effects be attached inline to a node's entry and exit; enter effects
  run after entry conditions pass, exit effects run before advancing away.
- **FR-023**: Conditions and effects MUST read and write a shared, typed dialogue state (supporting at
  least boolean, integer, decimal, and text values) addressed by named keys.
- **FR-024**: The library MUST ship a small, ready-to-use set of conditions (compare a named value of
  each supported type) and effects (set a named value of each supported type; log a message), so an
  author can build reactive dialogue without writing code.
- **FR-025**: A condition referencing a missing or mistyped value MUST resolve to "not satisfied" and log
  a diagnostic instead of throwing.
- **FR-026**: The engine MUST support stepping back at least one step, restoring the shared state to its
  value at that earlier point; and stepping back to the most recent checkpoint.
- **FR-027**: Named state keys MUST be referenced through defined constants, never as raw literals at the
  point of use, so the set of keys is centrally known.

#### Localization (US4)

- **FR-028**: The library MUST resolve every author-facing piece of dialogue text (line text, choice
  labels, speaker display names) from a text key into a displayed string according to the active language.
- **FR-029**: The library MUST define a single neutral text-resolution contract that the playback engine
  depends on, independent of any specific localization technology.
- **FR-030**: The library MUST ship a default, self-contained text source (no external dependency) that
  satisfies the contract, so the library is usable out of the box.
- **FR-031**: The library MUST also offer an optional adapter to the engine's own localization system,
  isolated so that projects not using it incur no dependency on it.
- **FR-032**: A lightweight setting MUST select which text source is active and the current language; if
  none is configured, a safe default source MUST be used so playback never fails for lack of setup.
- **FR-033**: Resolving a key absent in the active language MUST return a defined fallback and log a
  diagnostic, never empty or broken output.

#### Speakers (US2/US4)

- **FR-034**: The library MUST provide a speaker concept carrying a localizable display name and a set of
  named visual expressions (key → presentation asset) with a fallback expression.
- **FR-035**: A dialogue step that names a speaker MUST be able to resolve that speaker's display name and
  the requested expression, falling back safely when a key is unknown.

#### Foundation & process constraints (cross-cutting)

- **FR-036**: The library MUST be built strictly as an extension of the existing graph foundation, with
  zero modification to that foundation.
- **FR-037**: All dialogue-domain meaning (speakers, lines, localized text) MUST live in this library and
  never in the shared foundation.
- **FR-038**: The library MUST reuse the foundation's existing node types, canvas, persistence,
  serialization, node search, inspector framework, validation, shared-state blackboard, and playback
  state machine rather than reimplementing any of them.
- **FR-039**: The library MUST NOT introduce node types for conditions or effects; reactivity is
  expressed only inline on existing nodes, options, and connections.
- **FR-040**: Cross-dialogue invocation MUST occur only through the sub-dialogue mechanism; this library
  MUST NOT take a hard dependency on any sibling ecosystem library.
- **FR-041**: Every new behavior MUST be covered by an automated editor-mode test written before its
  implementation (test-first), runnable headlessly.

### Key Entities *(include if feature involves data)*

- **Dialogue (asset)**: The authored conversation. Owns its nodes, connections, declared state
  parameters, entry point, and a stable identity. Specialization of the foundation's graph asset.
- **Spoken-line node**: A step where one speaker says one localized line. Adds a speaker reference and a
  localized text key to the foundation's generic statement node.
- **Choice node**: A branch point holding an ordered list of options.
- **Choice option**: One selectable branch. Carries a localized label, an optional gating condition, and
  a stable identity used to route its branch. Specialization of the foundation's generic choice.
- **Start / End / Sub-dialogue nodes**: Reused foundation node types — entry point, terminus (with an end
  reason), and a node that plays another dialogue.
- **Condition (inline)**: A reusable, named test over the shared state, attachable to an option, a
  connection, or a node entry.
- **Effect (inline)**: A reusable, named mutation of the shared state, attachable to a node's entry/exit.
- **Dialogue state (shared blackboard)**: Typed named values (bool/int/decimal/text) read by conditions
  and written by effects; snapshot-able for step-back. Specialization of the foundation's context with a
  typed accessor surface and centralized key constants.
- **Speaker**: An interlocutor with a localizable display name and named expressions (key → presentation
  asset) plus a fallback.
- **Text-resolution contract**: The neutral interface that turns a text key + active language into a
  displayed string.
- **Text source — default**: A self-contained, dependency-free implementation of the contract.
- **Text source — engine adapter (optional)**: An isolated adapter implementing the contract over the
  engine's own localization system.
- **Localization setting**: Lightweight configuration selecting the active text source and language.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An author can build, from empty, a playable 6-node branching dialogue (start, two lines, a
  2-option choice, two ends) — connected, with speaker/text and choice labels set — in under 5 minutes.
- **SC-002**: 100% of authored content (node identities, field values, option order, connections,
  positions, colors) is identical after a save → close → reopen cycle.
- **SC-003**: A dialogue can be played from start to end entirely headlessly (no scene, no editor), and
  this is demonstrated by the automated test suite.
- **SC-004**: The same dialogue plays correctly in at least two languages with no change to the graph,
  switching language only.
- **SC-005**: A condition-gated option is correctly hidden/shown across at least one state change driven
  by an effect, verified end to end.
- **SC-006**: Stepping back one step restores the prior shared-state values in 100% of tested cases.
- **SC-007**: The same dialogue resolves its text through both the default text source and the optional
  engine-localization adapter, with no graph edits, verified by tests.
- **SC-008**: The library adds zero changes to the shared foundation (verified by diff) and depends on no
  sibling ecosystem library.
- **SC-009**: Every shipped behavior has an automated editor-mode test, and the full suite passes green
  before the feature is considered done.
- **SC-010**: No malformed input in the tested edge-case set (missing entry, empty/blocked choice,
  unknown speaker/key, cyclic sub-dialogue, dangling edge) causes a crash; each yields a diagnostic and a
  safe outcome.

## Assumptions

- The shared graph foundation (`graphcore`) already provides — and this MVP relies on without
  modification — the canvas/editor base, node types (start, statement, choice, sub-dialogue, end),
  deterministic persistence and serialization, node search, the inspector framework, validation, the
  typed shared-state blackboard, and the headless playback state machine (advance, choose, step-back,
  checkpoints, sub-graph nesting, cycle detection, history).
- The library follows the validated `starterGraph` package shape (separate runtime, editor, and
  editor-mode test assemblies) as its structural template.
- "Localized text" in the MVP means resolving a key to a string per active language; rich text features,
  per-character timing, and voice/audio are out of scope for this iteration.
- Speaker "expressions" are referenced (key → presentation asset) for the UI to use; spawning/animating
  avatars and any scene presentation are out of scope (the engine reports data; UI is the game's concern).
- The default text source uses a simple, self-contained tabular text format; authoring round-trips and
  translation editing for that format beyond basic load/resolve are minimal in this iteration.
- The optional engine-localization adapter targets the project's existing localization system and is
  packaged so projects without it take no dependency on it (per constitution v1.2.0).
- Playback is single-active-dialogue per engine instance; concurrent independent dialogues use separate
  engine instances.
- Test-driven development is mandatory and tests are editor-mode and headless (no play-mode tests
  required), consistent with the project constitution.
- The module is developed as a sibling folder during development and finalized as a distributable package
  later; that packaging finalization is out of scope for this iteration.

## Out of Scope (this iteration)

- Voice-over, audio, typewriter/letter-by-letter effects, and rich-text styling.
- Avatar spawning, animation, and any in-scene presentation of speakers.
- A full translation-management/editor workflow for the default text format beyond load/resolve.
- Visual condition/effect nodes (reactivity is inline-only by design).
- Porting the previous version's custom graph view, custom runner, custom serialization, or its
  empty marker context — all replaced by the shared foundation.
- Final UPM package publishing/distribution.
