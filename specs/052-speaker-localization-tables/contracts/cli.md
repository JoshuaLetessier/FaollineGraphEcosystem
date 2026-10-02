# Contract: Command-line entry points & mapping file

## `Faolline.GraphImport.Editor.DialogueImportBatch.Run`

```text
Unity -batchmode -projectPath <p> -executeMethod Faolline.GraphImport.Editor.DialogueImportBatch.Run
      -dialoguesJson <path> -dialoguePathTemplate <template>
      [-speakerFolder <path>]            # unchanged, default "Assets/Generated/Speakers"
      [-speakerTablesCsv <path>]         # NEW, optional
```

Without `-speakerTablesCsv`: behavior identical to 0.5.x.

With `-speakerTablesCsv`:

| Situation | Outcome |
|---|---|
| File missing/unreadable | Fatal before any asset write; exit 1. |
| Mapping invalid (missing `SpeakerKey`/`Table` column, empty key, key with two different groups) | Fatal before any asset write; message names the problem (line / key / both values); exit 1. |
| Speaker created by this run, listed | Created with the mapped group. |
| Speaker created by this run, not listed | Created with no group. |
| Existing speaker referenced by any dialogue of the export (generated **or** collided), listed with a different group | Group updated; line printed: `[DialogueImportBatch] Speaker '<key>' group '<old>' → '<new>' (<assetPath>)`. Not a failure. |
| Existing speaker, not listed | Untouched. |
| Listed speaker not referenced by any dialogue of the export | Ignored. |

Exit code rules otherwise unchanged (0 only when no conflicts and no generator failures). Group updates never affect the exit code.

### Mapping file format (`-speakerTablesCsv`)

- RFC4180 CSV, UTF-8, first record = header.
- Required header columns (exact, trimmed): `SpeakerKey`, `Table`. Column order free; extra columns ignored.
- `SpeakerKey`: matches `Speaker.SpeakerId` / interchange `speakerKey`. Trimmed; must not be empty.
- `Table`: the speaker's group. Trimmed; empty = explicitly no group (default speakers table).
- Same key twice with the same (normalized) group: accepted. With different groups: rejected.

```csv
SpeakerKey,Table
PNJ_Aubergiste,Chapitre1
PNJ_Forgeron,Chapitre2
Narrateur,
"PNJ_Garde, nuit",Chapitre1
```

## `Faolline.GraphLocalization.Unity.Editor.TranslationImportBatch.Run`

```text
Unity -batchmode -projectPath <p> -executeMethod Faolline.GraphLocalization.Unity.Editor.TranslationImportBatch.Run
      [-speakersCsv <path>] [-dialogueTranslationsDir <dir>]     # at least one (unchanged)
```

`-dialogueTranslationsDir`: unchanged — each `<name>.csv` imports into `TextCollection(ForGraph(<name>))` (same names as before on Windows-built projects).

`-speakersCsv` (**changed**): the file (`Key,<locale>…`, as exported by dialogue-studio) is split by key:

| Row's key held by… (among String Table Collections under `Assets/Localization/Collections/`) | Outcome |
|---|---|
| exactly one collection | Imported into that collection (other entries of the collection untouched). |
| no collection | Failure `key '<k>' found in no collection — build the tables first`; row skipped. |
| several collections | Failure `key '<k>' is ambiguous: <c1>, <c2>`; row skipped. |

Valid rows are imported even when some rows fail. Exit 1 if any failure (any flag), else 0.
