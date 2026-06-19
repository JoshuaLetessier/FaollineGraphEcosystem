# Feature Specification: Visual GraphLink cross-reference + editor navigation

**Feature Branch**: `030-graphlink-navigation`

**Created**: 2026-06-16

**Status**: Draft

**Input**: User description: "Let a gamedev open a host graph (e.g. a zone/level gameflow) and visually see which other graphs — quests, dialogues, others — belong to that part of the game, and double-click to open each in its proper editor. Authoring/readability only, zero runtime behaviour. A non-executed `GraphLink` annotation node + a graph-type→editor navigation registry."

## User Scenarios & Testing *(mandatory)*

A graph in this ecosystem composes with other graphs only through shared runtime state — a zone's flow,
its quests, and its dialogues live in separate assets with no visible link between them. A newcomer opening
the zone's flow cannot tell which quests are "part of" this zone. This feature adds a purely **documentary**,
editor-only association: an annotation node that points at another graph, so the structure of a game is
readable by browsing from a root graph, without changing how anything runs.

### User Story 1 - Read which graphs belong to a part of the game (Priority: P1)

A game developer opens a host graph that represents a level/zone (e.g. its flow). Sitting alongside the flow,
they see clearly-marked annotation nodes that name the other graphs belonging to this part of the game —
"Quest: Relics", "Quest: Trade", "Dialogue: Merchant" — each showing the referenced graph's name and its
kind. They can now read off the composition of the zone at a glance.

**Why this priority**: This is the core value — making the cross-graph structure visible. It is usable on its
own (even without the click-to-open navigation): the labelled annotations already answer "which quests are
part of this zone?".

**Independent Test**: Add an annotation node referencing a quest graph to a flow graph, open the flow graph in
the editor, and confirm the annotation renders as a labelled "Quest: <name>" reference distinct from the
executable nodes.

**Acceptance Scenarios**:

1. **Given** a flow graph with an annotation node referencing a quest graph, **When** the developer opens the
   flow graph, **Then** the annotation is shown as an openable reference displaying the quest's name and kind.
2. **Given** an annotation node whose referenced graph is not set, **When** the flow graph is opened, **Then**
   the annotation renders without error and clearly indicates it has no target.

---

### User Story 2 - Jump straight to the referenced graph (Priority: P2)

From the host graph, the developer double-clicks an annotation node and the referenced graph opens in the
editor that is meant for it — a quest reference opens the quest editor, a dialogue reference opens the dialogue
editor, a flow reference opens the flow editor — with the referenced asset already loaded.

**Why this priority**: Turns the static labels into navigation, letting a newcomer traverse the whole game
from a root graph. Builds on US1 but is separable (US1 delivers value without it).

**Independent Test**: Double-click an annotation referencing a quest graph and confirm the quest editor opens
showing that quest; double-click one whose kind has no registered editor and confirm a graceful fallback
(the asset is selected/pinged) plus a clear diagnostic, with no error.

**Acceptance Scenarios**:

1. **Given** an annotation referencing a quest graph and a quest editor that has registered itself, **When**
   the developer double-clicks the annotation, **Then** the quest editor opens with that quest loaded.
2. **Given** an annotation whose referenced graph kind has no registered editor, **When** the developer
   double-clicks it, **Then** the system falls back to selecting/pinging the asset and reports a clear
   diagnostic instead of failing.

---

### User Story 3 - The annotation never changes how the game runs (Priority: P3)

The association is documentation only. A developer must be able to add, remove, or re-point annotation nodes
with absolute confidence that the running game behaves identically — the annotation is never executed and
never touches the graph it references.

**Why this priority**: Trust/safety guarantee that makes the feature adoptable. It underpins US1/US2 but is
expressed as its own testable promise.

**Independent Test**: Run a flow with and without annotation nodes present (both off the execution path and,
as a safety net, wired onto it) and confirm the run outcome is identical and the referenced graphs are never
entered.

