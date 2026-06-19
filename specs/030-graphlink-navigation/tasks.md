# Tasks: Visual GraphLink cross-reference + editor navigation

**Input**: Design documents from `/specs/030-graphlink-navigation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/graphlink-api.md, quickstart.md

**Tests**: INCLUDED — Constitution IV (TDD, NON-NEGOTIABLE) requires EditMode tests written and failing before
implementation.

**Organization**: by user story (US1 P1, US2 P2, US3 P3). graphcore = `com.faolline.graphcore`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on incomplete work)
- File paths are relative to `Assets/FaollineGraphEcosystem/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: folders for the new files.

- [ ] T001 Ensure target folders exist: `com.faolline.graphcore/Runtime/Nodes/`,
  `com.faolline.graphcore/Editor/Nodes/`, `com.faolline.graphcore/Editor/Registry/`,
  `com.faolline.graphcore/Editor/Styles/`, `com.faolline.graphcore/Tests/EditMode/Editor/`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the node-data type every story depends on. MUST complete before US1/US2/US3.

- [ ] T002 Create `GraphLinkNodeData : BaseNodeData` in
  `com.faolline.graphcore/Runtime/Nodes/GraphLinkNodeData.cs` — `const string NodeTypeId = "graphcore/graph-link"`,
  `public BaseGraph TargetGraph { get; set; }`, `public string Note { get; set; }`, XML docs stating it is a
  non-executing documentary reference. Adds NO fields to `BaseNodeData` (append-only respected).

**Checkpoint**: the node type exists and serializes a `BaseGraph` reference.

---

## Phase 3: User Story 1 — Read which graphs belong to a part of the game (Priority: P1) 🎯 MVP

**Goal**: opening a host graph shows GraphLink nodes as distinct, labelled "<Kind>: <Name>" references in EVERY
lib editor, without per-lib changes.

**Independent test**: add a `GraphLinkNodeData` (TargetGraph = a QuestGraph) to a graph, open it, see the
labelled reference; a null target renders "(missing target)" without error.

### Tests (write first, MUST fail)

- [ ] T003 [P] [US1] EditMode test in
  `com.faolline.graphcore/Tests/EditMode/Editor/GraphLinkNodeViewTests.cs`: a `BaseGraphView` building a graph
  that contains a `GraphLinkNodeData` produces a `GraphLinkNodeView`; its label shows the target's kind + name;
  a null `TargetGraph` yields a "(missing target)" label and no exception.

### Implementation

- [ ] T004 [US1] Create `GraphLinkNodeView : BaseNodeView` in
  `com.faolline.graphcore/Editor/Nodes/GraphLinkNodeView.cs` — render `"<Kind>: <Name>"` (Kind derived
  generically from `TargetGraph.GetType().Name`, stripping a trailing "Graph"; Name from display/asset name),
  or "(missing target)" when null; visually distinct from executable nodes. Load styling from USS (no inline CSS).
- [ ] T005 [P] [US1] Add `com.faolline.graphcore/Editor/Styles/GraphLinkNodeView.uss` — distinct annotation
  styling (e.g. dashed/secondary look) so the reference reads as documentation, not a step.
- [ ] T006 [US1] In `com.faolline.graphcore/Editor/Graph/BaseGraphView.cs`, make the node-build path create a
  `GraphLinkNodeView` for any `GraphLinkNodeData` BEFORE delegating to the per-lib abstract `CreateNodeView`, so
  GraphLink renders in every lib editor with zero per-lib code; also append an "Add GraphLink" entry to the
  shared canvas context menu.

**Checkpoint**: US1 independently testable — GraphLink references are visible/labelled in any editor.

---

## Phase 4: User Story 2 — Jump straight to the referenced graph (Priority: P2)

**Goal**: double-clicking a GraphLink opens the referenced graph in the right editor; graceful fallback when no
editor is registered.

**Independent test**: register an opener for a graph type, double-click a GraphLink of that type → opener fires;
double-click one of an unregistered type → ping/select + `[GraphCore]` diagnostic, no throw.

### Tests (write first, MUST fail)

- [ ] T007 [P] [US2] EditMode test in
  `com.faolline.graphcore/Tests/EditMode/Editor/GraphEditorWindowRegistryTests.cs`: `Register` + `Open` invokes
  the registered opener for a matching type (incl. via a base type); `Open` for an unregistered type and for
  `null` falls back gracefully (no throw) and logs a `[GraphCore]` diagnostic; `Clear` resets. Use `LogAssert`.

### Implementation

- [ ] T008 [US2] Create `GraphEditorWindowRegistry` (static) in
  `com.faolline.graphcore/Editor/Registry/GraphEditorWindowRegistry.cs` — `Register(Type, Action<BaseGraph>)`,
  `TryGetOpener(Type, out Action<BaseGraph>)`, `Open(BaseGraph)` (resolve by `GetType()` walking base types;
  fallback `Selection.activeObject` + `EditorGUIUtility.PingObject` + `[GraphCore]` diagnostic; never throws),
  `Clear()`. Null args ignored with a `[GraphCore]` warning.
