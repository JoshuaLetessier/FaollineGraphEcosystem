# Feature Specification: Quest & Flow Graph Generation from Structured Data

**Feature Branch**: `048-quest-data-import`

**Created**: 2026-08-08

**Status**: Draft

**Input**: User description: "Generic quest/dialogue-flow graph generation from structured external data (spreadsheets exported as CSV/JSON) via a declarative, pluggable mapping layer, producing both quest/objective data and playable flow graphs with explicit branching from the same source tables, without hardcoding any specific project's table shape or column names."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Map an existing production spreadsheet without reshaping it (Priority: P1)

A content designer already tracks quests, ordered steps, puzzles, dialogues and characters across several linked spreadsheet tables, mixed in with unrelated production-tracking columns (status, notes, assignees, external ticket IDs). They want to point the tool at their existing tables and declare which columns matter, without renaming columns, splitting/merging tables, or losing any of their existing tracking data.

**Why this priority**: Without this, the feature demands the designer restructure their documentation to fit the tool, which defeats the purpose — the tool must adapt to real-world data, not the other way around.

**Independent Test**: Can be fully tested by providing a multi-table sample dataset (with irrelevant columns present) and a mapping configuration, and verifying the resulting pivot data contains only the mapped fields, correctly cross-referenced, with unmapped columns having no effect on the output.

**Acceptance Scenarios**:

1. **Given** a source table with both mapped and unmapped columns, **When** the mapping is applied, **Then** the pivot contains only the declared fields and ignores the rest without error.
2. **Given** two source tables where one references rows of the other by a stable identifier in one place and by a human-readable name in another, **When** the mapping resolves that reference, **Then** it correctly matches the referenced row in both cases.
3. **Given** a reference value that matches more than one row, or matches none, **When** resolution runs, **Then** the tool reports an explicit, specific error identifying the ambiguous or missing reference — it never guesses.

---

### User Story 2 - Preview and control where generated assets land (Priority: P1)

A content designer runs the generator against their mapped data and wants to see, before anything is created, exactly which assets will be produced, of what kind, and at what location — and be able to adjust the location of individual assets before committing, without being forced to accept a fully automatic placement.

**Why this priority**: Generating assets directly into unexpected or colliding locations risks damaging an existing project structure or overwriting hand-crafted work; a design partner must trust the tool enough to run it repeatedly.

**Independent Test**: Can be fully tested by running generation against sample data and verifying a complete, accurate preview (proposed assets and paths) is produced with nothing written to disk, then separately verifying that applying an edited preview creates assets exactly where specified.

**Acceptance Scenarios**:

1. **Given** mapped pivot data, **When** a preview is requested, **Then** a complete list of assets-to-be-created (with proposed locations) is produced and no asset is written.
2. **Given** a generated preview, **When** the designer changes the proposed location of one asset, **Then** only that asset's final location differs from the default; all others follow the default placement rule.
3. **Given** an unedited preview, **When** it is committed as-is, **Then** every asset is created exactly at its proposed location.

---

### User Story 3 - Regenerate safely from an automated pipeline (Priority: P2)

A pipeline (or a designer working without the interactive tool open) re-runs generation automatically after the source data changes, with no person reviewing each asset placement. It must never silently overwrite or silently skip an asset that already exists at a target location — any such collision must be visible in a report that a person can act on afterward.

**Why this priority**: Automated regeneration is the payoff of structuring the data this way, but it is only safe to automate if data loss and silent gaps are impossible by construction.

**Independent Test**: Can be fully tested by running generation twice against data that produces an overlapping asset location on the second run, and verifying the second run neither overwrites nor silently omits that asset, and produces a report identifying the conflict.

**Acceptance Scenarios**:

