# Feature Specification: In-Game Dialogue UI

**Feature Branch**: `011-dialogue-ui`

**Created**: 2026-06-03

**Status**: Draft

**Input**: User description: In-game dialogue UI layer for `com.faolline.graphdialoguesystem` — runtime presentation of dialogues with Canvas (UGUI/TextMeshPro) and UI Toolkit (UIDocument) front-ends, plus speaker avatars and player input. Mirror the reference implementation in `com.faolline.dialoguesystem~`, adapted to our player which already resolves localized text.

## Overview

The dialogue runtime (`DialoguePlayer`) is currently headless: it emits steps (line / choices / end)
through C# events but draws nothing on screen. This feature adds a **presentation layer** so a game
developer can put a working dialogue on screen with no custom code — wiring a prefab/components to a
`DialogueGraph` asset and pressing Play.

Two front-ends are provided so projects can use their preferred UI technology:
- **Canvas** (UGUI + TextMeshPro)
- **UI Toolkit** (UIDocument)

Both share the same speaker-avatar behaviour and the same player-driving logic; only the rendering
differs. Because the player resolves localized strings upstream, the UI displays ready-to-show text
and needs no localization package or table knowledge.

Two audiences:
- **Game developer** (integrator): assembles a dialogue scene from provided components.
- **Player** (end user): reads lines, advances, and picks choices in the running game.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Play a dialogue on a Canvas UI (Priority: P1)

A game developer adds the Canvas dialogue components to a scene, assigns a `DialogueGraph` asset and
the speaker assets, and presses Play. The first line appears with the speaker's name and resolved
text. Pressing the advance control moves to the next line. When a choice point is reached, one button
per option appears; clicking an available option continues down that branch. When the dialogue ends,
the UI hides.

**Why this priority**: This is the minimum that makes the whole headless runtime usable in a real
game. Without it, the player exists but nothing is visible. It is the MVP.

**Independent Test**: Open the Canvas sample scene, press Play, advance through a line, pick a choice,
reach the end — verify text/speaker shown, choice routing works, UI hides at end. Fully demonstrable
on its own.

**Acceptance Scenarios**:

1. **Given** a driver wired to a dialogue graph and a Canvas view, **When** the dialogue starts, **Then** the first line's resolved text and speaker name are displayed and the choices area is hidden.
2. **Given** a line is displayed, **When** the player triggers advance, **Then** the next step is shown (next line, choices, or end).
3. **Given** a choice point with N options, **When** it is displayed, **Then** exactly N choice controls are shown, each labelled with the option's resolved label.
4. **Given** displayed choices where one option is unavailable, **When** the player views them, **Then** the unavailable option is shown in a non-selectable (disabled) state and cannot be chosen.
5. **Given** an available choice, **When** the player selects it, **Then** the dialogue continues down that option's branch (routed by its choice id).
6. **Given** the dialogue reaches an end, **When** the end step fires, **Then** the line, speaker, choices, and avatars are cleared/hidden.

---

### User Story 2 - Play the same dialogue on a UI Toolkit UI (Priority: P2)

A developer who uses UI Toolkit assigns a `UIDocument` (with line label, speaker label, and a choices
container) instead of Canvas objects, and gets the same dialogue behaviour. Choices can be rendered
either by creating one button per option at runtime, or by reusing a fixed set of pre-defined buttons
declared in the UXML.

**Why this priority**: Parity with Canvas for the growing share of projects on UI Toolkit. Reuses all
shared logic; only rendering differs. Valuable but secondary to having one working front-end.

**Independent Test**: Open the UI Toolkit sample scene, press Play, advance a line, pick a choice,
reach end — same outcomes as US1 but rendered through the UIDocument.

**Acceptance Scenarios**:

1. **Given** a UIDocument with the expected named elements, **When** a line is shown, **Then** the line and speaker labels display the resolved strings.
2. **Given** the dynamic choices mode, **When** choices are shown, **Then** one button per option is created in the choices container and removed on the next step.
3. **Given** the fixed-slots choices mode, **When** choices are shown, **Then** the pre-defined buttons are populated/enabled for present options and disabled/hidden for absent ones.
4. **Given** an unavailable option, **When** choices render, **Then** the corresponding button is shown disabled and is not selectable.

---

### User Story 3 - Speaker avatars react to the active speaker and expression (Priority: P2)

As lines play, the on-screen avatar reflects the current speaker and their expression. When the
speaker changes, the previous avatar is demoted (e.g. to a secondary position) and the new one is
shown; optional transitions can animate the swap. Avatars are cleared when the dialogue hides.

