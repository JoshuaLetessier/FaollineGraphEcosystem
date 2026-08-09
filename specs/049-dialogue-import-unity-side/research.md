# Phase 0 Research: Dialogue Graph Generation from a Pivot Interchange Format

## 1. Why the dialogue pivot doesn't reuse 048's `MappingConfig`/table-mapping machinery

**Decision**: The interchange format is deserialized directly into the pivot shape (`InterchangeDialogueSet` → `PivotDialogue`) — no `MappingConfig`, no `TableMapping`, no column-name indirection.

**Rationale**: 048's mapping layer exists to translate an *arbitrary, pre-existing, user-owned* spreadsheet shape into the pivot, because the source data (a production tracking sheet) was never designed for this tool. A dialogue interchange file has no such history — the (future) external tool will emit the pivot's own shape by construction, so there is nothing to map *from*. Adding a mapping layer here would be pure indirection with no real source-shape variance to absorb (YAGNI).

**Alternatives considered**: Reusing `MappingConfig` with a fixed "identity" mapping — rejected, adds a layer that resolves nothing.

## 2. Why the dialogue pivot doesn't reuse 048's `PivotBuilder`/`Order`+branch-strategy machinery

**Decision**: A separate `DialoguePivotBuilder` processes the interchange set into `PivotDialogue`s, unrelated to quest/step/branch types.

**Rationale**: 048's `Order`-position + `DeclaredColumnBranchStrategy` model exists specifically to reconstruct a graph shape *from* flat tabular rows with a shared-position convention. A dialogue interchange file is already graph-shaped (explicit node ids + explicit "next" pointers, agreed with the user) — there is no flattening to undo, so there is no analogous "detect branch groups from shared position" step. Choices are declared as first-class nodes with explicit options, not inferred.

**Alternatives considered**: Generalizing `PivotBuilder`/`IBranchDetectionStrategy` to cover both shapes — rejected: the two source shapes (flat rows vs. an already-graph-shaped file) don't actually share meaningful structure once you get past "both eventually build something with nodes." Forcing a shared abstraction here would be the "false economy" kind of reuse the constitution's simplicity principle warns against; the two pivot builders legitimately only share the *downstream* Plan/Apply/ConflictReport stage, which they already do.

## 3. `IProjectAssetResolver` placement (Editor, not Runtime)

**Decision**: `IProjectAssetResolver` lives in `com.faolline.graphimport`'s **Editor** assembly, not Runtime.

**Rationale**: Its job is "given an external identifier, find the real Unity asset" — its return types are inherently Unity assets (`BaseGraph` for a target dialogue/content graph, `Speaker` for a speaker). `graphimport`'s Runtime assembly is deliberately `noEngineReferences: true` (048's own design choice, to keep parsing/pivot/plan logic portable and trivially testable). Putting an interface typed against `BaseGraph`/`Speaker` in Runtime would force Runtime to take on engine references for a concern (Editor-time asset lookup) that only ever runs at Editor-time anyway — both existing consumers (`FlowAssetGenerator`, the new `DialogueAssetGenerator`) already live in Editor. No conflict, no exception needed to the noEngineReferences rule.

**Shape**:
```csharp
public interface IProjectAssetResolver
{
    BaseGraph ResolveGraph(string targetTable, string targetId);   // used for quest content refs AND sub-dialogue refs
    Speaker ResolveSpeaker(string speakerKey);
}
```
A single default implementation returns null from both methods for V1 (matching 048's existing precedent exactly — a null `TargetGraph` is graphcore's own documented "incomplete node" state, not a crash). `FlowAssetGenerator`'s constructor parameter changes from `Func<PivotReference, BaseGraph>` to `IProjectAssetResolver` (source-compatible in spirit — same "resolve or null" contract, now shared).

**Alternatives considered**: Two separate `Func<...>` delegates as before (status quo) — rejected per spec FR-009 and the prior reflection's explicit decision: one seam, not two.

## 4. `DialogueGraphBuilder.AddSubGraph` — shape and placement

