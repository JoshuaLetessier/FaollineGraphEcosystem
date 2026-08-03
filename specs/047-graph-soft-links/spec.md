# Feature Specification: Break Hard Graph-to-Graph Asset References (Soft Graph Links)

**Feature Branch**: `047-graph-soft-links`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: "Break hard asset references between graphs so a root graph load does not pull in the full transitive closure of referenced graphs and their actions/conditions/defs. (1) GraphLinkNodeData (com.faolline.graphcore) — a documentary, non-executing cross-reference node — currently holds a hard BaseGraph reference, forcing the target graph into the build/bundle closure for zero runtime value. Replace it with a soft GUID reference with an editor drawer preserving drag-and-drop and navigation. No real project uses this deeply, so no data migration is needed. (2) Add an IGraphCatalog port to com.faolline.graphgameflow, mirroring ISceneLoader, resolving GraphId -> BaseGraph asynchronously via a pluggable adapter — independently required by com.faolline.graphsave. (3) Add a GraphKeySourceRegistry editor tool mirroring SceneKeySourceRegistry. (4) Add an Addressables adapter (AddressablesGraphCatalog, AddressablesGraphKeyProvider, PreloadNextChapterAction) in com.faolline.graphgameflow.addressables. Constraints: SubGraphNodeData keeps its hard reference unchanged; BaseRunner stays synchronous/headless/engine-free; graphcore never depends on com.unity.addressables. Also required: a validator rule flagging an unresolved soft GraphLinkNodeData target (checked by key, not just resolved reference), and a second validator rule flagging a SubGraphNodeData that crosses into a registered chapter-root graph (which would silently reintroduce the full pull). Acceptance requires a real Addressables Build + Analyze verification, not just code inspection."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Annotate a graph without paying for it (Priority: P1)

An author places a documentary, non-executing cross-reference on a graph — e.g. "this zone's flow is associated with this quest chapter" — purely so the relationship is visible and navigable in the editor. Today, doing this silently drags the entire referenced graph (and everything *it* references) into the same build/bundle inclusion group as the annotating graph, even though the reference is never touched at runtime. The author needs this annotation to cost nothing at build time.

**Why this priority**: This is the one change with no tradeoff — it helps every project that uses this annotation mechanism, whether or not the project ever adopts chaptering or asynchronous asset loading. It is also the prerequisite for the rest of the feature to mean anything (Lots 2-4 assume this reference is already soft).

**Independent Test**: Create a graph A with a documentary reference to graph B in an isolated test project. Confirm A's build dependency report no longer lists B. Confirm the author can still assign the reference by pointer-based selection and navigate from A to B in the editor exactly as before.

**Acceptance Scenarios**:

1. **Given** a graph with a documentary reference to another graph, **When** the project is inspected via the platform's build dependency report, **Then** the referenced graph does not appear as a dependency of the referencing graph.
2. **Given** an author editing a graph's documentary reference field, **When** they assign or change the target by pointer-based selection (drag-and-drop), **Then** the assignment succeeds and the field displays/links to the chosen target exactly as it did before this change.
3. **Given** a documentary reference whose recorded target no longer resolves to any asset, **When** the project's authoring-time validation runs, **Then** a warning is raised identifying the broken reference.

---

### User Story 2 - Resolve a graph identifier to an asset, independent of loading technology (Priority: P1)

A project with more than one independently-loadable root graph (e.g. one per game chapter) needs, at runtime, to turn a stable graph identifier into the concrete graph asset it refers to — most immediately because restoring a saved run only carries that identifier, not the asset itself, and the calling code should not have to maintain its own ad hoc lookup table to bridge the gap.

**Why this priority**: This capability is required by the existing save/restore feature the moment a project has more than one root graph, entirely independent of whether the project ever adopts any particular asset-loading technology. It is also the foundation Lot 3 and Lot 4 build on.

**Independent Test**: In a headless test with no asset-loading technology installed, register a simple resolver that maps a couple of identifiers to in-memory graph assets, then confirm a save/restore round trip resolves the correct asset purely from its identifier.

**Acceptance Scenarios**:

1. **Given** a project with two independently-loadable root graphs and a saved run tagged with one graph's identifier, **When** the run is restored, **Then** the correct graph asset is resolved from the identifier alone, without the caller supplying it directly.
2. **Given** a project with zero asset-loading technology installed, **When** it registers its own minimal identifier-to-asset resolver, **Then** the resolution mechanism works correctly with no missing capability and no forced dependency on any particular technology.

---