**Why this priority**: Avatars are core to dialogue presentation and are shared by both front-ends.
Independent of which text renderer is used, but not required for a textual MVP, so P2.

**Independent Test**: Play a dialogue whose lines alternate speakers/expressions; verify the displayed
avatar matches each line's speaker+expression, that an unknown speaker/expression degrades gracefully
(no avatar, no error), and that avatars are gone after the dialogue ends.

**Acceptance Scenarios**:

1. **Given** speakers bound to the view, **When** a line plays for a speaker with a matching expression asset, **Then** that avatar is displayed at the current-speaker mount.
2. **Given** an avatar is showing for speaker A, **When** a line for speaker B plays, **Then** A's avatar is demoted/removed and B's avatar is shown.
3. **Given** a line whose speaker or expression has no matching asset, **When** it plays, **Then** no avatar is shown and no error is raised (a fallback asset is used if the speaker defines one).
4. **Given** any avatar is visible, **When** the dialogue hides, **Then** all avatars are removed.

---

### User Story 4 - Keyboard input for advance and choice selection (Priority: P3)

In addition to pointer/click interaction, the player can advance lines with a key (Space) and pick
choices with number keys (1–9). This works whether the project uses the legacy Input Manager or the
new Input System.

**Why this priority**: A convenience that speeds testing and supports keyboard-first games, but
clicking already covers the core interaction, so lowest priority.

**Independent Test**: With either input backend active, play a dialogue, press Space to advance a
line, press a number key to choose; verify the same outcomes as clicking, and that pressing the number
of an unavailable/absent option does nothing.

**Acceptance Scenarios**:

1. **Given** a line is displayed, **When** the advance key is pressed, **Then** the dialogue advances (same as a click on advance).
2. **Given** choices are displayed, **When** the number key for an available option is pressed, **Then** that option is chosen.
3. **Given** choices are displayed, **When** the number key for an unavailable or non-existent option is pressed, **Then** nothing happens.

---

### User Story 5 - Ready-to-run samples for both front-ends (Priority: P3)

The package ships sample scene(s)/prefabs wiring each front-end to the existing sample dialogue, so a
developer can see a working setup to copy from.

**Why this priority**: Accelerates adoption and serves as living documentation, but the feature works
without it. Lowest priority.

**Independent Test**: Import/open the samples, press Play, and a dialogue runs end-to-end with no extra
setup for both Canvas and UI Toolkit.

**Acceptance Scenarios**:

1. **Given** the Canvas sample, **When** opened and played, **Then** the sample dialogue runs to completion with no manual wiring.
2. **Given** the UI Toolkit sample, **When** opened and played, **Then** the sample dialogue runs to completion with no manual wiring.

---

### Edge Cases

- **No view assigned**: the driver runs the dialogue logically and logs a clear warning rather than throwing.
- **Empty/short choice set vs. fixed slots**: extra fixed slots are hidden; if there are more options than slots, the surplus is not shown and a warning is logged.
- **All options unavailable** at a choice point: the player surfaces a "stuck" condition; the UI shows the (disabled) options and does not soft-lock silently.
- **Advance pressed during a choice point**: ignored (advance only applies to lines).
- **Speaker/expression mismatch**: graceful — no avatar, no exception (see US3).
- **Dialogue disabled/destroyed mid-play**: event subscriptions are cleaned up; no leaked avatars or callbacks.
- **Re-starting a dialogue** on the same driver: prior state, avatars, and choice controls are cleared before the new run.
- **Missing resolved text** (provider returned a fallback marker): the UI displays whatever the player provides verbatim (the player owns fallback policy).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a UI contract (view) that accepts the player's emitted steps and exposes: show a line, show choices, hide everything, and bind the available speakers.
- **FR-002**: The view MUST display a line's resolved text and resolved speaker name without performing any localization itself.
- **FR-003**: The view MUST display one selectable control per choice option, labelled with the option's resolved label.
- **FR-004**: The view MUST present unavailable options in a visible, non-selectable (disabled) state.
- **FR-005**: Selecting an option MUST identify it to the player by its choice id (not by position/index).
- **FR-006**: A driver component MUST own a player instance, subscribe to its line/choices/end/stuck events, and route them to the assigned view.
- **FR-007**: The driver MUST advance the current line on the advance interaction (pointer and/or key) and MUST ignore advance during a choice point.
- **FR-008**: The driver MUST support choosing an option via pointer click and via number keys (1–9), under both the legacy Input Manager and the new Input System.
- **FR-009**: The system MUST provide a Canvas (UGUI/TextMeshPro) front-end implementing the view contract.
- **FR-010**: The system MUST provide a UI Toolkit (UIDocument) front-end implementing the view contract, supporting both runtime-created choice buttons and pre-defined choice slots.
- **FR-011**: The system MUST display a speaker avatar matching the current line's speaker and expression, swapping it when the speaker/expression changes, with an optional animated transition.
- **FR-012**: Avatar resolution MUST degrade gracefully: an unknown speaker or expression results in no avatar and no error, using the speaker's fallback asset when defined.
- **FR-013**: The system MUST clear all displayed text, choices, and avatars when the dialogue hides or ends.
- **FR-014**: The headless runtime (player and core runtime) MUST remain free of any UI dependency; the UI MUST live in a separate assembly.
- **FR-015**: The system MUST clean up event subscriptions and spawned avatars when the driver/view is disabled or destroyed, and when a dialogue is restarted.
- **FR-016**: The system MUST ship runnable samples demonstrating both the Canvas and UI Toolkit front-ends with the existing sample dialogue.
- **FR-017**: The driver MUST function when no view is assigned (logical run only) and warn rather than fail.