- [ ] T009 [US2] In `GraphLinkNodeView` (`com.faolline.graphcore/Editor/Nodes/GraphLinkNodeView.cs`) wire a
  double-click (`MouseDownEvent`, `clickCount == 2`) → `GraphEditorWindowRegistry.Open(node.TargetGraph)`.
- [ ] T010 [P] [US2] Register `QuestGraph` → quest window in a new
  `com.faolline.graphquest/Editor/QuestEditorRegistration.cs` (`[InitializeOnLoadMethod]`).
- [ ] T011 [P] [US2] Register `DialogueGraph` → dialogue window in a new
  `com.faolline.graphdialoguesystem/Editor/DialogueEditorRegistration.cs` (`[InitializeOnLoadMethod]`).
- [ ] T012 [P] [US2] Register `GameFlowGraph` → gameflow window in a new
  `com.faolline.graphgameflow/Editor/GameFlowEditorRegistration.cs` (`[InitializeOnLoadMethod]`).

**Checkpoint**: US2 independently testable — double-click opens the target (or falls back) across the 3 libs.

---

## Phase 5: User Story 3 — The annotation never changes how the game runs (Priority: P3)

**Goal**: GraphLink is inert at runtime; on-path it is a no-op pass-through.

**Independent test**: a flow with a GraphLink off-path runs identically to one without it; a GraphLink wired
on-path passes straight through (no pause, same terminal state), TargetGraph untouched, no throw.

### Tests (write first, MUST fail)

- [ ] T013 [P] [US3] EditMode test in
  `com.faolline.graphcore/Tests/EditMode/GraphLinkRunnerPassThroughTests.cs`: (a) GraphLink off-path → run
  result + history identical to the same graph without it, target never entered; (b) GraphLink on-path
  (start → link → end) → run reaches `Ended`/`Completed` with no extra `NodeReady` pause and no exception;
  (c) GraphLink on-path with no outgoing edge → terminates like a dead-end (`Ended`).

### Implementation

- [ ] T014 [US3] In `com.faolline.graphcore/Runtime/Execution/BaseRunner.cs`, add a `GraphLinkNodeData` branch
  in the node-type dispatch so entering one runs no enter/exit actions and no executor and immediately advances
  along its outgoing edge (no `NodeReady` pause); no outgoing edge → `Ended`/`EndReason.Completed`. Existing
  SubGraph/End/regular-node behaviour and all current tests stay unchanged.

**Checkpoint**: US3 independently testable — zero runtime impact proven.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T015 [P] Bump `com.faolline.graphcore/package.json` MINOR (new node type + editor registry) and add a
  CHANGELOG entry in `com.faolline.graphcore/CHANGELOG.md`.
- [ ] T016 [P] Bump graphquest / graphdialoguesystem / graphgameflow PATCH + CHANGELOG entries, and align each
  one's `com.faolline.graphcore` floor to the new graphcore version (floor-alignment convention).
- [ ] T017 [P] Update `com.faolline.graphcore/README.md` with a short GraphLink section (documentary cross-ref,
  non-executing, double-click navigation, lib registration) referencing `quickstart.md`.
- [ ] T018 Run the full EditMode cert via Unity batchmode (`-runTests -testPlatform EditMode`, no `-quit`),
  confirm all suites green (incl. the 3 new tests) and zero new console errors.

---

## Dependencies & Story Completion Order

- **Setup (T001)** → **Foundational (T002)** blocks everything.
- **US1 (T003–T006)**, **US2 (T007–T012)**, **US3 (T013–T014)** all depend only on T002 and are otherwise
  independent — they can be built/tested in any order after Foundational.
- US2's double-click wiring (T009) depends on the view existing (T004) and the registry (T008); within US2,
  T008 before T009; T010–T012 are independent of each other and of T009 (they only call `Register`).
- **Polish (T015–T018)** after the stories it documents/versions.

**Recommended order**: T001 → T002 → US1 (MVP) → US3 (runtime safety) → US2 (navigation) → Polish.
(US3 before US2 is suggested so the runtime guarantee lands early; either order is valid.)

## Parallel Execution Examples

- After T002: T003, T007, T013 (the three story tests) can be written in parallel — different files.
- US2 registrations T010, T011, T012 in parallel — three different lib editor files.
- Polish T015, T016, T017 in parallel — different package/doc files.

## Implementation Strategy

- **MVP = US1** (T001–T006): a developer can open a host graph and read which quests/graphs belong to it. This
  alone delivers the core onboarding value, even before navigation or the runtime guarantee.
- Then **US3** (the "it never affects the game" promise) and **US2** (click-to-open) layer on independently.
- TDD throughout: write each phase's test (T003/T007/T013), confirm it fails for the right reason, then
  implement.
