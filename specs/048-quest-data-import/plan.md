# Implementation Plan: Quest & Flow Graph Generation from Structured Data

**Branch**: `048-quest-data-import` | **Date**: 2026-08-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/048-quest-data-import/spec.md`

## Summary

A new package (`com.faolline.graphimport`) that turns structured external data (CSV/JSON tables) into `graphquest` and `graphgameflow` assets, through a declarative per-table mapping (fields, ignored columns, ID-or-name reference resolution) feeding an internal pivot model (quests, ordered steps, branches, cross-references). Generation is a pure plan-then-apply pipeline: a headless, deterministic **Plan** phase turns pivot data into a list of proposed assets and paths without touching disk, and an **Apply** phase commits that plan either unattended (CI) or through an Editor review window — both consuming the same plan and the same conflict report, which never allows a silent overwrite or a silent skip.

## Technical Context

**Language/Version**: C# (Unity 6000.0, matching the rest of the ecosystem)

**Primary Dependencies**:
- `com.faolline.graphcore` — `BaseGraph`/`GraphAsset`, `SubGraphNodeData` (step→puzzle/dialogue references), signal-await primitives (branch gating)
- `com.faolline.graphquest` — fluent quest/objective builder, consumed to construct the quest/objective output
- `com.faolline.graphgameflow` — flow/scene orchestration primitives, consumed to construct the playable branching flow output
- `com.unity.nuget.newtonsoft-json` (new dependency, Unity's official Newtonsoft package) — for JSON table parsing and for the mapping configuration file format; `JsonUtility` cannot represent the mapping config's nested/dictionary shape (per-table field maps, per-reference resolution rules) or arbitrary-shape JSON source tables

**Storage**: N/A (reads user-provided CSV/JSON files from disk at generation time; writes only `GraphAsset` files under `Assets/`, no runtime storage)

**Testing**: Unity Test Framework, EditMode only, run via Coplay MCP `run_tests` (constitution IV) — mapping/resolution/pivot/plan logic is pure C# and testable without any asset I/O; apply-phase tests exercise `AssetDatabase` against a scratch folder, per the ecosystem's existing headless-testing convention

**Target Platform**: Unity Editor (tool is editor-time only; no runtime/player component)

**Project Type**: Library (Unity Editor tooling package, code-first + optional Editor UI)

**Performance Goals**: Not a hot path — a full plan generation over a few thousand source rows should complete well within an interactive editor session (sub-second to low seconds), not blocking the main thread noticeably

**Constraints**: Must never write to disk during the Plan phase (FR-009); must never silently overwrite or silently skip a colliding asset during Apply (FR-012); reference resolution must fail loud, never guess (FR-003)

**Scale/Scope**: Design-time tool sized for a single project's content set — tens to low hundreds of quests, low thousands of steps/rows across linked tables; not built for multi-project or massive-scale batch processing

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Foundation Stability | N/A — this is a new downstream package, not `graphcore`. |
| II. Universal Abstractions Only | N/A to this package directly; respected transitively — `graphimport` adds no domain concepts to `graphcore` itself. |
| III. Specification-First | PASS — `spec.md` exists, checklist green, one clarification resolved with the user before planning. |
| IV. Test-Driven Development | PASS (commitment) — tasks will be sequenced red→green per the constitution; plan/mapping/resolution logic is pure C#, making the "write failing test first" cycle straightforward without Editor state. |
| V. Simplicity (YAGNI) | PASS — no speculative support for spreadsheet shapes beyond what the reference dataset and spec require; branch detection is an explicit pluggable strategy but only one concrete strategy (declared-column) ships in V1. |
| VI. Typed Context Contract | N/A — this package does not touch `BaseContext` at runtime; it only authors assets at edit time. |
| VII. Cross-lib Compatibility via SubGraph Only | PASS, with a note — `graphimport` depends on **both** `graphquest` and `graphgameflow` simultaneously, which no existing lib does (each domain lib depends only on `graphcore` and shared foundations). This is judged compliant because principle VII restricts coupling *within generated graph content* (one lib's graph invoking another's, restricted to `SubGraphNodeData`) — it does not forbid an editor-time tool from consuming multiple libs' public authoring APIs to *produce* assets. `graphimport` itself contains no runtime graph logic and is never referenced by `graphcore`, `graphquest`, or `graphgameflow`. Generated cross-references between a flow step and its puzzle/dialogue content use `SubGraphNodeData`, per VII, not a new coupling mechanism. |

**New dependency justification** (per Development Standards): `com.unity.nuget.newtonsoft-json` is added because the mapping configuration (nested per-table field/reference declarations) and arbitrary-shape JSON source tables cannot be represented with `JsonUtility`. It is Unity's own officially distributed package, already a common transitive presence in Unity projects, and used only within `graphimport`'s parsing layer — no other package needs to take it as a dependency.

No violations requiring Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/048-quest-data-import/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
com.faolline.graphimport/
├── package.json
├── Runtime/
│   ├── com.faolline.graphimport.Runtime.asmdef      # no UnityEditor, no UnityEngine dependency where avoidable
│   ├── Mapping/            # MappingConfig, per-table field/reference declarations, config (de)serialization
│   ├── Resolution/         # IReferenceResolver + ID-or-name resolver, ambiguity/not-found error types
│   ├── Sources/             # IRowSource + CSV reader, JSON reader (raw rows in, independent of mapping)
│   ├── Pivot/               # PivotQuest, PivotStep, PivotBranch, PivotBuilder (mapping + rows -> pivot)
│   ├── Branching/           # IBranchDetectionStrategy + declared-column strategy
│   └── Planning/            # GenerationPlan, PlanEntry, IPathTemplateResolver, PlanBuilder (pivot -> plan)
├── Editor/
│   ├── com.faolline.graphimport.Editor.asmdef
│   ├── Apply/                # PlanApplier (AssetDatabase writes), ConflictDetector, ConflictReport
│   ├── Generation/           # QuestAssetGenerator (graphquest builder), FlowAssetGenerator (graphgameflow builder)
│   └── Window/                # Editor review window (plan preview, per-asset path override, commit)
├── Samples/
│   └── CryptiqueExample/     # sanitized sample dataset (from the real reference spreadsheet) + mapping config
└── Tests/
    └── EditMode/
        └── com.faolline.graphimport.Tests.EditMode.asmdef
```

**Structure Decision**: Single new UPM package at repo root, `Runtime`/`Editor`/`Tests` split following the existing convention (`graphquest`, `graphgameflow`, etc.). `Runtime` holds every piece that is pure data transformation (parsing, mapping, resolution, pivot building, plan building) so it stays headlessly testable without `AssetDatabase`; `Editor` holds everything that touches `AssetDatabase` or Editor UI (the actual apply step, the review window, and the calls into `graphquest`/`graphgameflow`'s builders to construct real `GraphAsset`s). This mirrors the Runtime/Runtime.Core-style separation already validated in the ecosystem (046 GraphCore split) applied at the Runtime/Editor boundary instead.

## Complexity Tracking

*No Constitution Check violations requiring justification.*