**Acceptance Scenarios**:

1. **Given** a flow with annotation nodes placed off the execution path, **When** the flow runs to completion,
   **Then** the result is identical to the same flow without the annotations and no referenced graph is touched.
2. **Given** an annotation node accidentally wired onto the execution path, **When** the flow runs, **Then**
   execution passes straight through it (like a comment) without pausing, executing the reference, or throwing.

### Edge Cases

- An annotation references a graph that is later deleted from disk → renders as a broken/missing reference
  with a clear label, never throws; double-click reports the missing target.
- An annotation references the host graph itself, or two annotations point at each other → purely cosmetic
  (no execution), so no cycle concern; the editor still opens the target normally.
- Multiple annotations reference the same graph → all render and all open the same target; allowed.
- The referenced graph's kind has no registered editor (e.g. a brand-new lib not yet wired in) → graceful
  fallback (select/ping the asset) + diagnostic, never a hard failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Authors MUST be able to place, in any graph, an annotation node that references another graph of
  any kind (quest, dialogue, flow, or future kinds).
- **FR-002**: The annotation MUST be visually distinct from executable nodes and MUST display the referenced
  graph's display name and its kind so the association is readable when the host graph is opened.
- **FR-003**: The annotation MUST be inert at runtime — it is never executed and never accesses the graph it
  references. A run with the annotation present off the execution path MUST be identical to a run without it.
- **FR-004**: If execution ever reaches an annotation node (e.g. it was wired onto the path), it MUST pass
  straight through to its next step without pausing, executing the reference, or raising an error.
- **FR-005**: Authors MUST be able to open the referenced graph from the annotation (e.g. double-click), which
  opens it in the editor appropriate to that graph's kind, with the referenced asset loaded.
- **FR-006**: The mapping from a graph kind to the editor that opens it MUST be extensible by downstream libs
  registering their own editor, so the core has no built-in knowledge of any specific game-domain library.
- **FR-007**: When no editor is registered for the referenced graph's kind (or the target is missing), the
  open action MUST fall back gracefully (select/ping the asset) and report a clear diagnostic, never failing.
- **FR-008**: Adding this feature MUST NOT change any existing runtime behaviour, existing nesting/sub-graph
  execution, or break existing automated checks.

### Key Entities *(include if feature involves data)*

- **GraphLink annotation**: a node placed in a host graph that holds a reference to another graph plus an
  optional author note. It carries no execution semantics; it exists to express "this graph is associated with
  the host" and to be opened from the editor.
- **Editor navigation registry**: a mapping from a graph kind to the editor capable of opening it, populated by
  each library registering its own editor, and consulted when an author opens a referenced graph.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer who has never seen a given zone can, by opening its host graph, correctly list every
  quest/graph associated with that zone in under 30 seconds, with no need to read code or open other assets.
- **SC-002**: From a host graph, a developer reaches and views any referenced graph in a single action
  (one double-click), with the correct editor and asset loaded.
- **SC-003**: 100% of runs are identical whether or not annotation nodes are present (off-path), and a flow
  with an annotation wired on-path completes with the same outcome as without it.
- **SC-004**: Referencing a graph whose kind has no registered editor never produces an error; it always yields
  a graceful fallback plus a diagnostic.

## Assumptions

- This is an editor/authoring feature for developers building games on the ecosystem; it is not surfaced to end
  players and has no in-game UI.
- The association is one-directional for this slice (host graph → referenced graphs). A reverse "belongs-to"
  back-reference shown on the referenced graph is out of scope.
- "Open in the proper editor" relies on each domain library opting in by registering its editor; libraries that
  have not registered fall back gracefully rather than the core hard-coding them.
- The referenced graph is identified by its kind; the core provides the registry and open action but remains
  free of any knowledge of specific downstream libraries.
- Runtime execution/await of a referenced graph (a flow that hosts-and-waits-for a quest) is explicitly a
  separate, later feature and is NOT part of this slice.
