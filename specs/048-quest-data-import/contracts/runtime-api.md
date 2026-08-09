# Contracts: `com.faolline.graphimport` public API

This is a library, not a service — "contracts" are the public interfaces/types other code (Editor tooling, future Part-2 dialogue tool, tests) is written against. Namespaces omitted for brevity; all live under `Faolline.GraphImport.*` mirroring the folder layout in `plan.md`.

## Runtime (pure C#, no Editor/Unity dependency)

```csharp
// Sources
public interface IRowSource
{
    SourceTable Read(string filePath, string tableName);
}
public sealed class CsvRowSource : IRowSource { /* RFC 4180 */ }
public sealed class JsonRowSource : IRowSource { /* array-of-objects JSON */ }

// Mapping
public sealed class MappingConfig
{
    public static MappingConfig LoadFromJson(string json);
    public IReadOnlyList<TableMapping> Tables { get; }
    // Validates every declared column exists in the given SourceTables; throws MappingValidationException listing every problem found (not just the first).
    public void Validate(IReadOnlyDictionary<string, SourceTable> sourceTables);
}

// Resolution
public interface IReferenceResolver
{
    PivotReference Resolve(SourceRow fromRow, ReferenceMapping reference, IReadOnlyDictionary<string, SourceTable> allTables);
}
public sealed class IdOrNameReferenceResolver : IReferenceResolver { }
public sealed class ReferenceResolutionException : Exception { /* SourceTable, SourceRowIndex, SourceColumn, RawValue, Reason, CandidateRows */ }

// Branching
public interface IBranchDetectionStrategy
{
    // Given all of one quest's steps, returns the linear steps and the detected branches.
    // Throws BranchDetectionException on an unresolvable shared-position group (FR-006) — never guesses.
    (IReadOnlyList<PivotStep> Linear, IReadOnlyList<PivotBranch> Branches) Detect(PivotQuest quest, IReadOnlyList<PivotStep> steps);
}
public sealed class DeclaredColumnBranchStrategy : IBranchDetectionStrategy { }

// Pivot
public sealed class PivotBuilder
{
    public PivotBuilder(MappingConfig mapping, IReferenceResolver resolver, IBranchDetectionStrategy branchStrategy);
    public IReadOnlyList<PivotQuest> Build(IReadOnlyDictionary<string, SourceTable> sourceTables);
}

// Planning
public interface IPathTemplateResolver
{
    string Resolve(PlanEntryKind kind, PivotQuest quest);
}
public sealed class TemplatePathResolver : IPathTemplateResolver
{
    public TemplatePathResolver(IReadOnlyDictionary<PlanEntryKind, string> templatesByKind);
}
public sealed class PlanBuilder
{
    public PlanBuilder(IPathTemplateResolver pathResolver);
    // Pure: no disk access, no AssetDatabase. Deterministic for a given input (SC-003).
    public GenerationPlan Build(IReadOnlyList<PivotQuest> quests);
}
```

## Editor (Editor-only assembly; references `UnityEditor`, `graphquest`, `graphgameflow`)

```csharp
public static class PlanConflictDetector
{
    // Checks each PlanEntry.ProposedPath against AssetDatabase, and against duplicate
    // ProposedPaths within the same plan. Never mutates anything.
    public static ConflictReport Detect(GenerationPlan plan);
}

public static class PlanApplier
{
    // Creates exactly the entries in `plan` whose paths are NOT present in `report`.
    // Any entry present in `report.Conflicts` is skipped and left for the caller to see in the report — never overwritten.
    // Returns which entries were actually created.
    public static IReadOnlyList<PlanEntry> Apply(GenerationPlan plan, ConflictReport report);
}

public interface IQuestAssetGenerator
{
    // Builds a graphquest asset at entry.ProposedPath from entry.Data (a PivotQuest), via graphquest's fluent builder.
    void Generate(PlanEntry entry);
}

public interface IFlowAssetGenerator
{
    // Builds a graphgameflow asset at entry.ProposedPath from entry.Data (the quest's resolved steps/branches),
    // via graphgameflow primitives, referencing puzzle/dialogue content through SubGraphNodeData.
    void Generate(PlanEntry entry);
}

// Editor review window: lists plan.Entries with editable ProposedPath, shows report.Conflicts,
// and on confirm calls PlanApplier.Apply with the (possibly edited) plan. No new data contract
// beyond GenerationPlan/ConflictReport — the window is a view over them.
```

## CI / unattended entry point

```csharp
public static class GraphImportPipeline
{
    // One call usable from a batchmode command: builds the pivot, the plan, detects conflicts,
    // applies whatever is clean, and returns the report. A non-clean report (report.IsClean == false)
    // is the signal a CI script checks to fail the job — no separate machine-readable format needed,
    // ConflictReport already IS the contract for both consumers (FR-013).
    public static ConflictReport Run(MappingConfig mapping, IReadOnlyDictionary<string, SourceTable> sourceTables, IPathTemplateResolver pathResolver);
}
```
