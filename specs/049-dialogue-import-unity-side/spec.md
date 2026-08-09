# Feature Specification: Dialogue Graph Generation from a Pivot Interchange Format

**Feature Branch**: `049-dialogue-import-unity-side`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "Unity-side generation of playable DialogueGraph assets from a dedicated dialogue pivot interchange format — the Unity/library half of Part 2 of the quest-data-import initiative. The external authoring tool that will eventually produce this format is out of scope; only the consuming side (inside com.faolline.graphimport, plus a small necessary addition to com.faolline.graphdialoguesystem) is built now, so the pipeline is ready the moment that tool exists."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a playable dialogue from the interchange format (Priority: P1)

A content pipeline (standing in for the future external authoring tool) provides a dialogue's structure — speakers, lines, player choices, and an ending — in the dedicated interchange format. The system builds a real, playable dialogue graph from it, with dialogue text already positioned to flow into the existing localization pipeline with no extra step.

**Why this priority**: This is the entire point of the feature — without it there is nothing to test independently, and every other story only adds structure on top of a working single-dialogue generation path.

**Independent Test**: Can be fully tested by feeding a self-contained interchange file describing one dialogue (an opening line, a choice with two options, two follow-up lines, two endings) and verifying the generated dialogue graph plays exactly that structure and its line text is positioned for automatic inclusion in a localization table build.

**Acceptance Scenarios**:

1. **Given** an interchange file with lines, a choice, and an ending, **When** the dialogue is generated, **Then** the resulting graph reproduces the same line order, the same choice options each leading to their declared next step, and the same ending.
2. **Given** a generated dialogue graph, **When** a localization table build runs, **Then** every line's text is picked up automatically, with no separate localization-specific authoring step required.
3. **Given** an interchange file naming a speaker, **When** the dialogue is generated, **Then** the corresponding line node is linked to that existing speaker.

---

### User Story 2 - One dialogue jumps into another (Priority: P1)

A dialogue's flow needs to continue inside a separate, independently-authored dialogue partway through (e.g., a shared "commerce" sub-conversation reused by several NPCs), then that referenced dialogue plays as part of the same conversation.

**Why this priority**: Explicitly required — a dialogue authored as a single flat conversation cannot represent reusable sub-conversations, which the interchange format and this generation path must support from day one, not as a later add-on.

**Independent Test**: Can be fully tested by an interchange file where one dialogue's flow points to a node referencing a second, separately-defined dialogue, and verifying the generated graph contains a working reference from the first into the second rather than a copy of its content.

**Acceptance Scenarios**:

1. **Given** a node in one dialogue that references another dialogue by its identifier or name, **When** both dialogues are generated, **Then** the first dialogue's graph contains a link to the second dialogue's own generated graph asset, not an inlined copy of its content.
2. **Given** a referenced dialogue that hasn't been generated yet (or can't be located), **When** the referencing dialogue is generated, **Then** the link is left in a recognized "not yet resolved" state rather than causing generation to fail outright — consistent with how this pipeline already treats an unresolved sub-graph reference elsewhere.

---

### User Story 3 - Dialogue assets go through the same safe review/apply pipeline as everything else (Priority: P2)

A person (or an unattended run) generating dialogue assets gets the same guarantees already in place for quest and flow assets: a full preview before anything is written, and a colliding asset is always reported, never silently overwritten or silently skipped.

**Why this priority**: Consistency and safety matter as much for dialogue assets as for quest/flow assets, but this story only has meaning once User Story 1 produces something to plan and apply — it's the integration wrapper, not new risk surface.

**Independent Test**: Can be fully tested by including a dialogue alongside existing quest/flow data in one generation run and confirming the dialogue asset appears in the same preview, is subject to the same collision handling, and behaves identically whether the run is unattended or reviewed interactively.

**Acceptance Scenarios**:

1. **Given** interchange data describing one or more dialogues, **When** a preview is requested, **Then** each dialogue appears as a proposed asset alongside any quest/flow assets from the same run, with nothing written to disk yet.
2. **Given** a dialogue asset whose proposed location already holds an existing asset, **When** the plan is applied, **Then** that dialogue is left untouched and the collision is recorded exactly as any other asset collision is.

---

### Edge Cases