### Key Entities

- **Dialogue View (contract)**: the presentation boundary. Receives a line step, a choices step, a hide command, and a speaker binding; raises a "choice selected" notification carrying the chosen option's id.
- **Shared UI Adapter (base)**: technology-independent behaviour common to both front-ends — speaker registry and avatar lifecycle (show current, demote previous, despawn, optional transition, clear on hide).
- **Canvas Front-End**: a view implementation rendering with UGUI text and buttons.
- **UI Toolkit Front-End**: a view implementation rendering with a UIDocument's labels and buttons (dynamic or fixed-slot choices).
- **Dialogue Driver**: orchestrates a player + a view + input; the single component a developer drops into a scene to make a dialogue play.
- **Speaker / Avatar**: an interlocutor with a display-name fallback and a set of expression→presentation-asset mappings plus a fallback asset (already defined by the runtime; consumed here).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can make the sample dialogue play on screen (Canvas or UI Toolkit) by opening the corresponding sample and pressing Play, with zero lines of code written.
- **SC-002**: Switching a working dialogue scene from Canvas to UI Toolkit (or vice-versa) requires changing only the view component, not the driver or the dialogue asset.
- **SC-003**: A line's on-screen text and speaker name match what the player resolved for the active locale in 100% of shown lines.
- **SC-004**: Selecting choice option *k* always continues down option *k*'s branch (correct routing by id), verified across a multi-branch dialogue.
- **SC-005**: Unavailable options are never selectable by click or key, in 100% of cases.
- **SC-006**: Playing a dialogue start-to-end leaves no orphaned avatar objects or active choice controls in the scene afterwards.
- **SC-007**: The core runtime assembly compiles and runs with the UI assembly absent (no UI dependency leaks into the headless core).

## Assumptions

- **Resolved-text model**: The player resolves localized strings before emitting steps; the UI never reads localization tables or keys. Changing locale takes effect on the next emitted step, not retroactively on the line already shown (live re-resolution of the current line is out of scope).
- **Avatar representation**: A speaker expression maps to an instantiable presentation prefab placed under a mount in the scene/canvas; 2D vs 3D is the project's choice. This mirrors the reference implementation.
- **Unavailable option presentation**: Disabled-but-visible (greyed), not hidden — so players see paths gated by conditions. (Configurable hiding may come later.)
- **Advance semantics**: Advance applies only to lines; at a choice point the player must pick an option (no "advance" past choices).
- **Auto-start**: The driver exposes an opt-in auto-start (default on) plus a public Start method for manual control.
- **TextMeshPro present**: The Canvas front-end assumes TextMeshPro is available (standard in modern Unity projects); UI Toolkit is built into the engine.
- **Choice key mapping**: Number keys 1–9 map to the first nine options in display order; more than nine options are not keyboard-selectable (still clickable).
- **Reuses existing types**: `DialoguePlayer`, `LineStep`, `ChoiceStep`, `ChoiceOption`, `EndStep`, and `Speaker` from `com.faolline.graphdialoguesystem` are reused as-is; no runtime API changes are required by this feature.

## Dependencies

- `com.faolline.graphdialoguesystem` runtime (player, steps, `Speaker`).
- TextMeshPro (Canvas front-end) and the built-in UI Toolkit (UI Toolkit front-end).
- Optional: project input backend (legacy Input Manager or Input System) for keyboard controls.

## Out of Scope (Non-Goals)

- Typewriter / letter-by-letter text reveal.
- Voice-over / audio playback.
- Save/restore UI (the runtime supports session save/restore; a UI for it is separate).
- History/backlog window (the player exposes Back/BackToCheckpoint, but a backlog view is deferred).
- Authoring-time changes to the graph editor.