1. **Given** a target location that already holds an asset, **When** generation is applied, **Then** that asset is left untouched and the conflict is recorded in a report.
2. **Given** a run that produced one or more conflicts, **When** the run completes in an unattended context, **Then** the run's outcome is distinguishable from a fully clean run (so an automated pipeline can flag it) and the same conflict information is available for a person to review afterward.
3. **Given** a run with zero conflicts, **When** it completes unattended, **Then** it is distinguishable as fully clean, with no unresolved items to review.

---

### User Story 4 - Get a playable branching flow, not just a flat quest list (Priority: P2)

A content designer's step-sequence data already expresses branching (e.g., two possible next steps depending on the outcome of a prior mini-game or dialogue), each branch tagged with which outcome activates it. The designer wants the generated output to preserve that branching as an actual playable flow, and wants each step's underlying content (a puzzle, a dialogue) to be referenced rather than duplicated inline.

**Why this priority**: This is what elevates the feature from a quest-log generator to a true flow-graph generator; it is more involved than the P1 stories and depends on them being solid first.

**Independent Test**: Can be fully tested by supplying step-sequence data with two steps sharing the same position under the same quest, each tagged with a distinct outcome, and verifying the generated flow contains two distinct branches gated on those outcomes, each pointing to a reference to its step's content rather than an inline copy.

**Acceptance Scenarios**:

1. **Given** two steps at the same sequence position under the same quest, each declaring a different triggering outcome, **When** generation runs, **Then** the resulting flow contains two branches, each gated on its declared outcome.
2. **Given** step-sequence data with steps at the same position but no declared outcome distinguishing them, **When** generation runs, **Then** the tool reports this as an error rather than guessing a branch order or condition.
3. **Given** a step referencing a puzzle or dialogue defined elsewhere in the data, **When** the flow is generated, **Then** the step's node references that content rather than embedding a copy of it.

---

### Edge Cases

- A row's declared reference points to an ID or name that doesn't exist anywhere in the target table → explicit error naming the source row and the unresolved reference, generation for that item does not silently proceed.
- The same reference value matches a row in more than one candidate table (when a reference may resolve against multiple table types) → explicit error requiring the data or mapping to disambiguate.
- A mapping declares a column that doesn't exist in the actual source table (e.g., typo, or the spreadsheet changed shape) → explicit, early error rather than a silent empty field.
- Two previewed assets resolve to the identical target location → treated as a conflict within the same run, surfaced in the report rather than one silently clobbering the other.
- A quest's trigger data references another quest that (directly or transitively) triggers it back → reported rather than causing infinite processing.
- Source data for a table is entirely empty (headers only, no rows) → generation completes with zero assets for that table, not an error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a user to declare, per source table, which columns map to which pivot fields, which column is the row's stable identifier, and which columns are ignored — without requiring changes to the source table itself.
- **FR-002**: The system MUST resolve cross-table references using either a stable identifier or a designated human-readable fallback key, as declared per reference, and MUST NOT require the same resolution key to be used consistently across all references.
- **FR-003**: The system MUST report an explicit, specific error — identifying the source row and the ambiguous or unresolved value — whenever a reference resolves to zero or more than one candidate, and MUST NOT guess a resolution.
- **FR-004**: The system MUST produce an internal representation (not exposed as an input or output format) of quests, ordered steps, branches, and cross-references, independent of the shape or column names of any specific source table set.
- **FR-005**: The system MUST support detecting branch points in step-sequence data only through an explicit, declared signal (e.g., a designated column identifying the triggering outcome) and MUST NOT infer branching from step names, descriptions, or other free-text content.
- **FR-006**: The system MUST report an error when step-sequence data contains multiple steps that appear to share a position without the declared branch signal distinguishing them, rather than assuming an arbitrary order or merging them.
- **FR-007**: The system MUST generate, from the same mapped source data, both quest/objective data and a playable branching flow, as distinct outputs.
- **FR-008**: Generated flow steps that represent existing content (a puzzle, a dialogue) MUST reference that content rather than duplicating it inline.
- **FR-009**: The system MUST support producing a complete preview of what would be generated (assets, their kind, and their proposed location) without creating, modifying, or deleting anything.
- **FR-010**: The proposed location of each previewed asset MUST be derived from a configurable rule per asset kind, and MUST be individually adjustable by a user before anything is created.
- **FR-011**: The system MUST support committing a preview either without further interaction (suitable for an unattended/automated run) or after user review and adjustment, from the same preview data.
- **FR-012**: The system MUST NOT overwrite an existing asset at a previewed target location, and MUST NOT silently omit it either — every such collision MUST appear in a report. This applies uniformly regardless of the existing asset's origin: an asset from a prior run of this same pipeline is treated identically to a hand-authored one. Re-running never modifies a previously generated asset in place; removing it (deliberately, outside this pipeline) is a prerequisite for a location to be regenerated.
- **FR-013**: The collision report MUST be usable both by an automated/unattended run (to signal that the run needs attention) and by a person reviewing the run afterward, as a single consistent source of truth rather than two separate mechanisms.
- **FR-014**: The system MUST treat columns and tables not declared in the mapping as inert — present in the source data but without any effect on generation.

