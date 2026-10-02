# Quickstart: validating separable speaker tables

End-to-end checks in the dev project (`FaollineGraphEcosystem`, which already has com.unity.localization 1.5.12, locales, and a legacy `Assets/Localization/Collections/GraphDialogue/_Global/Global_Text` collection — a real migration case). Automated EditMode tests cover each step; this is the human-visible walkthrough.

## 0. Prerequisites

- Localization settings Mode = **UnityLocalization**, Table mode = Text (or Both).
- Note a translated speaker name currently in `Global_Text` (e.g. `speaker_npc` in `fr`).

## 1. Group speakers (US1)

1. Select a Speaker; in **Localization Group** pick *New group…*, type `Chapitre1`, **Create**. On a second speaker create `Chapitre2`; then select several speakers at once and pick `Chapitre1` from the dropdown (now listed) to set them all. Leave a third on *(None)*. The inspector shows the resolved table (`GraphDialogue_Speakers_Chapitre1`, …).
2. **Faolline ▸ Localization ▸ Build All Tables**.
3. Expect under `Assets/Localization/Collections/GraphDialogue/_Global/`: `GraphDialogue_Speakers_Text`, `GraphDialogue_Speakers_Chapitre1_Text`, `GraphDialogue_Speakers_Chapitre2_Text`, each with exactly its speaker's `speaker_*` key, source column pre-filled.
4. The previously translated name's `fr` value appears in its new table (carry-over from `Global_Text`).
5. Console: an orphan-collection warning for `Global_Text` ("translations carried over, safe to delete"). It is NOT deleted.
6. Move a speaker from `Chapitre1` to `Chapitre2`, rebuild → its translations move with it; the `Chapitre1` table no longer has the key.

## 2. Targeted runtime lookup (US2)

1. Enter Play mode with a dialogue driver on a dialogue using a `Chapitre1` speaker.
2. Lines and the speaker name display translated as before.
3. (Automated, not visible here) the EditMode test suite asserts that only the dialogue's own `_Text` collection and `GraphDialogue_Speakers_Chapitre1_Text` were read — not other dialogues' tables.
4. Optional Addressables check: Localization ▸ Addressable group rules → put `GraphDialogue_Speakers_Chapitre2_Text` tables in a separate group; play a Chapitre1 dialogue and confirm (Addressables Event Viewer / Profiler) that the Chapitre2 group is never loaded.

## 3. Quests (US2, FR-016)

Open a quest log (QuestEvaluator journal) in a project with dialogues: quest/objective names display as before; the suite asserts only that quest's `_Text` collection was read.

## 4. Speaker translation import (US3)

```text
Unity -batchmode -projectPath <dev project> -executeMethod Faolline.GraphLocalization.Unity.Editor.TranslationImportBatch.Run -speakersCsv <dialogue-studio>/speakers.csv
```
- Each row lands in its speaker's table. Add a row for an unknown key → it is reported by key, the rest still imports, exit code 1.

## 5. Dialogue import with mapping (US4)

```csv
SpeakerKey,Table
PNJ_A,Chapitre1
PNJ_B,Chapitre2
```
```text
Unity -batchmode … -executeMethod Faolline.GraphImport.Editor.DialogueImportBatch.Run -dialoguesJson export.json -dialoguePathTemplate "Assets/Generated/Dialogues/{name}.asset" -speakerTablesCsv speaker-tables.csv
```
- New speakers carry the mapped group; unlisted new ones have none.
- Change `PNJ_A` to `Chapitre2`, re-run (all dialogues now collide) → output prints `Speaker 'PNJ_A' group 'Chapitre1' → 'Chapitre2' (...)`; rebuild tables → its translations follow.
- Break the mapping (duplicate key with two groups) → run aborts before writing anything, naming the key and both values.

## 6. Regression

- Full ecosystem EditMode suite green (batchmode `-runTests -testPlatform EditMode`, no `-quit`).
- A project with no groups and no mapping: one rebuild, every dialogue/quest displays identical text (SC-005).
