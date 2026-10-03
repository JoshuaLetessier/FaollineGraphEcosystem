# Feature Specification: Full Graph-Stack Save & Restore

**Feature Branch**: `053-graph-stack-snapshot`

**Created**: 2026-10-03

**Status**: Ready for planning — decisions D1–D4 answered by the user on 2026-10-03 (all option A); D5 (names) not answered, candidates adopted provisionally (see end of document)

**Input**: User description: "Full graph-stack save/restore for GraphRunSnapshot. Problem: GraphRunSnapshot only records the TOP execution frame (GraphId + CurrentNodeId). A consumer (Cryptique) runs a chapter graph as the ROOT of a GraphFlowDriver; its zones are sub-graphs via SubGraphNodeData (InheritParentContext = true, OpensScope = false), e.g. EverfrostFlow -> SubGraph -> ForetFlow. It saves on every OnNodeEntered of an IsCheckpoint node or a StartNodeData (including a sub-graph's Start) with GraphRunSnapshot.Capture(driver.Runner, driver.Context). A save taken inside ForetFlow restores with ForetFlow as root: at its End the whole run ends instead of returning into EverfrostFlow; and the consumer has to resolve that sub-graph by GraphId (a hand-placed Addressables label per graph), although it can already load the ROOT graph itself (chapter registry). Wanted: (1) Capture records the stack root->top (GraphId + current node per frame) and exposes the root graph id; GraphId/CurrentNodeId stay the top frame's. (2) Restore: the consumer supplies the root graph (the lib never resolves it); the lib walks down frame by frame through the current SubGraph node's TargetGraph, checking every reached graph has the saved GraphId and every node exists; only the top frame's node is re-entered; parent frames sit on their SubGraph node without replaying their enter actions; afterwards everything behaves like a normally built stack. (3) GraphFlowDriver.Boot(snapshot) restores the stack when the snapshot has one (driver.Graph = root). (4) Non-breaking: existing API unchanged; a snapshot without stack restores exactly as today; format stays JsonUtility- and savesystem-compatible; docs/CHANGELOG/versions updated, 'top-level only' wording lifted or narrowed. No migration of existing saves (none exist). Agreed corrections: stack walk/validation/frame building in graphcore, graphsave only serializes, graphgameflow consumes; whole-path validation before any mutation; validation rule per frame; repeated graph in a saved path reported as an inconsistency; side-effect-free validation callable before Boot plus loud failure; a stacked snapshot against a non-root graph fails explicitly; capture never throws; restored run uses the root's history settings, rewires editor tooling, starts with empty history; world state built by parent frames is consumer territory. Open decisions (options + recommendation, not decided): fresh-context frames, OpensScope overlay values, content changed between save and load, DialoguePlayer now or later, API names."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resume a save taken inside a sub-graph, within its enclosing graphs (Priority: P1)

A game runs a chapter graph that enters zone graphs as sub-graphs, and saves at checkpoints — including at the start of each zone. When the player loads a save taken inside a zone, the game supplies only the chapter (root) graph, which it already knows how to load. The run resumes at the saved point inside the zone; when the zone ends, the run returns into the chapter graph and continues there, exactly as if the player had never quit.

**Why this priority**: This is the request. Today a save taken inside a zone resumes that zone as if it were the whole game — the zone's end terminates the run — and the game has to be able to locate every zone graph by its identifier just to load a save.

**Independent Test**: Chapter graph C enters zone Z through a sub-graph step. Save at Z's first step, convert the save to text and back, restore it from C alone: the active step is Z's first step; finishing Z continues in C on the step after the sub-graph step, and the run ends at C's end.

**Acceptance Scenarios**:

1. **Given** a run two levels deep (C → Z) saved on a step inside Z, **When** it is restored from C, **Then** the active step is the saved step in Z, and the save names C as its root graph.
2. **Given** a run three levels deep (C → Z → R) saved on a step inside R, **When** the save is converted to text and back and restored from C, **Then** the active step is the saved step in R; when R ends the run continues in Z, and when Z ends it continues in C.
3. **Given** a restored run, **When** it resumes, **Then** only the saved step re-runs its entry checks and entry effects (as a single-level resume does today); the sub-graph steps the enclosing graphs are paused on do NOT re-run theirs.
4. **Given** a save taken while the run sits on a sub-graph step before entering it (a checkpoint on that step), **When** it is restored, **Then** the run resumes on that step and entering the sub-graph then proceeds normally.
5. **Given** any restored run, **When** it resumes, **Then** the run's values (variables, collections, signals already raised) are those of the save.
6. **Given** a save taken while no sub-graph was active, **When** it is restored from the root, **Then** the outcome is identical to today's.

