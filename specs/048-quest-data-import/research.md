# Phase 0 Research: Quest & Flow Graph Generation from Structured Data

## 1. Mapping configuration format

**Decision**: The mapping configuration is authored as JSON (parsed via Newtonsoft), one document per project, containing an array of per-table declarations (`id_column`, `fields`, `ignore`, `references`). A thin typed C# model (`MappingConfig`, `TableMapping`, `ReferenceMapping`) sits behind it — nothing in `Pivot`/`Planning` ever touches raw JSON.

**Rationale**: JSON keeps the config diffable and versionable alongside the project, and Newtonsoft's `JObject`/`JArray` handle arbitrary column names (including accented/spaced French column names from real spreadsheets) without any C# identifier constraints. Consumers only ever see the typed model, so switching the file format later (e.g. to YAML) is a parser-layer change, not a consumer-layer one.

**Alternatives considered**:
- *YAML* — more human-friendly to hand-edit, but adds a second serialization dependency for one file; deferred, not precluded (the typed `MappingConfig` model does not care which parser produced it).
- *ScriptableObject-based config* — would fit Unity conventions and give an Inspector UI "for free", but a mapping config is fundamentally a list of column-name strings tied to an external file's exact header row; a text format is more natural to keep in sync with a spreadsheet than a serialized Unity asset, and stays usable from a CI context without an Editor. Rejected for V1; an Editor-side Inspector wrapper reading/writing the same JSON remains possible later without changing the model.

## 2. Source table parsing (CSV / JSON rows)

**Decision**: Implement a small self-contained RFC 4180 CSV row reader directly in `graphimport` (`Sources/CsvRowSource.cs`), independent of any other package. JSON row sources are read via Newtonsoft directly into row dictionaries.

**Rationale**: The ecosystem already has an RFC 4180 CSV parser in `graphlocalization` (`CsvLocalizationExporter`/`CsvLocalizationProvider`, per the 2026-07 audit), but it is written against `graphlocalization`'s own localization-table shape (key/locale columns), not generic row/column data, and it is not exposed as a public reusable component. Depending on `graphlocalization` here only to reach that internal parser would add a real package dependency for a ~100-line utility, and `graphimport` has no other reason to depend on `graphlocalization` (localization/dialogue import is explicitly out of scope, per the spec's assumptions — that's Part 2). A small owned CSV reader is simpler and keeps the dependency list to exactly what's needed (YAGNI).

**Alternatives considered**:
- *Depend on `graphlocalization` for CSV parsing* — rejected: wrong-shaped API, unjustified extra package coupling for out-of-scope functionality.
- *Third-party CSV package* — rejected: RFC 4180 (quoted fields, embedded commas/newlines) is a small, well-understood grammar; not worth an external dependency.

## 3. Reference resolution (ID-or-name, ambiguity handling)

**Decision**: `IReferenceResolver` resolves a raw string value against a target table's index, built from **both** the table's `id_column` values and a designated fallback key column's values (e.g., `Nom`/`Name`), declared per reference in the mapping config. Both indices are built once per table and cached for the duration of a single pivot-build run. A value found in exactly one index (or found identically in both, pointing to the same row) resolves; found in more than one row across either index, or in neither, raises a `ReferenceResolutionException` naming the source table/row/column and the offending value.

**Rationale**: Matches the real reference dataset's behavior directly — `Puzzles."Quête liée"` uses the target's Name, `Sequence."Quête (ID)"` uses the target's ID, sometimes within the same table's different columns. A single resolver strategy (index-then-lookup) handles both without per-column special-casing, and the ambiguity/not-found cases become simple index-hit-count checks rather than heuristics.

**Alternatives considered**:
- *Always require ID references, reject name-based ones* — rejected by the user during discussion: real production sheets mix both, and the tool must not force a spreadsheet reshape.
- *Fuzzy/best-effort name matching* — rejected: FR-003 explicitly forbids guessing; only exact matches count.

## 4. Branch detection strategy

**Decision**: `IBranchDetectionStrategy` is a pluggable seam; V1 ships exactly one implementation, `DeclaredColumnBranchStrategy`, which groups a quest's steps by their declared order/position field and requires a designated "outcome/signal" column to be present and distinct across every step sharing a position. Missing or duplicate outcome values within a shared-position group raise an explicit error (FR-006) rather than falling back to any inferred order.

**Rationale**: Directly implements the discussion's decision (option "a" — an explicit new column in the source data, not text-inference). Keeping it behind an interface (rather than hardcoding the grouping logic into `PivotBuilder`) costs nothing extra in V1 and is what makes User Story 4's "no free-text inference, ever" requirement enforceable/testable in isolation, and reusable if Part 2 (external dialogue tool) needs a different branch-declaration shape later.

**Alternatives considered**:
- *Infer branches from step-name text similarity* — explicitly rejected in discussion (no NLP-style guessing).
- *Hardcode the single strategy with no interface* — rejected: the interface costs one type and keeps the door open for Part 2 without speculative extra strategies being built now (YAGNI respected — only one implementation ships).

## 5. Package dependency shape (graphquest + graphgameflow together)

**Decision**: `graphimport` depends directly on `graphcore`, `graphquest`, and `graphgameflow`. See Constitution Check in `plan.md` for why this is judged compliant with principle VII.

**Rationale**: The quest/objective output is built via `graphquest`'s fluent builder; the playable branching flow output is built via `graphgameflow`'s primitives; both are needed because FR-007 requires both outputs from one pivot. No indirection layer is introduced to avoid this — an abstraction here would exist only to satisfy an appearance of decoupling, not a real present requirement.

## 6. JSON library choice

**Decision**: `com.unity.nuget.newtonsoft-json`, Unity's officially distributed Newtonsoft package.

**Rationale**: `JsonUtility` cannot deserialize dictionaries/arbitrary-shape objects, which both the mapping config and JSON source tables require. Newtonsoft is the de facto standard, officially distributed by Unity (not a random third-party package), and low-risk to add.

**Alternatives considered**:
- *`JsonUtility`* — rejected: cannot represent the required shapes.
- *Hand-rolled minimal JSON parser* — rejected: reinventing a well-solved problem for no present benefit; violates YAGNI in the other direction (unnecessary custom code where a standard, official solution exists).
