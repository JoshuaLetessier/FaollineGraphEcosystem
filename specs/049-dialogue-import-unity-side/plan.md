# Implementation Plan: Dialogue Graph Generation from a Pivot Interchange Format

**Branch**: `049-dialogue-import-unity-side` | **Date**: 2026-08-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/049-dialogue-import-unity-side/spec.md`

## Summary

Extends `com.faolline.graphimport` (built in 048) with a `DialogueAssetGenerator` that turns a small, purpose-built JSON interchange format into real `com.faolline.graphdialoguesystem` `DialogueGraph` assets — reusing the existing Plan/Apply/ConflictReport pipeline (new `PlanEntryKind.DialogueAsset`) rather than adding a parallel one. Requires one small upstream addition to `com.faolline.graphdialoguesystem`'s `DialogueGraphBuilder` (an `AddSubGraph` method, symmetric to graphstandard's) so a dialogue-to-dialogue jump can be authored through the existing builder. Introduces one shared `IProjectAssetResolver` seam, retrofitted onto 048's `FlowAssetGenerator` and used by the new `DialogueAssetGenerator`, so both generators' "find an existing asset from an external id" need is solved once. The external authoring tool that will eventually produce interchange files is out of scope; a hand-authored sample file stands in for it.

## Technical Context

**Language/Version**: C# (Unity 6000.0), same ecosystem conventions as 048

**Primary Dependencies**:
- `com.faolline.graphimport` (this feature extends it directly, in-place — same package, no new package)
- `com.faolline.graphdialoguesystem` (new dependency for `graphimport`) — `DialogueGraphBuilder`, `DialogueGraph`, `Speaker`, plus the new `AddSubGraph` method this feature adds to it
- `com.faolline.graphcore` (already a dependency) — `SubGraphNodeData` for the sub-dialogue link
- `com.faolline.graphlocalization` (already a transitive presence via `graphdialoguesystem`) — no direct calls made by this feature; relied upon only in the sense that `DialogueGraph` already implements `ILocalizedGraph`, so nothing further is needed for SC-002

**Storage**: N/A (reads a hand-authored interchange JSON file from disk at generation time; writes only `DialogueGraph` assets under `Assets/`, exactly like 048's other generators)

**Testing**: Unity Test Framework, EditMode, run via real Unity batchmode (constitution IV) — interchange parsing/pivot-building/plan logic stays pure C# and testable without Editor state, mirroring 048; asset-generation tests exercise `AssetDatabase` against a scratch folder, same convention as 048's `AssetGenerationTests`

**Target Platform**: Unity Editor (editor-time only, same as 048)

**Project Type**: Library extension (no new package; extends the existing `com.faolline.graphimport` package's Runtime/Editor/Tests)

**Performance Goals**: Same as 048 — not a hot path, interactive-editor-session scale

**Constraints**: Must reuse 048's Plan/Apply/ConflictReport mechanism unchanged in spirit (FR-008, FR-009); must not silently swallow the edge cases in spec.md (dangling reference, duplicate id, bad entry point, reference cycle) — every one is a specific, identifiable error, never a guess (FR-006, FR-007), consistent with 048's established "never guess" precedent (`ReferenceResolutionException`, `PivotFieldParseException`)

**Scale/Scope**: A handful to dozens of dialogues per project for V1 testing purposes; no different order-of-magnitude assumption than 048

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Foundation Stability | N/A — neither `graphimport` nor `graphdialoguesystem` is `graphcore`. |
| II. Universal Abstractions Only | N/A directly; respected transitively — no new domain concepts added to `graphcore` itself. The one `graphcore` type touched (`SubGraphNodeData`) is used exactly as designed, not extended. |
| III. Specification-First | PASS — `spec.md` exists, checklist green, all decisions pre-agreed in prior reflection (zero clarification markers needed). |
| IV. Test-Driven Development | PASS (commitment) — same red→green discipline as 048; interchange parsing and pivot/plan logic are pure C#, keeping the cycle practical. |
| V. Simplicity (YAGNI) | PASS — the interchange format models exactly the 5 node kinds agreed (Line/Choice/End/SubDialogue + the owning Dialogue), nothing speculative; the shared asset resolver is introduced because a SECOND ad hoc copy of the same need already existed as of 048, not preemptively. |
| VI. Typed Context Contract | N/A — no `BaseContext` runtime usage; editor-time generation only. |
| VII. Cross-lib Compatibility via SubGraph Only | PASS — the dialogue-to-dialogue link uses `SubGraphNodeData`, the existing sanctioned mechanism, exactly as `FlowAssetGenerator` already does for quest→content links in 048. No new cross-lib coupling mechanism introduced. |

**New/changed dependency justification**: `com.faolline.graphimport` gains a direct dependency on `com.faolline.graphdialoguesystem` (0.17.2) — needed for `DialogueGraphBuilder`/`DialogueGraph`/`Speaker`, the only way to construct a correct, playable dialogue asset (constitution: "if a concept already exists downstream, use it — never reimplement"; hand-assembling `DialogueLineNodeData` etc. directly would duplicate what the builder already gets right, per its own doc comment about lines needing bespoke handling).

**Upstream change**: `com.faolline.graphdialoguesystem`'s `DialogueGraphBuilder` gains one new method, `AddSubGraph`, mirroring graphstandard's `GraphBuilderBase.AddSubGraph(title, target)` — additive, no existing behavior changes, no other consumer of `DialogueGraphBuilder` is affected.

No violations requiring Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/049-dialogue-import-unity-side/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
com.faolline.graphdialoguesystem/
└── Runtime/Builder/
    └── DialogueGraphBuilder.cs          # MODIFIED: + AddSubGraph(title, target) -> DialogueSubGraphHandle
    └── DialogueSubGraphHandle.cs          # NEW: thin handle, same shape as DialogueBasicHandle

com.faolline.graphimport/
├── package.json                          # MODIFIED: + com.faolline.graphdialoguesystem dependency
├── Runtime/
│   ├── DialoguePivot/                     # NEW folder, mirrors Pivot/ but for the graph-shaped dialogue model
│   │   ├── InterchangeDialogueSet.cs        # raw deserialized shape (1:1 with the JSON)
│   │   ├── PivotDialogue.cs
│   │   ├── PivotDialogueNode.cs             # PivotLine / PivotChoice / PivotEnd / PivotSubDialogueLink, one file each
│   │   └── DialoguePivotBuilder.cs          # interchange -> validated pivot (FR-002, FR-006, FR-007)
│   └── Planning/
│       └── PlanEntryKind.cs                # MODIFIED: + DialogueAsset
├── Editor/
│   ├── Resolution/
│   │   └── IProjectAssetResolver.cs       # NEW: the shared seam — lives in Editor, not Runtime, because its return types (BaseGraph, Speaker) are real Unity assets; see research.md
│   └── Generation/
│       ├── DialogueAssetGenerator.cs        # NEW: IAssetGenerator, builds via DialogueGraphBuilder
│       └── FlowAssetGenerator.cs            # MODIFIED: contentResolver param replaced by IProjectAssetResolver
├── Samples/
│   └── DialogueExample/                   # NEW: hand-authored interchange JSON fixture (stand-in for the future external tool's output)
└── Tests/EditMode/
    ├── DialoguePivotBuilderTests.cs         # NEW
    └── DialogueAssetGenerationTests.cs      # NEW
```

**Structure Decision**: No new package — this is a direct, in-place extension of `com.faolline.graphimport` (048), plus the one small upstream addition to `com.faolline.graphdialoguesystem`. A new `Runtime/DialoguePivot/` folder (rather than folding into the existing `Runtime/Pivot/`) keeps the quest/flow pivot model (order-based, tabular-mapping-driven) and the dialogue pivot model (graph-shaped, direct-JSON-driven) textually separate, since research.md documents why they deliberately don't share a base type — they only share the downstream Plan/Apply/ConflictReport machinery, not the upstream shape.

## Complexity Tracking

*No Constitution Check violations requiring justification.*
