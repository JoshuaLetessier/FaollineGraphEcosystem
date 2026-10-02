# Data Model: Separable Speaker Localization Tables

## Entities

### Speaker (graphdialoguesystem, asset — modified)

| Field | Type | Notes |
|---|---|---|
| `_speakerId` | string | Unchanged. Logical id; name key = `speaker_{SpeakerId}`. |
| `_displayNameFallback` | string | Unchanged. Source text + runtime fallback. |
| **`_localizationGroup`** | string | **NEW.** Optional. Empty/whitespace = no group. Trimmed when read for naming. Serialized default `""` → existing assets need no migration. |

Derived (not stored):
- **Speaker table group** = `"Speakers"` when no group, else `"Speakers_" + NormalizeGroup(group)`.
- **Speaker table** = `LocalizationTableNames.ForGroup("GraphDialogue", speakerTableGroup)` → e.g. `GraphDialogue_Speakers`, `GraphDialogue_Speakers_Chapitre1`.

Validation: two speakers with the same `SpeakerId` and different normalized groups → build error naming both (first by asset path keeps the key).

### LocalizationKeyEntry (graphlocalization, transient — modified)

| Field | Type | Notes |
|---|---|---|
| `Key`, `Type`, `NodeId`, `DefaultHint`, `AssetFlags` | — | Unchanged. |
| **`Group`** | string | **NEW.** Global keys only. Normalized; null = default group (`"Global"` in names). Canonical spelling = first seen (case-insensitive). |

### Table (logical)

A table is identified by its **base name** (string). Physical artifacts derive from it:

| Kind | Base name | Unity String collection | Unity Asset collections | CSV file |
|---|---|---|---|---|
| Per-graph (dialogue, quest) | `ForGraph(graph.name)` | `{base}_Text` in `Collections/{lib}/{base}/` | `{base}_{Audio\|Sprite\|Texture\|Video\|Font}` | `Csv/{lib}/{base}.csv` |
| Global group | `ForGroup(lib, group)` | `{base}_Text` in `Collections/{lib}/_Global/{base}/` | `{base}_{type}` (only if flagged keys) | `Csv/{lib}/{base}.csv` |
| Legacy shared (pre-feature) | — | `Global_Text` in `Collections/{lib}/_Global/Global_Text/` | — | `Csv/{lib}/{lib}_Global.csv` |

### SpeakerGroupMapping (graphimport, transient — new)

Parsed from the consumer's CSV (format in [contracts/cli.md](contracts/cli.md)).

| Member | Notes |
|---|---|
| `TryGetGroup(speakerKey, out group)` | `group` normalized; `""`/null = explicitly no group. |
| `Count` | Distinct keys. |

Validation (all before any write): header has `SpeakerKey` and `Table`; no empty key; no key with two different normalized groups.

### SpeakerGroupChange (graphimport, transient — new)

`(SpeakerKey, AssetPath, OldGroup, NewGroup)` — one per existing speaker realigned by the import; printed by the batch/window.

## Naming rules (`LocalizationTableNames`)

| Function | Rule |
|---|---|
| `Sanitize(s)` | Replace `" < > \| : * ? \ /` and chars 0–31 with `_`; null/empty → `Unnamed`. Platform-independent. |
| `NormalizeGroup(g)` | Trim; empty/whitespace → null. |
| `GroupComparisonKey(g)` | `Sanitize(NormalizeGroup(g)).ToLowerInvariant()`. |
| `ForGraph(name)` | `Sanitize(name)`. |
| `ForGroup(lib, g)` | `Sanitize(lib) + "_" + Sanitize(NormalizeGroup(g) ?? "Global")`. |
| `TextCollection(t)` | `t + "_Text"`. |
| `AssetCollection(t, type)` | `t + "_" + type`. |

## Carry-over precedence (build, per key × locale)

Applied when a key's target table value is **empty**:

1. Target table's own non-empty value — always kept (never overwritten).
2. First non-empty value found for that key in **another** table of the **same lib** (tables visited in ordinal name order; includes orphan/legacy tables).
3. Source-locale default hint (unchanged pre-feature behavior).

Moved-from entries are then removed from still-managed tables by the normal orphan pass; orphan *tables* (legacy, renamed graph, emptied group) are reported, never deleted.

## State transitions

### A speaker's name key across builds

```text
(no group) ──author/import sets G──▶ (group G) ──set H──▶ (group H) ──cleared──▶ (no group)
   table: {lib}_Speakers      {lib}_Speakers_G       {lib}_Speakers_H        {lib}_Speakers
```
Every arrow: next build carries all locale values to the new table, removes the entry from the old one.

### Legacy migration (one-time)

```text
Global_Text holds speaker_* (+translations)
  ── first build after upgrade ──▶ speaker_* created in {lib}_Speakers[_G]_Text with carried values
                                   Global_Text reported as orphan (safe to delete), untouched
```

## Runtime lookup (per call)

| Caller | Table passed | Provider supports scoping? | Behavior |
|---|---|---|---|
| Presenter: line/choice/voice | `ForGraph(ownerGraph.name)` (owner = `CurrentGraph`) | yes (Unity) | Read that table only; miss → `#key` → strict/audit/title handling. |
| Presenter: speaker name | `ForSpeakerTable(speaker)` | yes | Same; miss → `DisplayNameFallback`. |
| QuestEvaluator | `ForGraph(quest.name)` | yes | Same; miss → authored text. |
| Any of the above | table not in manifest | yes | Warn once per table, classic probing. |
| Any caller | — or provider not scoped (CSV, custom, title) | no | Classic `Resolve(key, locale)` — unchanged. |
