# Implementation Plan: Break Hard Graph-to-Graph Asset References (Soft Graph Links)

**Branch**: `047-graph-soft-links` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/047-graph-soft-links/spec.md`

## Summary

A root graph's build/bundle closure today pulls in every graph reachable through
`GraphLinkNodeData` (a purely documentary, non-executing annotation) exactly as if it were a real
dependency, because Unity treats any serialized `BaseGraph` field as one. This plan replaces that
one field with a GUID-backed soft reference (Lot 1, `graphcore`) behind an **unchanged public
property signature** so zero editor-ergonomics or call-site changes are needed elsewhere. It then
adds the runtime/editor seams a multi-root-graph project needs regardless of Addressables — an
`IGraphCatalog` port mirroring the existing `ISceneLoader` pattern (Lot 2, required independently
by `graphsave`'s restore path) and an editor `GraphKeySourceRegistry` mirroring
`SceneKeySourceRegistry` (Lot 3) — and finally an optional Addressables adapter (Lot 4) that can
preload a soft-referenced next chapter ahead of time. Two new `GraphValidator` rules replace the
compile-time safety net the hard reference used to provide. `BaseRunner` stays synchronous and
unchanged throughout; `SubGraphNodeData` keeps its hard reference unchanged.

## Technical Context

**Language/Version**: C# (Unity 2022+ scripting runtime, matching the rest of the ecosystem)

**Primary Dependencies**: `com.faolline.graphcore` (existing), `com.faolline.graphgameflow`
(existing), `com.unity.addressables` (Lot 4 only, already a dependency of
`com.faolline.graphgameflow.addressables`) — no *new* external package dependency is introduced
anywhere.

**Storage**: Unity `ScriptableObject` assets (`.asset` files), same as every other graph/node
asset in the ecosystem. No new save-data schema (`graphsave`'s `GraphRunSnapshot` is untouched).

**Testing**: EditMode (Unity Test Framework), run via Coplay MCP `run_tests` per Constitution IV.
Lot 4's Addressables-Analyze acceptance check (spec SC-001/SC-007) additionally requires a real
Addressables build pass — not achievable as a pure EditMode assertion, and called out explicitly
as such in `quickstart.md`.

**Target Platform**: Unity Editor (all authoring/validation) + any player platform the ecosystem
already supports (no new platform constraint — the whole point is *removing* a forced dependency
from the player build, not adding one).

**Project Type**: Unity UPM package library (multi-package monorepo) — not an app/service.

**Performance Goals**: N/A in the traditional sense; the operative "performance" goal is the build
artifact itself — a root graph's build/bundle inclusion group must shrink by the excluded target's
full transitive size (spec SC-001), verified via Addressables Analyze, not a runtime metric.

**Constraints**:
- `BaseRunner` MUST remain synchronous, headless, engine-free — no new `RunnerState`, no `async`.
- `com.faolline.graphcore` MUST NOT reference `com.unity.addressables`, directly or transitively.
- `SubGraphNodeData` MUST NOT change (field, type, or ergonomics).
- No data migration path is provided for `GraphLinkNodeData`'s serialized field rename (explicit,
  session-recorded requester sign-off — see `research.md` R8).

**Scale/Scope**: 3 packages touched (`graphcore`, `graphgameflow`, `graphgameflow.addressables`),
1 package consumed-but-untouched (`graphsave`). ~9 new types/members across Lots 2-4, 1 modified
node type + 2 new validator rules in Lot 1.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

| Principle | Status | Notes |
|---|---|---|
| I. Foundation Stability | ⚠️ Justified deviation | Public `TargetGraph` API signature is preserved (no deprecation cycle needed for it). The *private serialized field* rename (`_targetGraph` → `_targetGraphGuid`) is, read literally, still inside "BaseNodeData fields are append-only." Recorded as an explicit, authorized deviation — see Complexity Tracking below and `research.md` R8. |
| II. Universal Abstractions Only / no lib knowledge in graphcore | ✅ Pass | Lot 1 stays entirely within `graphcore`; no new reference to any downstream lib or to Addressables. |
| III. Specification-First | ✅ Pass | `spec.md` exists, reviewed via checklist (16/16 pass), no unresolved `[NEEDS CLARIFICATION]`. |
| IV. Test-Driven Development | ✅ Pass (procedural, enforced at `/speckit-tasks` + implementation) | Plan defers actual test-writing to tasks.md/implementation phase per the constitution's own gate ordering; `quickstart.md` enumerates the scenarios each task must cover, red-green-refactor. |
| V. Simplicity (YAGNI) | ✅ Pass | `TargetGraph`'s signature-preserving design (R1) avoids touching 2 Editor files that would otherwise need changes; `DirectGraphCatalog` is the minimum viable non-Addressables implementation, not a speculative plugin system. |
| VI. Typed Context Contract | ✅ Pass | `GameFlowContext.GraphCatalog` is a runtime-service field, mirroring the existing `SceneLoader` treatment exactly — not smuggled in as a string/bool context parameter. |
| VII. Cross-lib Compatibility via SubGraph Only | ✅ Pass, reinforced | `SubGraphNodeData` unchanged; the new validator rule (Lot 1+3) exists specifically to catch an *accidental* misuse of this exact mechanism across a chapter boundary — it strengthens, not weakens, this principle. |
| Dependencies (Development Standards) | ✅ Pass | Addressables usage stays confined to the already-Addressables-dependent adapter package; no new dependency category anywhere. |

## Project Structure

### Documentation (this feature)

```text
specs/047-graph-soft-links/
├── plan.md              # This file
├── research.md          # Phase 0 output — 9 resolved design decisions (R1-R9)
├── data-model.md         # Phase 1 output — modified/new types per package
├── contracts/            # Phase 1 output — public API surface per lot
│   ├── graphlink-soft-reference.md
│   ├── graph-catalog-port.md
│   └── addressables-adapter.md
├── quickstart.md         # Phase 1 output — 6 verification walkthroughs tied to acceptance criteria
└── tasks.md              # Phase 2 output (/speckit-tasks — NOT created by this command)
```

### Source Code (repository root)

```text
com.faolline.graphcore/
├── Runtime/Nodes/GraphLinkNodeData.cs                # MODIFY (Lot 1)
├── Editor/Tools/GraphValidator.cs                    # MODIFY (Lot 1 — 2 new rules)
├── Editor/Tools/GraphValidatorExtensionRegistry.cs   # NEW (generic seam — see research.md R9; required so rule 2 never references graphgameflow)
├── Editor/Nodes/GraphLinkNodeView.cs                 # unchanged (verified — no edit needed)
├── Editor/Inspector/BaseNodeInspectorView.cs         # unchanged (verified — no edit needed)
└── Tests/EditMode/
    ├── GraphLinkSoftReferenceTests.cs                # NEW
    └── GraphValidatorSoftLinkTests.cs                # NEW (extends existing GraphValidator test file/suite)

