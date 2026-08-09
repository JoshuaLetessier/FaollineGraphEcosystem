# Quickstart: Dialogue Graph Generation from a Pivot Interchange Format

## 1. A hand-authored interchange file (stand-in for the future external tool)

```json
{
  "dialogues": [
    {
      "id": "DLG_006",
      "name": "Victoire contre le joueur de dé",
      "entryNodeId": "n1",
      "nodes": [
        { "id": "n1", "kind": "line", "speakerKey": "antagoniste", "text": "Bien joué...", "next": "n2" },
        { "id": "n2", "kind": "choice", "options": [
          { "label": "Continuer la conversation", "next": "n3" },
          { "label": "Partir", "next": "n4" }
        ] },
        { "id": "n3", "kind": "subDialogue", "targetDialogue": "Rencontre avec Tsuki" },
        { "id": "n4", "kind": "end", "reason": "Completed", "outcomeLabel": "left_early" }
      ]
    }
  ]
}
```

## 2. Build the pivot and plan

```csharp
var interchange = InterchangeDialogueSet.LoadFromJson(File.ReadAllText("dialogues.json"));
var dialogues = new DialoguePivotBuilder().Build(interchange); // throws with node-level context on any structural problem

var plan = new PlanBuilder(pathResolver).Build(dialogues); // extended overload — Kind = DialogueAsset
```

## 3. Apply (same safety guarantees as quest/flow — US3)

```csharp
var report = PlanConflictDetector.Detect(plan);
var resolver = new NullProjectAssetResolver(); // V1: no real disk lookup yet
var generators = new Dictionary<PlanEntryKind, IAssetGenerator>
{
    [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver),
    [PlanEntryKind.FlowAsset] = new FlowAssetGenerator(resolver), // same resolver instance, shared seam
};
var result = PlanApplier.Apply(plan, report, generators);
```

## 4. Localization — nothing further to do

The generated `DialogueGraph` already implements `ILocalizedGraph`; running graphlocalization's existing "Build All Tables" pass picks up every line's text automatically (SC-002). No dialogue-specific localization step exists in this pipeline.

## Sample

`com.faolline.graphimport/Samples/DialogueExample/` ships a hand-authored interchange file exercising a line → choice → sub-dialogue-link → end flow, as a runnable end-to-end reference (mirrors 048's `Samples/CryptiqueExample`).
