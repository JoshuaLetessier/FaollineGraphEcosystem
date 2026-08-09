# Quickstart: Quest & Flow Graph Generation from Structured Data

## 1. Export your tables

Export each linked table (e.g., `Quetes`, `Sequence`, `Puzzles`, `Dialogues`) to CSV or JSON, keeping your existing columns as-is — including production-tracking columns you don't want mapped.

## 2. Write a mapping config

```json
{
  "tables": [
    {
      "sourceTableName": "Quetes",
      "idColumn": "ID",
      "fields": [
        { "pivotField": "name", "sourceColumn": "Nom" },
        { "pivotField": "chapter", "sourceColumn": "Chapitres" }
      ],
      "references": [
        {
          "pivotField": "triggeredBy",
          "sourceColumn": "Déclencheur(Quêtes)",
          "targetTables": ["Quetes"],
          "matchOn": ["Id", { "nameColumn": "Nom" }]
        }
      ]
    },
    {
      "sourceTableName": "Sequence",
      "idColumn": "ID",
      "fields": [
        { "pivotField": "order", "sourceColumn": "Ordre" },
        { "pivotField": "branchOutcome", "sourceColumn": "Signal" }
      ],
      "references": [
        { "pivotField": "quest", "sourceColumn": "Quête (ID)", "targetTables": ["Quetes"], "matchOn": ["Id"] },
        { "pivotField": "content", "sourceColumn": "Référence_ID", "targetTables": ["Puzzles", "Dialogues"], "matchOn": ["Id", { "nameColumn": "Nom" }] }
      ]
    }
  ]
}
```

Every column not listed above (Statut, Notes, Emplacement du script, Discord IDs, ...) is left untouched and has no effect on generation.

## 3. Preview

```csharp
var mapping = MappingConfig.LoadFromJson(File.ReadAllText("mapping.json"));
var tables = new Dictionary<string, SourceTable> {
    ["Quetes"] = new CsvRowSource().Read("Quetes.csv", "Quetes"),
    ["Sequence"] = new CsvRowSource().Read("Sequence.csv", "Sequence"),
    // ...
};
mapping.Validate(tables);

var pivot = new PivotBuilder(mapping, new IdOrNameReferenceResolver(), new DeclaredColumnBranchStrategy())
    .Build(tables);

var plan = new PlanBuilder(new TemplatePathResolver(new() {
    [PlanEntryKind.QuestAsset] = "Assets/Graphs/{chapter}/Quests/{name}.asset",
    [PlanEntryKind.FlowAsset]  = "Assets/Graphs/{chapter}/GameFlow/{name}.asset",
})).Build(pivot);

// Nothing has been written yet — `plan.Entries` is inspectable/editable here.
```

## 4a. Apply headlessly (CI)

```csharp
var report = GraphImportPipeline.Run(mapping, tables, pathResolver);
if (!report.IsClean) {
    // non-zero exit — report.Conflicts lists exactly what needs attention
}
```

## 4b. Apply via the Editor review window

Open **Window → Faolline → Graph Import**, load the same mapping/tables, review the generated plan (adjust any `ProposedPath` inline), then confirm — conflicts are shown in the same window before commit.

## Sample

`com.faolline.graphimport/Samples/CryptiqueExample` ships a sanitized version of a real multi-table production spreadsheet (quests, sequence/ordering, puzzles, dialogues) with a working mapping config, as a runnable end-to-end reference.
