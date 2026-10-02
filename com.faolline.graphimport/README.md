# Faolline GraphImport

**Version**: 0.6.0 — **Unity**: 6000.x — **Depends on**: `com.faolline.graphcore` ≥ 0.43.4,
`com.faolline.graphstandard` ≥ 0.18.0, `com.faolline.graphdialoguesystem` ≥ 0.20.0,
`com.faolline.graphlogging` ≥ 0.2.0, `com.unity.nuget.newtonsoft-json` ≥ 3.2.2

Editor-only tooling that generates real [`graphdialoguesystem`](../com.faolline.graphdialoguesystem/)
`DialogueGraph`/`Speaker` assets from a dedicated dialogue interchange JSON format, through a pure
plan-then-apply pipeline. No source shape or column naming is assumed — only the pivot's own domain model
(dialogue / line / choice / end / sub-dialogue) is opinionated. Never runs at gameplay time and produces no
shared runtime type; see [`../ARCHITECTURE.md`](../ARCHITECTURE.md)'s **T4 · Generation tooling** tier.

---

## Installation

See [`../INSTALL.md`](../INSTALL.md) for the full install guide (module selector or manual git URL).

```
https://github.com/JoshuaLetessier/FaollineGraphEcosystem.git?path=com.faolline.graphimport#master
```

## The pipeline

Four stages, each independently testable, none of which touches disk until the last one:

```
InterchangeDialogueSet          raw JSON, 1:1 with the file — no validation yet
        │  DialoguePivotBuilder.Build()   ← fail-fast: duplicate ids, dangling refs, bad entry point,
        ▼                                    sub-dialogue reference cycles
IReadOnlyList<PivotDialogue>    validated, source-shape-independent
        │  PlanBuilder.BuildDialogues()   ← pure, deterministic: no disk access, no AssetDatabase
        ▼
GenerationPlan                  the full preview: one PlanEntry per asset that WOULD be created
        │  PlanConflictDetector.Detect()  ← read-only: flags AlreadyExists / DuplicateTargetWithinPlan
        ▼
ConflictReport
        │  PlanApplier.Apply()            ← only non-conflicting entries are written
        ▼
ApplyResult                     what was actually created, and any generator failures (never aborts the rest)
```

**Never overwrites, never silently drops.** A conflicting entry (a `AlreadyExists` or
`DuplicateTargetWithinPlan` collision) is simply excluded from the apply step and left for the
`ConflictReport` to describe — resolve it by hand (rename, delete the stale asset) and re-run. A generator
exception on one entry is caught and recorded in `ApplyResult.Failures`; every other non-conflicting entry
still gets its chance.

## The interchange format

One JSON file, a list of dialogues, each a list of nodes:

```json
{
  "dialogues": [
    {
      "id": "DLG_006",
      "name": "Victoire contre le joueur de dé",
      "entryNodeId": "n1",
      "nodes": [
        { "id": "n1", "kind": "line", "speakerKey": "antagoniste", "text": "Bien joué, tu as gagné.", "next": "n2" },
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

A worked two-dialogue example ships in `Samples~/DialogueExample/dialogues.json` — import the **Dialogue
Example** sample from the Package Manager to try it against your own project.

Node kinds (`InterchangeNodeKind`) — only the fields for that kind matter:

| `kind` | Fields | Becomes |
|---|---|---|
| `line` | `speakerKey`, `text`, `next` | a `DialogueLineHandle`, speaker resolved via `IProjectAssetResolver.ResolveSpeaker` |
| `choice` | `options: [{ label, next }, …]` | a `DialogueChoiceHandle` with one option per entry |
| `end` | `reason` (a `graphcore` `EndReason` name, e.g. `"Completed"`), `outcomeLabel` | a `DialogueEndHandle` |
| `subDialogue` | `targetDialogue` (an id **or** a name — resolved unambiguously within the same file) | a `DialogueSubGraphHandle`, target resolved by `IProjectAssetResolver.ResolveGraph` |

`DialoguePivotBuilder.Build()` validates before anything else runs: every node id unique within its
dialogue, `entryNodeId` matches a real node, every `next`/option target exists, every `subDialogue` target
resolves to exactly one dialogue (by id, falling back to name) with no reference cycle across dialogues.
Any violation throws (`DialogueStructureException` / `DialogueReferenceException` / `DialogueCycleException`)
— nothing partial is ever written.

## Two ways to run it

### Editor window — interactive review

**Window ▸ Faolline ▸ Graph Import**. Point it at your interchange JSON, a path template, a speaker
folder and, optionally, a speaker tables CSV (see *Speaker localization groups* below); **Build dialogue plan**
shows every proposed asset (editable path per row, conflicts flagged inline); **Commit** applies only the
non-conflicting entries.

### CLI batch — unattended / CI

`DialogueImportBatch.Run` via `-executeMethod`, e.g.:

```
Unity.exe -batchmode -quit -projectPath <path> \
  -executeMethod Faolline.GraphImport.Editor.DialogueImportBatch.Run \
  -dialoguesJson Assets/Data/dialogues.json \
  -dialoguePathTemplate "Assets/Graphs/Dialogues/{name}.asset" \
  -speakerFolder "Assets/Generated/Speakers" \
  -speakerTablesCsv Assets/Data/speaker-tables.csv