### Key Entities

- **Source Table**: A named table of rows from external structured data (e.g., a spreadsheet export), with a header row and arbitrary columns, some relevant to generation and some not.
- **Mapping Configuration**: A declarative, per-table definition of which column is the row identifier, which columns map to which pivot fields, which columns are ignored, and how each cross-table reference resolves.
- **Reference**: A declared link from one source row to another (in the same or a different table), resolved via a stable identifier or a fallback human-readable key.
- **Pivot Quest**: A generated-independent representation of a quest — its identity, objectives, and the other quests/dialogues that trigger it or that it triggers.
- **Pivot Step**: A single position within a quest's flow, referencing its underlying content (puzzle, dialogue) rather than containing it.
- **Pivot Branch**: A point where a quest's flow diverges into multiple steps at the same position, each gated by a declared triggering outcome.
- **Generation Plan**: The complete, unapplied preview of a generation run — every asset that would be created, its kind, its proposed location, and the data it would contain.
- **Conflict Report**: The record of every previewed asset whose proposed location already holds an existing asset, produced by every run and consumed identically by automated and interactive contexts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A content designer can go from an existing multi-table production spreadsheet (with unrelated tracking columns present) to a full generation preview using only a mapping configuration — with no changes to the spreadsheet's existing structure or columns.
- **SC-002**: 100% of asset location collisions across all generation runs are visible in a conflict report; 0% result in an existing asset being overwritten or a new asset being silently dropped.
- **SC-003**: Running generation twice against unchanged source data and mapping produces an identical preview both times (same assets, same proposed locations).
- **SC-004**: A step-sequence branch (multiple steps at one position, each with a distinct declared outcome) always produces a matching number of distinct branches in the generated flow, gated on those outcomes — verified across representative sample datasets with zero misclassified branches.
- **SC-005**: A reference that cannot be resolved (missing or ambiguous) is caught and reported before any asset is generated for the item that depends on it — never surfacing later as a broken or missing in-game link.

## Assumptions

- V1 targets structured tabular input (CSV/JSON-style rows and columns); free-text or unstructured input formats are out of scope.
- Character/NPC-style reference tables are treated as lookup data consumed when resolving references from quest/step/dialogue tables — they do not themselves produce generated flow or quest assets in V1.
- Generation operates at whole-asset granularity: an asset is either newly created or reported as a conflict; partial/in-place merging of a previously generated asset's internal content is out of scope for V1 (see clarification below on what "already exists" means across repeated runs).
- The dedicated external dialogue-authoring tool (feeding this same pipeline with a dialogue-specific pivot format) is a separate, later effort; this feature only needs to keep the mapping/pivot/plan/apply architecture reusable for it, not build it.
- Path template rules are declared per asset kind (e.g., one rule for quest assets, one for flow assets), not per individual asset.
