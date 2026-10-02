# Feature Specification: Separable Speaker Localization Tables

**Feature Branch**: `052-speaker-localization-tables`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Separable speaker localization tables for Addressables projects. Problem today: every Speaker display name goes into ONE Unity Localization String Table collection named \"Global_Text\" (not even lib-scoped), so a project using Addressables cannot put a chapter's speaker names into that chapter's bundle (Unity Localization assigns Addressables groups per collection, not per entry). Worse, the runtime UnityLocalizationProvider (and UnityLocalizedAssetProvider) resolves any key by probing every manifest collection with GetTableAsync().WaitForCompletion() until it finds it; Global_Text is probed last, so the first speaker-name resolution loads every dialogue table of the game — per-graph dialogue tables are not separable at runtime either. Decisions already taken: (A) build side — explicit optional table/group field on Speaker, generic grouped global keys in graphlocalization, one lib-scoped collection per (lib, group), translations carried over when a key moves between collections of the same lib (also migrates the legacy Global_Text), CSV backend mirrors the split, TranslationImportBatch -speakersCsv routes each row to the collection holding its key (unknown key = failure). (B) runtime side — resolve directly in the right table via an optional table-aware provider interface, CSV provider ignores the table, un-scoped calls keep probing, one shared naming function. (C) dialogue import — optional -speakerTablesCsv mapping (SpeakerKey,Table), mapping wins on re-import, absent speaker keeps its value, no change to dialogue-studio or the interchange contract. Packages: graphlocalization, graphdialoguesystem, graphimport."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Assign speakers to separate localization tables (Priority: P1)

A content author on a project that ships its content as separately-loaded packages (typically one per chapter) assigns each speaker to a named table group — for example "Chapter1", "Chapter2", or a shared group for recurring characters. Building the localization tables produces one distinct table per group, so each chapter's speaker names can be packaged and loaded with that chapter. Speakers with no group keep landing in a single default speakers table.

**Why this priority**: This is the core of the request. As long as every speaker name lives in one shared table, no project can split speaker names across separately-loaded packages, whatever else is done.

**Independent Test**: Can be fully tested by creating three speakers — one in group "Chapter1", one in group "Chapter2", one with no group — running a table build, and verifying that three distinct speaker tables exist, each holding exactly its own speaker's name with the source-language text pre-filled.

**Acceptance Scenarios**:

1. **Given** speakers assigned to groups A and B and one speaker with no group, **When** the localization tables are built, **Then** three distinct speaker tables exist, each containing only the names of its own speakers, with source-language text pre-filled.
2. **Given** any build, **When** its speaker tables are inspected, **Then** each table's name identifies the library that owns it, so no other library's non-dialogue table can ever end up with the same name.
3. **Given** a speaker whose name already has translations in group A's table, **When** the author moves that speaker to group B and the tables are rebuilt, **Then** every existing translation of that name appears in group B's table and no longer in group A's, with no translator action.
4. **Given** an existing project whose speaker names (with translations) live in the legacy single shared table, **When** the first build after the upgrade runs, **Then** every existing translation ends up in the new speaker table(s), and the legacy table is reported as no longer used rather than deleted silently.
5. **Given** a project using the lightweight file-based localization backend, **When** the tables are built, **Then** the same split appears as one file per speaker group.

---

### User Story 2 - Playing a dialogue only opens the tables it needs (Priority: P1)

At runtime, displaying a dialogue line, a choice label, a localized voice clip or a speaker's name looks the text up directly in the one table that holds it, instead of opening tables one after another until the text is found.

**Why this priority**: Without this, separate tables bring no runtime benefit. Today the very first speaker-name lookup opens every table of the game, including tables belonging to packages that are not needed yet or not even downloaded. Separate tables (User Story 1) are only worth having if lookups respect them.

**Independent Test**: Can be fully tested in a project with two dialogues (each with its own table) and speakers spread over two groups: playing one dialogue to its end opens only that dialogue's table and the tables of the speaker groups it actually shows, and no other table.

**Acceptance Scenarios**:

