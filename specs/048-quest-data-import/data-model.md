# Phase 1 Data Model: Quest & Flow Graph Generation from Structured Data

Entities are grouped by pipeline stage. Arrows (`→`) indicate "produced from".

## Input stage

### SourceTable
Raw parsed rows from one input file, before any mapping is applied.
- `Name: string` — logical table name (e.g., `"Quetes"`, `"Sequence"`)
- `Header: string[]` — column names as they literally appear in the source
- `Rows: SourceRow[]`

### SourceRow
- `Table: SourceTable` (back-reference, for error messages)
- `RowIndex: int` (1-based, for error messages)
- `Values: IReadOnlyDictionary<string, string>` — raw column name → raw string value

## Mapping stage

### MappingConfig
- `Tables: TableMapping[]`

### TableMapping
- `SourceTableName: string` — must match a `SourceTable.Name`
- `IdColumn: string`
- `Fields: FieldMapping[]` — pivot field name → source column name
- `Ignore: string[]` — explicitly-documented ignored columns (informational; any undeclared column is implicitly ignored per FR-014)
- `References: ReferenceMapping[]`

### FieldMapping
- `PivotField: string`
- `SourceColumn: string`

### ReferenceMapping
- `PivotField: string` — the field on the pivot entity that will hold the resolved reference
- `SourceColumn: string` — the raw column holding the reference value
- `TargetTables: string[]` — one or more candidate target `TableMapping.SourceTableName`s
- `MatchOn: ReferenceMatchKey[]` — ordered list of `Id` and/or `NameColumn(string)` to try

**Validation rules**:
- Every `FieldMapping.SourceColumn` and `ReferenceMapping.SourceColumn` MUST exist in the corresponding `SourceTable.Header` (checked at mapping-load time — FR fails fast on typo'd columns, see Edge Cases).
- `IdColumn` MUST be unique per row within its table (duplicate IDs are a load-time error, not a resolution-time one).

## Resolution stage

### ReferenceIndex
Built once per target `TableMapping`, from its `IdColumn` values and each declared fallback name column's values.
- Lookup by value → zero, one, or many matching `SourceRow`s.

### ReferenceResolutionException
Thrown by `IReferenceResolver`, never swallowed.
- `SourceTable`, `SourceRowIndex`, `SourceColumn`, `RawValue`, `Reason: NotFound | Ambiguous`, `CandidateRows` (when ambiguous)

## Pivot stage (the internal representation — FR-004)

### PivotQuest
- `Id: string` (from the quest table's `IdColumn`)
- `Name: string`
- `Fields: IReadOnlyDictionary<string,string>` — mapped, non-reference fields (chapter, type, etc. — domain-neutral bag, not hardcoded per-field properties)
- `Steps: PivotStep[]`
- `TriggeredBy: PivotReference[]` — quests/dialogues that trigger this quest
- `Triggers: PivotReference[]` — quests/dialogues this quest triggers

### PivotStep
- `Id: string`
- `Quest: PivotQuest` (back-reference)
- `Order: int` (or comparable position value, from the sequence table)
- `ContentRef: PivotReference` — resolved reference to the puzzle/dialogue row this step represents (never inlined content — FR-008)
- `BranchOutcome: string?` — populated only when this step is part of a `PivotBranch`; null for a purely linear step

### PivotBranch
- `Quest: PivotQuest`
- `Position: int` — the shared order/position value
- `Steps: PivotStep[]` (≥2, each with a distinct non-null `BranchOutcome`)

### PivotReference
- `TargetTable: string`
- `TargetId: string` (always resolved to the canonical ID, regardless of whether the source used ID or name)

**State/derivation rule**: `PivotBuilder` first resolves all references (failing loud per FR-003), then groups each quest's steps by `Order` via `IBranchDetectionStrategy` — positions with exactly one step become plain `PivotStep`s; positions with more than one step become a `PivotBranch` (and each member step must have a distinct declared outcome, or `PivotBuilder` raises the FR-006 error).

## Planning stage

### GenerationPlan
- `Entries: PlanEntry[]`
- Pure data — no Unity/Editor types, so it is constructible and comparable (SC-003 determinism) entirely in `Runtime`.

### PlanEntry
- `LogicalId: string` — stable identity within the plan (e.g., `"quest:Q_001"`, `"flow:Q_001"`), used to detect duplicate-path collisions within one run
- `Kind: QuestAsset | FlowAsset`
- `ProposedPath: string` — from the path template, overridable by the caller before Apply
- `SourcePivotId: string` — which `PivotQuest`/etc. this entry was derived from
- `Data: object` — the typed payload (`PivotQuest` or the quest's resolved flow structure) the Apply-phase generator will consume

### IPathTemplateResolver
- `Resolve(PlanEntry.Kind, PivotQuest) → string`, driven by one template string per `Kind` (e.g., `"Assets/Graphs/{Chapter}/GameFlow/{Name}.asset"`), substituting from `PivotQuest.Fields`/`Name`/`Id`.

## Apply stage (Editor-only)

### ConflictReport
- `Conflicts: ConflictEntry[]`
- `IsClean: bool` (derived: `Conflicts.Count == 0`) — the single signal both CI (non-zero exit when false) and the review window (list to display) consume, per FR-013.

### ConflictEntry
- `PlanEntry: PlanEntry`
- `ExistingAssetPath: string`
- `Reason: AlreadyExists | DuplicateTargetWithinPlan`

## Relationships summary

```
SourceTable ──rows──> SourceRow
MappingConfig ──describes──> SourceTable (by name)
SourceRow ──(Mapping + Resolution)──> PivotQuest / PivotStep / PivotReference
PivotQuest ──1:N──> PivotStep
PivotStep ──0/1:1──> PivotBranch (grouped by Order)
PivotQuest ──(PlanBuilder)──> PlanEntry (QuestAsset), PlanEntry (FlowAsset)
GenerationPlan ──(Apply)──> ConflictReport + created GraphAssets (graphquest / graphgameflow)
```