**Decision**: Add to `com.faolline.graphdialoguesystem`'s `DialogueGraphBuilder`:
```csharp
public DialogueSubGraphHandle AddSubGraph(string title = null, BaseGraph target = null)
```
mirroring graphstandard's `GraphBuilderBase.AddSubGraph(string title, BaseGraph target)` signature exactly, returning a new thin `DialogueSubGraphHandle : DialogueNodeHandle<DialogueSubGraphHandle>` (same shape as the existing `DialogueBasicHandle` used by `AddEnd`) so it wires into the builder's existing `.To(...)` edge machinery with no special-casing.

**Rationale**: `SubGraphNodeData` is a universal graphcore node — nothing dialogue-specific about it, unlike `DialogueLineNodeData`/`ChoiceNodeData`+`DialogueChoice` which need the builder's bespoke handling (per the builder's own doc comment). Matching graphstandard's existing method name/signature exactly means anyone already familiar with `GraphBuilder<TGraph>.AddSubGraph` needs to learn nothing new to use it on a `DialogueGraphBuilder`.

**Alternatives considered**: Constructing `SubGraphNodeData` directly via `graph.AddNode(...)` after `Build()`, bypassing the builder entirely, kept local to `graphimport` — rejected: would leave `com.faolline.graphdialoguesystem`'s builder permanently unable to express a sub-dialogue link for ANY future consumer, not just this pipeline; the fix belongs one layer down, per constitution ("if a concept already exists in graphcore, downstream libs must use it — never reimplement or shadow it").

## 5. Speaker resolution is not required for line correctness

**Decision**: `DialoguePivotBuilder` copies a `PivotLine`'s speaker identifier straight through as a plain string (matching `DialogueLineNodeData.SpeakerKey`, which is *documented* as "not translated — used to look up the speaker at runtime" and only needs to match an existing `Speaker.SpeakerId`). `IProjectAssetResolver.ResolveSpeaker` is used only as a best-effort enrichment — if it resolves, `DialogueGraphBuilder.WithSpeaker(...)` registers the real asset on the graph for self-containment (per its own doc comment: "so the scene needs no separate speaker list"); if it doesn't resolve, the line still carries the correct `SpeakerKey` string and plays correctly as long as the speaker is available some other way at runtime (e.g. assigned in the scene).

**Rationale**: Discovered by reading `DialogueLineNodeData.cs` directly rather than assuming — the speaker "reference" in the interchange format was never going to be a hard blocking dependency the way a sub-dialogue link is (a missing sub-dialogue graph genuinely can't be played; a missing speaker registration is a convenience gap). This narrows FR-005's "recognized incomplete state" for speakers specifically to "optional enrichment not performed," not "broken link."

## 6. Localization — confirmed, no new code (SC-002)

**Decision**: No localization-specific code in this feature. `DialogueLineNodeData`'s text lives on `Title` (inherited from `BaseNodeData`), consumed by `DialogueTitleProvider` with no table needed, or auto-indexed into a real table by `graphlocalization`'s existing per-graph adapter scan (`DialogueGraph : ILocalizedGraph`) once one exists — this pipeline only needs to set `Title` correctly on every generated line, exactly as `AddLine(speakerKey, text)`/`.Say(text)` already do.

**Rationale**: Confirmed directly in `DialogueGraph.cs`/`DialogueLineNodeData.cs` source during the prior reflection and re-confirmed here — no assumption left unverified.

## 7. Cycle detection for sub-dialogue references (FR-007)

**Decision**: `DialoguePivotBuilder` performs its own DFS cycle check across `PivotSubDialogueLink` references while building the full set of requested dialogues (it has all of them in memory at once, from one interchange file/set), raising a specific exception naming the cycle — before generation, not relying on graphcore's own runtime/edit-time cycle detection (which exists for `SubGraphNodeData` in general, per the constitution, but operates graph-by-graph on already-built assets, not across a still-in-memory pivot set that hasn't produced any assets yet).

**Rationale**: Catching it at pivot-build time gives a cleaner, earlier, more specific error (FR-007's "identifiable error... rather than processing it indefinitely") than letting generation proceed and relying on a later stage to catch it — consistent with 048's general "fail as early and as specifically as possible" pattern (`MappingConfig.Validate`, `ReferenceResolutionException`, `PivotFieldParseException`).