1. **Given** dialogues D1 and D2, each with its own table, and speakers in group A (used only by D1) and group B (used only by D2), **When** D1 is played to its end, **Then** only D1's table and group A's speaker table have been opened.
2. **Given** dialogue D1 continuing into a sub-dialogue D3, **When** D3's lines are displayed, **Then** they are looked up in D3's own table.
3. **Given** a lookup in the designated table that does not find the text (for example after a stale build), **When** the text is displayed, **Then** it is handled exactly like any other missing text today (literal fallback, warning or strict failure per the project's setting), and no other table is opened in search of it.
4. **Given** a project that supplies its own custom text provider written before this feature, **When** a dialogue is played, **Then** it works exactly as before, with no change required to that provider.
5. **Given** a text lookup made without designating a table (for example existing project code), **When** it runs, **Then** it behaves exactly as today.
6. **Given** a project with both quests and dialogues, **When** a quest's name or one of its objectives is displayed, **Then** only that quest's own table is opened — no dialogue table and no other quest's table.

---

### User Story 3 - One speaker translation file reaches the right tables (Priority: P2)

The dialogue authoring tool exports every speaker-name translation as a single file. Importing that file fills each speaker's translations into whichever table that speaker belongs to, without the person running the import having to know or declare the groups.

**Why this priority**: Required for the existing translation workflow to keep working once speaker names are split. It only matters once User Story 1 produces more than one speaker table.

**Independent Test**: Can be fully tested by importing one translation file containing rows for three speakers from three different groups, and verifying that each row's translations land in its speaker's table.

**Acceptance Scenarios**:

1. **Given** one translation file with rows for speakers in group A, group B and the default group, **When** it is imported, **Then** each row's translations land in the table of its speaker's group.
2. **Given** a row whose key belongs to no existing speaker table, **When** the file is imported, **Then** that row is reported by its key as a failure, is placed in no table, and the overall run is reported as unsuccessful, while the valid rows are still imported.
3. **Given** the existing per-dialogue translation files, **When** they are imported alongside the speaker file, **Then** they behave exactly as today.

---

### User Story 4 - Dialogue import assigns speaker groups from a mapping (Priority: P2)

A pipeline that generates dialogues from the authoring tool's export also receives a mapping file, produced by the project from its own spreadsheet, that pairs each speaker with a table group. Speakers created by the import get their group from it, and existing speakers are brought in line with it.

**Why this priority**: Projects that generate speakers through the import pipeline would otherwise have to assign every group by hand after each import. It builds on User Story 1, which defines what a group is.

**Independent Test**: Can be fully tested by importing an export that references three speakers with a mapping that lists two of them: the created speakers carry the mapped groups, the unlisted one has no group; re-importing with a changed mapping updates the existing speaker's group and reports the change.

**Acceptance Scenarios**:

1. **Given** a speaker that does not exist yet and is listed in the mapping with group A, **When** the import runs, **Then** the created speaker carries group A.
2. **Given** an existing speaker with group A that the mapping lists with group B, **When** the import runs, **Then** the speaker's group becomes B and the change is reported.
3. **Given** an existing speaker with group A that the mapping does not list, **When** the import runs, **Then** its group stays A.
4. **Given** a speaker that does not exist yet and is not listed in the mapping, **When** the import runs, **Then** it is created with no group (default speakers table).
5. **Given** no mapping file is supplied, **When** the import runs, **Then** it behaves exactly as today.
6. **Given** a malformed mapping file (missing required columns, or the same speaker listed twice with different groups), **When** the import runs, **Then** it is rejected with a specific error before any asset is written.

---

### Edge Cases

- Two speakers share the same identifier but are assigned to different groups → the build reports an explicit error naming both speakers; their shared name is not silently placed in one of the two tables.
- Two group names that differ only by letter case, or that become identical once made safe for table/file names → reported as a conflict at build time, not silently merged.
- A group name with leading or trailing spaces → trimmed; a group made only of spaces counts as no group.
- A mapping row with an empty group → the speaker is explicitly set to no group (the mapping wins on re-import).
- A mapping row for a speaker that none of the imported dialogues references → ignored, not an error.
- A dialogue renamed (so its table's name changes) → its lines' existing translations follow it into the renamed table, by the same rule as a speaker changing group.
- The legacy shared table and a new speaker table both hold a value for the same name and language → the new table's non-empty value is kept; the legacy value only fills gaps.
- A speaker deleted from the project → its name disappears from its table on the next build, as today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A speaker MUST carry an optional table group, set explicitly by the author (or by the import, FR-013); it MUST NOT be inferred from where the speaker asset is stored. A speaker with no group belongs to the default speakers table. *(Follow-up 2026-10-03)* The author picks the group from the groups already used in the project (or creates a new one once) rather than typing it on every speaker, and can set it on several speakers at once.
- **FR-002**: The localization table build MUST produce one separate table per (owning library, group) for keys that are not tied to a single dialogue, and every such table's name MUST identify its owning library.
- **FR-003**: Grouping MUST be a generic capability of the localization build, usable by any library for its non-dialogue keys; the localization core MUST NOT contain speaker-specific knowledge.
- **FR-004**: When a key moves from one table to another table of the same library between two builds, the build MUST carry every existing translation, in every language, into the new table before removing the old entry.
- **FR-005**: On the first build after upgrading, translations held in the legacy single shared speaker table MUST be migrated into the new speaker tables; the legacy table MUST then be reported as no longer used, not deleted automatically.
- **FR-006**: The file-based localization backend MUST mirror the same split (one file per library and group).
- **FR-007**: The rule that turns a dialogue or a group into a table name MUST exist exactly once and be shared by the table build, the translation import and the runtime lookup.
- **FR-008**: At runtime, a dialogue's line text, choice labels and localized voice clips MUST be looked up in that dialogue's own tables (including when it is played as a sub-dialogue), and a speaker's name in its group's table, without opening any other table.
- **FR-009**: A lookup in a designated table that does not find the text MUST be treated as missing text under the project's existing missing-text policy, and MUST NOT fall back to searching other tables.
- **FR-010**: A lookup made without designating a table MUST behave exactly as before this feature.
- **FR-011**: A project-supplied text provider that does not support designated tables MUST keep working unchanged; designated-table lookups MUST fall back to its existing behavior.
- **FR-012**: The translation import MUST route each row of a single speaker translation file to the table holding that row's key; a key found in no speaker table MUST be reported by key as a failure, placed nowhere, and make the run report failure.
- **FR-013**: The dialogue import MUST accept an optional speaker-to-group mapping. Speakers it creates take their group from the mapping (no group when unlisted); existing speakers referenced by the imported dialogues take the mapped group when it differs, and the change is reported; existing speakers not listed keep their group. Without a mapping, the import MUST behave exactly as before.
- **FR-014**: The dialogue import MUST validate the mapping (required columns present, no speaker listed twice with different groups) before writing any asset, and reject it with a specific error otherwise.
- **FR-015**: The table build MUST report, as an explicit error naming both speakers, two speakers that share an identifier but carry different groups.
- **FR-016**: At runtime, a quest's displayed texts (quest name, objective names and descriptions) MUST be looked up in that quest's own table, without opening any other table, under the same missing-text rule as FR-009.

### Key Entities

- **Speaker table group**: An optional, author-chosen name on a speaker that decides which speaker table holds its display name.
- **Speaker table**: One table per (owning library, group) holding speaker display names in every language; the default one holds speakers with no group.
- **Dialogue table**: The existing one-per-dialogue table holding a dialogue's line texts and choice labels (and its localized voice clips, when used).
- **Quest table**: The existing one-per-quest table holding a quest's name and its objectives' names and descriptions.
- **Speaker group mapping**: A project-produced file pairing speaker identifiers with table groups, consumed by the dialogue import.
- **Legacy shared speaker table**: The single table that held every speaker name before this feature; migrated once, then reported as unused.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a project with at least three dialogues and at least two speaker groups, playing any one dialogue to its end opens 0 tables other than that dialogue's own (plus any sub-dialogue it enters) and the tables of the speaker groups it displays.
- **SC-002**: In a project with both quests and dialogues, displaying any quest's texts opens 0 tables other than that quest's own.
- **SC-003**: Changing a speaker's group, renaming a dialogue, or upgrading a project from the legacy shared table loses 0 existing translations, in every language.
- **SC-004**: Importing a single speaker translation file places 100% of rows whose speaker exists in the correct table, and reports 100% of rows whose speaker does not exist, by key.
- **SC-005**: An existing project that adopts no groups and no mapping needs no manual step beyond one table rebuild; every dialogue and quest displays exactly the same text, in every language, before and after the upgrade.
- **SC-006**: A custom text provider written against the pre-feature contract keeps compiling and producing the same results with no modification.

## Assumptions

- Only the Unity Localization backend gains a runtime benefit; the lightweight file-based backend loads every file up front by design and only mirrors the split for consistency (FR-006).
- Assigning the produced tables to separately-loaded packages (e.g. Addressables groups) remains the consuming project's own configuration; the libraries only produce separable tables and stop opening all of them.
- The dialogue authoring tool and the interchange format stay unchanged; the speaker-to-group mapping is produced by the consuming project from its own data.
- Renaming the legacy shared speaker table is an accepted breaking change, made transparent for translations by FR-005; any project code or script that referenced that table by its literal name must be updated.
- Per-dialogue and per-quest table names keep their current naming (derived from the graph's name), so existing per-dialogue translation files keep importing unchanged.
- Packages touched: the localization library, the dialogue library, the quest library (runtime lookups only, FR-016) and the import pipeline. Quest tables' content and build are unchanged.
- A designated-table lookup trusts the most recent table build; a missing text after a stale build is a missing-text case (FR-009), fixed by rebuilding.