### User Story 3 - Mark a graph as a chapter entry point from an editor tool (Priority: P2)

An author, working in the graph editor, wants to see which string keys are already available, where each one currently resolves to (or that it doesn't yet), and mark a given graph asset as the graph that a particular key should resolve to — the same authoring experience already available for scene keys.

**Why this priority**: Pure editor ergonomics on top of User Story 2 — valuable, but the runtime resolution capability (US2) is useful on its own even before this tool exists, since keys can be registered by other means (code, config) in the meantime.

**Independent Test**: Open the tool against a project containing a handful of graph assets and confirm the key dropdown, source label, and "mark as" action all behave the same way their scene-key equivalents already do.

**Acceptance Scenarios**:

1. **Given** an author viewing a graph asset in the editor, **When** they open the graph-key assignment tool, **Then** they see the list of currently known keys and which, if any, already resolves to this asset.
2. **Given** a graph asset not yet associated with any key, **When** the author uses the "mark as" action for a chosen key, **Then** that key now resolves to this asset for subsequent lookups.

---

### User Story 4 - Preload the next chapter ahead of time (Priority: P3)

In a project that has adopted an asynchronous asset-loading technology, an author wants to trigger the asynchronous load of the next chapter's graph early — well before the current chapter ends — so that by the time the current flow finishes, the next chapter's graph is already available and can be handed to the runner without a visible pause, all without the current chapter's build ever depending on the next one's content.

**Why this priority**: Highest-value payoff of the feature end-to-end, but it only applies to projects that use the relevant asset-loading technology, and it is built entirely on top of User Stories 1-2. Projects that don't adopt this technology are unaffected either way.

**Independent Test**: In a project with the relevant asset-loading technology installed, trigger the preload action early in a chapter, let the chapter run to completion, and confirm the next chapter's graph is already loaded and ready to hand to the runner with no additional wait, while the build dependency report confirms the current chapter never depended on the next one's content.

**Acceptance Scenarios**:

1. **Given** a chapter graph with an early preload trigger for the next chapter, **When** the chapter reaches its end, **Then** the next chapter's graph is already resolved and ready, with no runtime wait for its load to begin.
2. **Given** the same setup, **When** the project's build dependency report is inspected, **Then** the current chapter's build inclusion group does not contain the next chapter's content.

---

### Edge Cases

- What happens when a documentary reference's recorded target has been deleted or was never assigned? → Authoring-time validation must flag it; at runtime the reference is never touched regardless, so nothing breaks silently at play time — the risk is purely a dangling authoring artifact.
- What happens when a documentary reference's target is recorded only by key/string and that key doesn't match any current asset (e.g., a mis-filed asset renamed or moved outside the tracked location)? → The same authoring-time validation must catch this by key, since standard dependency-scanning only sees resolved hard references and would report nothing wrong.
- What happens when an author uses the hard sub-flow invocation mechanism to jump into a graph that has itself been designated as an independently-loadable chapter entry point? → Authoring-time validation must flag this specifically, since it silently reintroduces the exact build-time pull this feature exists to eliminate, and would otherwise pass every other check.
- What happens when a documentary reference points back at a graph that (directly or transitively) documentary-references the original graph (a reference cycle)? → Since the reference is never traversed at runtime and authoring-time validation only needs to check each reference's target in isolation, a cycle must not cause the validation pass itself to loop or fail.
- What happens when two different graph assets are both marked (accidentally) as the entry point for the same key? → Authoring-time tooling must surface this as a conflict rather than silently letting the later assignment win invisibly.
- What happens when a project restores a saved run whose graph identifier doesn't resolve to any known asset (e.g., content removed since the save was made)? → Resolution must fail in a way the caller can detect and handle, not silently proceed with a wrong or null graph.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an author to record a documentary, non-executing cross-reference from one graph to another such that the referenced graph is not included in the same build/asset-bundle inclusion group as the referencing graph purely because of that reference.
- **FR-002**: The authoring experience for creating, changing, and navigating a documentary cross-reference MUST remain pointer-based (drag-and-drop or equivalent single-action selection) — no regression in the number of steps or the affordance compared to today.
- **FR-003**: The existing hard-reference mechanism for one graph to directly invoke another as a nested sub-flow MUST remain completely unchanged in behavior, reference type, and authoring ergonomics — this feature must not alter it.
- **FR-004**: System MUST provide a way to resolve a stable graph identifier to its concrete graph asset at runtime through a swappable seam, independent of any specific asset-loading technology, so a project with more than one independently-loadable root graph can look up "which asset does this identifier mean."
- **FR-005**: This resolution seam MUST be usable to support restoring a saved run by identifier alone, without the calling code maintaining its own separate lookup table.
- **FR-006**: The resolution seam MUST function correctly in a project with no asynchronous asset-loading technology installed at all (e.g. backed by a simple direct-reference implementation), imposing no mandatory dependency on any particular technology.
- **FR-007**: System MUST provide an editor tool letting an author see currently known graph keys, which asset (if any) each currently resolves to, and mark a chosen graph asset as the resolution target for a given key.
- **FR-008**: System MUST detect and surface, at authoring time, a documentary cross-reference whose recorded target cannot currently be resolved to any asset (missing, deleted, or never assigned).
- **FR-009**: The detection in FR-008 MUST also catch a target recorded purely by key/string that fails to resolve, not only a previously-resolved-then-broken asset reference, since standard dependency-scanning tools cannot see a key-based miss.
- **FR-010**: System MUST detect and surface, at authoring time, a case where the hard sub-flow invocation mechanism (FR-003) targets a graph that has been designated (FR-007) as an independently-loadable entry point, since this would silently reintroduce a full build-time pull the feature is meant to prevent.
- **FR-011**: For a project that has adopted an asynchronous asset-loading technology, System MUST additionally support triggering an early, asynchronous load of a target graph identified only through the resolution seam (FR-004), so the target can be ready ahead of when it's needed without ever being a build-time dependency of the graph that triggers the load.
- **FR-012**: None of the above capabilities MUST require the graph execution engine itself to become asynchronous, to gain new run-time states, or to take on a dependency on any specific asset-loading technology — it must remain fully synchronous, headless, and testable exactly as it is today, with or without such a technology present.

### Key Entities

- **Documentary Graph Reference**: A non-executing, authoring-only pointer from one graph to another (plus an optional note), used to make a conceptual relationship visible and navigable without affecting execution or build inclusion.
- **Graph Identifier**: A stable string identity for a graph asset, carried by saved run data and by authoring tools, independent of how — or whether — the asset is loaded asynchronously.
- **Graph Catalog**: The runtime seam that resolves a Graph Identifier to a concrete graph asset; swappable per project, with a trivial direct-reference implementation available when no asynchronous asset-loading technology is in use.
- **Graph Key Registry** *(editor-only)*: The authoring tool that lets a developer inspect known keys and mark a graph asset as the entry point for one of them.
- **Sub-Flow Invocation**: The existing, unchanged hard-reference mechanism by which one graph directly runs another as a nested part of its own execution.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A build containing a root graph and a separately-located, documentary-referenced graph shows zero content from the referenced graph in the root graph's build/bundle inclusion group, confirmed via the platform's build dependency analysis tooling (not by reading source).
- **SC-002**: Assigning, changing, or navigating a documentary reference takes the same number of user actions after this change as before it (zero added steps).
- **SC-003**: A project with multiple independently-loadable root graphs can restore a saved run by identifier alone, with zero caller-maintained lookup code required.
- **SC-004**: 100% of the two new authoring-time mistake patterns this feature introduces protection for (an unresolved documentary/key-based reference; a hard sub-flow invocation crossing into a registered chapter entry point) are caught by an authoring-time warning in a representative test project before ever reaching a build.
- **SC-005**: The existing automated test suite covering the unchanged hard sub-flow invocation mechanism passes with zero regressions.
- **SC-006**: The identifier-resolution mechanism passes its automated tests in an environment with zero asynchronous asset-loading technology installed.
- **SC-007**: For a project using an asynchronous asset-loading technology, an early-triggered preload of the next chapter completes (asset ready) before the current chapter's flow reaches its end, verified in a representative test scenario.

## Assumptions

- No existing project has authored enough content against the current documentary-reference field to require a compatibility or versioned-migration path; this is a direct, one-time replacement rather than a staged migration (explicitly confirmed by the requester).
- "Independently-loadable root graph" and "chapter" are used interchangeably in this spec; the boundary between them is authoring-designated (via the Graph Key Registry), not structurally inferred from graph content.
- The early-preload capability (User Story 4 / FR-011) is scoped to projects that already use an asynchronous asset-loading technology; projects that don't are entirely unaffected by it and pay no cost, code, or dependency for its existence.
- The two new authoring-time checks (FR-008-010) extend the project's existing authoring-time validation tool rather than introducing a separate, standalone tool.
- The graph execution engine's synchronous, headless, engine-agnostic contract is a hard constraint carried over unchanged from the existing system, not something this feature negotiates.