com.faolline.graphgameflow/
├── Runtime/Graph/IGraphCatalog.cs                    # NEW (Lot 2)
├── Runtime/Graph/DirectGraphCatalog.cs               # NEW (Lot 2)
├── Runtime/Context/GameFlowContext.cs                # MODIFY (Lot 2 — GraphCatalog property)
├── Editor/Inspector/GraphKeySourceRegistry.cs        # NEW (Lot 3, mirrors SceneKeySourceRegistry.cs)
├── Editor/Tools/GraphKeyRegistryWindow.cs            # NEW (Lot 3)
├── Editor/Tools/ChapterRootSubGraphValidatorExtension.cs  # NEW (Lot 3 — registers into graphcore's new seam)
└── Tests/EditMode/
    ├── DirectGraphCatalogTests.cs                    # NEW
    ├── GameFlowContextGraphCatalogTests.cs            # NEW
    ├── GraphKeySourceRegistryTests.cs                 # NEW
    └── ChapterRootSubGraphValidatorExtensionTests.cs  # NEW

com.faolline.graphgameflow.addressables/
├── Runtime/AddressablesGraphCatalog.cs               # NEW (Lot 4)
├── Runtime/PreloadNextChapterAction.cs               # NEW (Lot 4)
├── Editor/AddressablesGraphKeyProvider.cs            # NEW (Lot 4, mirrors AddressablesSceneKeyProvider.cs)
└── Tests/EditMode/
    ├── AddressablesGraphCatalogTests.cs               # NEW
    └── AddressablesGraphKeyProviderTests.cs           # NEW

com.faolline.graphsave/                               # NOT MODIFIED — consumes Lot 2 only
```

**Structure Decision**: Feature spans 3 existing UPM packages, each following its own established
`Runtime/` `Editor/` `Tests/EditMode/` `Tests/PlayMode/` asmdef split (verified against each
package's actual `.asmdef` files — no new assembly needed anywhere; `AddressablesGraphCatalog`
and `PreloadNextChapterAction` slot into the existing
`com.faolline.graphgameflow.addressables.Runtime` asmdef, which already references
`Unity.Addressables`/`Unity.ResourceManager`). No new package is created.

## Complexity Tracking

> Justifying the one Constitution Check item flagged above.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `GraphLinkNodeData`'s private serialized field is renamed/retyped (`_targetGraph: BaseGraph` → `_targetGraphGuid: string`) without a deprecation-cycle minor version, technically inside the literal scope of Principle I's "BaseNodeData fields are append-only" | A GUID-keyed soft reference and a hard `BaseGraph` reference cannot coexist as two representations of the same one concept without dead/ambiguous state (which one wins?); a straight swap is the only representation with no redundant state to keep in sync | **Keep both fields for one deprecation minor version** (the proposal's originally-considered path) — rejected because for the *entire* deprecation window the old hard field would still exist and still force the build-time pull this feature exists to eliminate, i.e. it would ship a version of the fix that doesn't fix anything yet, to protect zero real consumers (explicitly confirmed by the requester in-session: no project uses this field deeply). Protecting nobody at the cost of shipping a non-functional interim state fails Principle V (Simplicity) in the opposite direction. |

## Post-Design Constitution Re-Check

Re-verified after Phase 1 (`data-model.md`, `contracts/`): the concrete design surfaced one
**real** Principle II violation in the first draft — validator rule 2 (SubGraph crossing a chapter
boundary) was initially written as `GraphValidator` (graphcore) calling `GraphKeySourceRegistry`
(graphgameflow) directly, which is exactly the "graphcore has zero knowledge of any downstream
lib" rule this principle exists to enforce. This is precisely what the Constitution Check gate is
for — it was caught before task generation, not during implementation. Corrected (research.md R9):
graphcore gains a small generic `IGraphValidatorExtension`/`GraphValidatorExtensionRegistry` seam
(mirroring the ecosystem's existing `ContextKeyLabelRegistry` precedent), and
`graphgameflow.Editor` registers the concrete chapter-root check into it. `data-model.md`,
`contracts/`, and the Source Code structure above are updated to reflect this.

`TargetGraphGuid`'s addition (R3) and `IGraphKeySourceProvider.TryResolveGuid`'s addition (R7) are
both purely additive new members, not covered by the Complexity Tracking item above (which
concerns only the field *removal/rename*, not the new members). Gate: **PASS** with the one
documented, authorized deviation (Complexity Tracking) plus the R9 correction above.