- A choice option's declared next-step identifier doesn't match any node in the same dialogue → reported as an explicit error identifying the dialogue and the dangling reference, not a broken or silently dropped link.
- Two nodes in the same dialogue share the same identifier → reported as an explicit error; identifiers must be unique within a dialogue for "next" pointers to be unambiguous.
- A line references a speaker that cannot be located → treated the same "not yet resolved" way as an unresolved sub-dialogue reference (Edge Case above), not a hard failure of the whole generation run.
- A dialogue's declared entry point doesn't match any of its nodes → reported as an explicit error before generation proceeds.
- A dialogue that references itself (directly or transitively, through a chain of sub-dialogue references) → reported rather than causing infinite processing, consistent with how the existing pipeline already handles a cyclical quest-trigger reference.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST build a playable dialogue graph from an interchange description consisting of, at minimum: an identifying name, an entry point, spoken lines (each attributed to a speaker and carrying source-language text), player choices (each with one or more labeled options leading to a next step), and endings.
- **FR-002**: The system MUST preserve the exact flow declared in the interchange description — line order, which option leads to which next step, and which ending is reached — with no reordering or inference of structure not explicitly declared.
- **FR-003**: Generated dialogue line text MUST be positioned so that an existing localization table build picks it up automatically, without any additional localization-specific authoring step for this pipeline to perform.
- **FR-004**: The system MUST support a dialogue referencing another, separately-defined dialogue as a point in its flow, resulting in a real link between the two generated assets rather than a copy of the referenced dialogue's content.
- **FR-005**: A dialogue-to-dialogue reference or a speaker reference that cannot be resolved to an existing asset at generation time MUST leave that specific link in a recognized incomplete state rather than failing the entire generation run.
- **FR-006**: The system MUST reject, with a specific and identifiable error, any interchange description containing a dangling next-step reference, a duplicate node identifier within one dialogue, or an entry point that doesn't match any of its own nodes.
- **FR-007**: The system MUST reject, with a specific and identifiable error, a dialogue-reference cycle (a dialogue reaching itself again through a chain of sub-dialogue references) rather than processing it indefinitely.
- **FR-008**: Dialogue assets MUST be generated through the same preview-then-apply pipeline already used for other generated asset kinds in this system, subject to the same collision handling (a colliding asset location is always reported, never silently overwritten or silently skipped).
- **FR-009**: The mechanism used to resolve a reference to an existing on-disk asset MUST be shared between every generator that needs one (at minimum, the existing flow-asset generator and the new dialogue-asset generator), rather than each generator having its own separate resolution logic.

### Key Entities

- **Dialogue**: An identifiable, nameable unit of conversation — an entry point plus a set of nodes forming its flow.
- **Line**: A single spoken beat within a dialogue — a speaker, source-language text, and what comes next.
- **Choice**: A branch point offering one or more labeled options, each leading to a next step.
- **Ending**: A terminal point of a dialogue's flow, optionally carrying an outcome label.
- **Sub-dialogue link**: A point in one dialogue's flow that continues inside another, separately-defined dialogue.
- **Speaker reference**: An attribution on a line pointing at an already-existing speaker, not a newly authored one.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A hand-authored interchange description of a representative multi-branch dialogue (at least one choice, one sub-dialogue reference, two endings) produces a generated dialogue that plays back exactly as declared, verified without manually inspecting or editing the generated asset.
- **SC-002**: 100% of a generated dialogue's line text is picked up by a subsequent localization table build with zero manual localization-authoring steps performed as part of dialogue generation.
- **SC-003**: 100% of malformed interchange descriptions in the edge-case categories above (dangling reference, duplicate identifier, invalid entry point, reference cycle) are rejected with an identifiable error before any asset is written — none silently produce a broken or partial asset.
- **SC-004**: Dialogue asset generation, when combined with quest/flow generation in the same run, never produces a duplicate or conflicting write outcome — every collision across all asset kinds is reported through the one existing mechanism, not a second one specific to dialogues.

## Assumptions

- The interchange format itself is a small, purpose-built data shape (not a generic user-configurable mapping over arbitrary external tables, unlike the quest/flow half of this initiative) — the future external authoring tool is expected to emit this shape directly.
- Only one source language is authored through this pipeline per dialogue; translation into additional languages continues through the existing localization workflow, entirely outside this feature.
- V1 branching is limited to player-facing choices with no per-option gating condition; state-conditioned branching is out of scope for this feature.
- A real, working lookup of "find this existing asset on disk from an external identifier" is not implemented by this feature — only the single shared mechanism (interface/seam) both generators use is required; a not-yet-resolved link is an accepted, valid outcome for V1 (see FR-005).
- The external authoring tool that will produce interchange files — its interface, distribution, and any continuous-integration branching workflow around it — is entirely out of scope for this feature.