```

`-speakerFolder` defaults to `Assets/Generated/Speakers` if omitted; `-speakerTablesCsv` is optional. Exits `0`
only if the run is fully clean (no conflicts, no generator failures) — a conflict or a failure exits `1` and logs
each one to stderr, so a CI step fails loudly instead of silently producing a partial import. An invalid speaker
tables CSV aborts the run (exit `1`) before anything is written.

## Path templates

`{name}` and `{id}` substitute the dialogue's own name/id into a per-kind template string
(`TemplatePathResolver`), e.g. `"Assets/Graphs/Dialogues/{name}.asset"` →
`"Assets/Graphs/Dialogues/Victoire contre le joueur de dé.asset"`. An unrecognized `{token}` throws rather
than silently resolving to an empty string.

Dialogues that sub-dialogue-link to each other are generated in dependency order (the linked-to dialogue
first) regardless of input order, so `ProjectAssetResolver.ResolveGraph` can always resolve a same-run
target — the resolver only ever looks up assets that are part of the same plan, never the wider project; a
target generated in an *earlier* run, or hand-authored outside this pipeline, resolves to `null` (the
same documented-safe "incomplete node" state `graphcore` already uses for an unset `SubGraphNodeData`).

## Stable node ids → stable localization keys

Every generated node's id is overridden to `{dialogueId}_{pivotNodeId}` (`DialogueAssetGenerator.StableNodeId`)
instead of the builder's default fresh GUID. This makes a line's localization key
(`DialogueLocalizationKeys.ForLine`, derived from the node's own id) predictable ahead of time — and means
re-authoring a dialogue and regenerating it (after deleting the stale asset, per the never-overwrite rule
above) reproduces the *same* keys instead of orphaning every existing translation on each regeneration.

## Speaker resolution

`ProjectAssetResolver.ResolveSpeaker` is find-or-create: reuses an existing `Speaker` asset anywhere under
`Assets` whose `SpeakerId` matches the interchange `speakerKey`, or creates one under the configured speaker
folder if none exists. Never creates a duplicate for the same key, including across repeated calls within
one run.

## Speaker localization groups

A speaker's display name is built into its own localization table per **group** (`Speaker.LocalizationGroup`,
e.g. one per chapter, so each chapter's names can ship in its own Addressables group). The interchange format
carries no group: give the import a CSV produced from your own spreadsheet instead.

```csv
SpeakerKey,Table
PNJ_Aubergiste,Chapitre1
PNJ_Forgeron,Chapitre2
Narrateur,
"PNJ_Garde, nuit",Chapitre1
```

- `SpeakerKey` matches the interchange `speakerKey`; `Table` is the group (empty = the default speakers table).
  Column order is free, extra columns are ignored, values are trimmed.
- Speakers **created** by the import take their mapped group (none when unlisted).
- After the plan is applied, every **existing** speaker the export references is realigned with the mapping —
  the mapping wins, and each change is printed. This also covers dialogues whose asset collided, i.e. every
  dialogue on a re-import, so changing a chapter in the spreadsheet and re-running is enough. Speakers the mapping
  doesn't list keep their group; listed speakers the export doesn't reference are ignored.
- Rejected before anything is written: a missing `SpeakerKey`/`Table` column, an empty key, one key listed with
  two different groups.

Translations follow a speaker whose group changes on the next localization table build (graphlocalization carries
them over).

## Architecture

```
com.faolline.graphimport/
  Runtime/
    DialoguePivot/
      InterchangeDialogueSet.cs   ← raw JSON deserialization (Newtonsoft.Json)
      DialoguePivotBuilder.cs     ← validation: duplicate ids, dangling refs, entry point, ref cycles
      PivotDialogue.cs, PivotDialogueNode.cs   ← validated, source-shape-independent model
      Dialogue*Exception.cs       ← structured, always-identifies-the-node validation failures
    Planning/
      GenerationPlan.cs           ← the full unapplied preview (PlanEntry per proposed asset)
      PlanBuilder.cs              ← pivot data + a path resolver → a GenerationPlan (pure, no disk access)
      TemplatePathResolver.cs     ← {name}/{id} token substitution
    Resolution/
      PivotReference.cs           ← a resolved cross-table reference (table + canonical id)
    SpeakerGroups/
      SpeakerGroupMapping.cs      ← SpeakerKey,Table CSV → speaker localization group (validated, pure)
  Editor/
    Apply/
      PlanConflictDetector.cs     ← read-only: plan vs. current project state
      PlanApplier.cs              ← writes only non-conflicting entries; never overwrites
      ConflictReport.cs, ApplyResult.cs
    Generation/
      DialogueAssetGenerator.cs   ← PivotDialogue → real DialogueGraph via DialogueGraphBuilder
      IAssetGenerator.cs
    Resolution/
      ProjectAssetResolver.cs     ← IProjectAssetResolver: resolves within the current plan; find-or-create Speakers
      SpeakerGroupApplier.cs      ← realigns existing referenced speakers with the speaker tables mapping
    Batch/
      DialogueImportBatch.cs      ← -executeMethod CLI entry point
      BatchArgs.cs                ← shared `-flag value` parsing
    Window/
      GraphImportWindow.cs        ← interactive plan review + commit
  Samples~/
    DialogueExample/              ← a worked two-dialogue interchange JSON
```

## Layering

**T4 · Generation tooling** (see [`../ARCHITECTURE.md`](../ARCHITECTURE.md)): the one sanctioned exception
to "verticals never reference verticals" — graphimport may reference `graphdialoguesystem` (and, in
principle, other T2 verticals) because it never *executes* a graph, only *authors* assets through their
public builder APIs, one-way, Editor-only. Nothing in T0–T3 is allowed to reference it back.

## What this doesn't do (yet)

Quest/flow asset generation shipped in 0.1.0–0.4.1 but was removed in 0.5.0 — design issues need
reworking before it comes back; see [`CHANGELOG.md`](CHANGELOG.md#050). The full implementation is
preserved on branch `archive/graphimport-quest-flow` if you need to reference it. This package currently
generates dialogue/speaker assets only.