---

### User Story 2 - A save that no longer matches the content is reported, never guessed (Priority: P1)

Between saving and loading, content can change (a step deleted, a sub-graph step retargeted, a graph swapped), or the game can supply the wrong root graph. The game can check a save against the root graph before starting anything, and learns why and at which level it does not match. If the game starts the restore anyway, the failure is loud and nothing is modified.

**Why this priority**: Without it, a mismatched save yields a frozen or wrong run with no signal — a failure the game cannot detect, let alone handle. It ships together with User Story 1.

**Independent Test**: For each kind of mismatch, the check returns a failure with its reason and level; a restore attempted anyway leaves the run and the game's values untouched.

**Acceptance Scenarios**:

1. **Given** a save whose root graph identifier differs from the supplied root graph, **When** it is checked, **Then** the check reports "wrong root graph"; **and when** it is restored anyway, **Then** nothing is modified and an error is reported.
2. **Given** a save whose saved step, at some level, no longer exists in that level's graph, **When** it is checked, **Then** the check reports "step missing" with that level.
3. **Given** a save where an enclosing level's saved step is no longer a sub-graph step, or its sub-graph target is empty, or targets a graph whose identifier differs from the next saved level's, **When** it is checked, **Then** the check reports the matching reason with that level.
4. **Given** a save whose path names the same graph twice (only possible for a corrupted or hand-edited save), **When** it is checked, **Then** it is reported through the same channel as the other mismatches, not as a crash.
5. **Given** any mismatch, **When** a restore is started anyway (including through the game-flow driver), **Then** an error naming the reason is reported, the run does not start, and the game's values are not partially overwritten.
6. **Given** a valid save, **When** it is checked, **Then** the check reports success and has no side effect on the run, the graphs or the values.

---

### User Story 3 - The game-flow driver's load path restores the whole stack (Priority: P2)

A game that boots its game-flow driver from a save gets the full stack restored, with the driver's assigned graph acting as the root — without changing how it calls the driver.

**Why this priority**: This is how the consumer actually loads a game. It builds on User Stories 1 and 2.

**Independent Test**: A driver with root C assigned boots from a save taken in Z: the driver reports running, the active step is in Z, and finishing Z continues in C with the driver's own notifications firing as in an uninterrupted run.

**Acceptance Scenarios**:

1. **Given** a driver whose graph is C and a save taken in Z, **When** the driver boots from the save, **Then** the active step is the saved step in Z, and the run continues into C after Z ends.
2. **Given** a driver whose graph is not the save's root, **When** it boots from the save, **Then** the driver stays inert, reports an error naming both graph identifiers, and the supplied values are not modified.
3. **Given** a save without a stack, **When** the driver boots from it, **Then** the driver behaves exactly as today.
4. **Given** a boot from a save requested from inside one of the driver's own notifications (deferred today), **When** it is carried out, **Then** the outcome is the same as a direct boot.

---

### User Story 4 - Existing saves and code keep working (Priority: P2)

Saves built without a stack (through the capture that takes explicit identifiers) and every existing save/restore operation keep their behavior; the save stays convertible to text with both supported serializers.

**Why this priority**: The library is consumed by several packages and projects; the new capability must be purely additive.

**Independent Test**: The pre-existing save/restore tests pass unchanged; a save without stack information reads back as "no stack" and restores as before.

**Acceptance Scenarios**:

1. **Given** a save built without a stack, **When** it is restored into a graph, **Then** the run resumes in that graph at the saved step, exactly as today.
2. **Given** existing code that reads the save's graph identifier and current step, **When** it reads a stacked save, **Then** both still name the deepest (active) level.
3. **Given** a stacked save, **When** it is converted to text and back with the engine's built-in serializer and with the save-system backend, **Then** the path is intact; a save without stack information reads back as "no stack".

---

### User Story 5 - Sub-graphs that do not simply share the enclosing graph's values (Priority: P3)

A sub-graph step runs its target in one of three ways: sharing the enclosing values (the consumer's case); sharing them plus a temporary local layer discarded when the sub-graph ends; or with a separate, fresh set of values. A restored run must rebuild each level's way of holding values, so that ending a sub-graph behaves as in an uninterrupted run.

**Why this priority**: The consumer only uses the shared mode today; the other two must not be silently broken (decided: D1-A, D2-A).

**Independent Test**: One test per mode: restore a two-level save, read and write values in the restored sub-graph, end it, and compare with an uninterrupted run.

**Acceptance Scenarios**:

1. **Given** a sub-graph sharing the enclosing values, **When** restored, **Then** it reads and writes the same values as the enclosing level.
2. **Given** a sub-graph with a local layer, **When** restored, **Then** the local layer exists again: its declared local values are readable, values written to undeclared keys stay local and vanish when the sub-graph ends, and values written into the layer before the save are not restored (the layer restarts from its graph's declared defaults).
3. **Given** a sub-graph with fresh values saved on its entry step, **When** restored, **Then** its values are its graph's declared defaults plus the replayed entry effects — the same as an uninterrupted run — and no error was reported at capture.
4. **Given** a sub-graph with fresh values saved past its entry step (or enclosing a deeper level), **When** captured, **Then** an error naming that level is reported without interrupting the run; **when** restored, **Then** that level restarts from its graph's declared defaults.

---

### Edge Cases

- A save taken while the active step waits for a signal or a timer → the restore re-enters the step and the wait starts over; the remaining time is not preserved (as today).
- A save captured before the run started (no active level) → no stack, no step: the restore starts at the supplied graph's entry (as today).
- A save captured after the run ended → its path ends on the step where the run ended (the root's end step, or a step with no way out at any level); restoring re-enters that step and the run ends again (as today).
- The game saves from inside the "step entered" notification → capturing must never interrupt the running graph, in any state.
- Restoring re-fires the "step entered" notification for the resumed step, so a game that saves on that notification immediately writes an equivalent save — harmless, as today.
- Two nested sub-graphs that both open a local layer → the engine holds one local layer at a time (an inner layer replaces the outer one); the restore reproduces exactly the layering of an uninterrupted run, no more.
- A stacked save restored against its deepest graph instead of its root → "wrong root graph" (no fallback that treats the supplied graph as the deepest level). A single-level save's root and deepest graph are the same graph.
- A sub-graph step whose way of holding values (shared / local layer / fresh) changed between save and load → reported as a mismatch, like a retargeted sub-graph step.
- History after a restore is empty: going back cannot rewind past the load point (as today); the history depth limit is the root graph's.
- World state that enclosing levels built before their sub-graph step (for example loaded scenes) is not rebuilt by the restore — the game's responsibility, as today.
- The editor's live-run tooling shows a restored run (every level) exactly like a normally started one.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Capturing a run MUST record, for every active level from the root to the deepest, that level's graph identifier, the step it is on, and how it holds values (shared / local layer / fresh), in order.
- **FR-002**: A save MUST expose its root graph identifier and whether it carries a stack. A save without a stack names its own graph identifier as its root (the only graph it can be restored into).
- **FR-003**: A save's existing graph identifier and current step MUST keep naming the deepest active level.
- **FR-004**: Capturing MUST never interrupt the run: it MUST NOT fail by throwing, whatever the run's state; problems found while capturing are reported as errors.
- **FR-005**: Restoring a stacked save MUST take the root graph from the caller; the libraries MUST NOT locate any graph by identifier themselves.
- **FR-006**: A restore MUST reach every deeper level only through the target of the enclosing level's sub-graph step, and MUST verify that: the supplied root's identifier equals the saved root's; each enclosing level's saved step exists, is a sub-graph step, has a non-empty target whose identifier equals the next level's saved identifier, and still holds values the way the save recorded; the deepest level's saved step exists; no graph appears twice in the path.
- **FR-007**: Verification MUST complete before any change to the run or to the values; a failed verification MUST leave both untouched (all or nothing).
- **FR-008**: A side-effect-free check MUST be available that takes a save and a root graph and returns success, or a failure carrying its reason, the level where it occurs and the deepest level that is still valid (D3-A). The libraries MUST NOT fall back to any partial resume on their own; choosing a fallback is the game's decision.
- **FR-009**: Every restore entry point, including the game-flow driver's, MUST report a failed verification loudly (error) — never a silently stuck or silently wrong run.
- **FR-010**: After a restore, only the deepest level's step MUST be re-entered (entry checks, entry effects, step behavior, notifications), as a single-level resume does today; the enclosing levels' sub-graph steps MUST NOT be re-entered.
- **FR-011**: A restored run MUST then behave as one built by uninterrupted play: ending a sub-graph returns into the enclosing level and continues along its sub-graph step's outgoing connections; ending the root ends the run.
- **FR-012**: A restore MUST rebuild each level's way of holding values: a shared level uses the enclosing level's values; a local-layer level reopens its layer seeded with its graph's declared defaults, and values written into the layer before the save are not restored (D2-A, documented, no capture message); a fresh level is recreated from its graph's declared defaults (D1-A). Deeper shared levels share whatever their enclosing level holds.
- **FR-012a**: Capturing MUST report an error (without throwing, FR-004) naming the level whenever a fresh-values level's values would be lost by FR-012 — that is, when a fresh-values level is an enclosing level, or is the deepest level and not on its graph's entry step. A fresh-values level that is the deepest level and sits on its graph's entry step is captured silently (defaults plus the replayed entry effects reproduce its state exactly).
- **FR-013**: A restored run MUST use the root graph's history settings, start with an empty history, and be visible to the editor's live-run tooling like a normally started run.
- **FR-014**: A save without a stack MUST restore exactly as today, through every existing entry point.
- **FR-015**: A stacked save restored against a graph other than its root MUST fail per FR-008/FR-009; the supplied graph MUST NOT be treated as the deepest level instead.
- **FR-016**: The game-flow driver's existing boot-from-save entry MUST restore the full stack when the save has one, with the driver's assigned graph as the root, applying FR-006 to FR-011; on failure it stays inert, reports an error and leaves the supplied values unchanged.
- **FR-017**: The save MUST stay convertible to text and back with the engine's built-in serializer and with the save-system backend; missing stack information MUST read back as "no stack".
- **FR-018**: Every existing public operation MUST keep its signature, and its behavior for saves without a stack.
- **FR-019**: Each touched package's documentation MUST lift or narrow the "top-level only" restriction, state the remaining limits (D1, D2), and the graph-catalog seam documentation MUST state that the root graph identifier is the key a game resolves (only root graphs need to be resolvable).
- **FR-020**: The dialogue player's own save/restore keeps its single-level behavior in this change (D4-A); its documentation MUST state the limit and point to the new graph-core capability as the way to lift it later.

### Key Entities

- **Save (run snapshot)**: The existing serializable record of a run — values, collections, raised signals, deepest graph identifier and step — extended with the run path.
- **Run path**: The ordered list of active levels from the root to the deepest; each level holds a graph identifier, the step it is on, and how it holds values.
- **Root graph**: The outermost graph of a run; supplied by the game at restore time, never located by the libraries.
- **Restore check result**: Success, or a failure with a reason (wrong root graph, step missing, not a sub-graph step, empty sub-graph target, sub-graph target mismatch, value-holding mode changed, repeated graph) the level where it occurs, and the deepest level that is still valid.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For saves taken at depth 1, 2 and 3, 100% resume at the saved step and then visit the same sequence of steps, through every enclosing level up to the root's end, as an uninterrupted run from the same point.
- **SC-002**: 0 entry effects of enclosing levels are replayed by a restore.
- **SC-003**: 100% of the mismatch kinds listed under Restore check result are reported with their reason and level, and in 100% of failures the run and the values are unchanged afterwards.
- **SC-004**: Restoring any save performs 0 lookups of a graph by identifier inside the libraries; a game needs to make only its root graphs resolvable.
- **SC-005**: 100% of the pre-existing save/restore tests pass without modification.
- **SC-006**: A stacked save round-trips through text with both supported serializers with 0 loss of path information.

## Assumptions

- No existing save needs migrating (confirmed by the user, 2026-10-03). "Without a stack" compatibility is about the existing API and the identifier-only capture, not about stored data.
- The consumer currently uses only the shared-values mode; the local-layer and fresh modes are covered for correctness, not driven by a present need.
- Sub-graph step targets are direct references available in player builds; editor-only cross-graph reference steps are passed through by the run and never form a level.
- Graph identifiers are unique per graph, as runtime cycle detection already assumes.
- A save captured from a running graph now carries its path, so restoring it into a graph whose identifier differs from the saved root fails (FR-015), even for a single-level save; the identifier-only capture keeps today's lenient behavior. Acceptable because no saves exist and restoring into a different graph was never a supported use.
- Waiting state (awaited signal, remaining timer) is not preserved beyond re-entering the step, as today.
- World and scene state outside the graph run stays the game's responsibility.
- Packages: graphcore (new public capability to read, check and restore a run path — the run's internals are private), graphsave (path serialization and restore entry points), graphgameflow (driver boot), graphdialoguesystem (documentation of its single-level limit only, D4-A). Each package gets a minor version bump, CHANGELOG and README updates, and dependency floors are synced.

## Decisions *(answered by the user on 2026-10-03: D1-A, D2-A, D3-A, D4-A; D5 unanswered — candidates adopted provisionally, renamable before merge)*

### D1: Sub-graphs with fresh values

**Context**: A sub-graph step that neither shares the enclosing values nor opens a local layer runs its target on a separate, fresh set of values. The game captures the root's values; the fresh level's values are not in the save — today they are already lost, silently.

| Option | Answer | Implications |
|--------|--------|--------------|
| **A — chosen** | Documented limit: restore recreates the fresh level from its graph's declared defaults; capture reports an error (never throws) naming the level whenever that loses data — i.e. the fresh level is an enclosing level, or the deepest level not on its graph's entry step | Exact for saves taken at a fresh sub-graph's entry step (defaults + replayed entry effects = the real state); loud elsewhere; smallest change; per-level value capture can be added later without breaking the format |
| B | Capture and restore the values of every fresh level in the save | Complete; larger save format and a public way to read each level's values; no present user |
| C | Refuse: the check fails ("fresh-values level not restorable") for any save containing a fresh level; capture reports an error | Never wrong; such saves become unloadable, even at entry steps where they would be exact |

### D2: Local-layer values

**Context**: Reopening the local layer is mandatory either way — otherwise reading a local key fails and writes to undeclared keys leak into the durable values. The open point is only whether values written into the layer are restored. Local values are already excluded from saves by design (they are transient).

| Option | Answer | Implications |
|--------|--------|--------------|
| **A — chosen** | Reopen the layer seeded with its graph's declared defaults; values written into the layer before the save are not restored (documented, no capture message) | Exact for saves taken at the sub-graph's entry step (the consumer's pattern); consistent with today's "local values are transient" rule |
| B | Also capture and restore the layer's values | Exact anywhere; extends the save format and the value-holder's public surface for a mode no one uses yet |

### D3: Content changed between save and load

**Context**: Step deleted, sub-graph step retargeted or changed mode, graph replaced, wrong root supplied.

| Option | Answer | Implications |
|--------|--------|--------------|
| **A — chosen** | Explicit failure, all or nothing; the report carries the reason, the failing level and the deepest valid level, so the game can choose its own fallback | Nothing guessed; the game keeps full control (e.g. offer to restart the zone) |
| B | Automatically resume at the deepest valid level, with a warning | Resuming there re-enters its sub-graph step and replays the sub-graph from its start: a guessed outcome, possibly replaying effects |
| C | Explicit failure; the report carries the reason and failing level only | As A, minus the information a game needs to build a fallback |

### D4: Dialogue player save/restore

**Context**: The dialogue player has its own save record, in its own package, with the same single-level limit. The consumer has no current need.

| Option | Answer | Implications |
|--------|--------|--------------|
| **A — chosen** | Later: document the limit now; adopting the new graph-core capability becomes a small follow-up | Smaller, focused change now |
| B | Now: extend the dialogue save record and restore the same way | One more package and test suite in this change |

### D5: Names of the new public operations

**Context**: Naming only — decided before the plan. Candidates (graphcore, then graphsave):

| Concept | Candidate |
|---------|-----------|
| Read the active path, root → deepest | `BaseRunner.GetExecutionPath()` returning a list of `GraphRunFrame` (graph id, node id, context mode) |
| Side-effect-free check | `GraphRunPath.TryResolve(rootGraph, frames, out resolved, out GraphRunPathError error)` |
| Restore a path | `BaseRunner.StartFromPath(rootGraph, frames, context, registry)` |
| Save fields | `GraphRunSnapshot.Frames`, `HasStack`, `RootGraphId` (computed from `Frames[0]`, falls back to `GraphId`) |
| Snapshot check | `GraphRunSnapshot.TryValidate(rootGraph, out error)` |
