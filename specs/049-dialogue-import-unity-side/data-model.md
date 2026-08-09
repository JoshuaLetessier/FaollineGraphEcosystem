# Phase 1 Data Model: Dialogue Graph Generation from a Pivot Interchange Format

## Interchange stage (raw deserialized JSON — 1:1 with the file)

### InterchangeDialogueSet
- `Dialogues: InterchangeDialogue[]`

### InterchangeDialogue
- `Id: string`
- `Name: string`
- `EntryNodeId: string`
- `Nodes: InterchangeNode[]`

### InterchangeNode (discriminated by `Kind`)
- `Id: string`
- `Kind: "line" | "choice" | "end" | "subDialogue"`
- Line: `SpeakerKey: string`, `Text: string`, `Next: string`
- Choice: `Options: [{ Label: string, Next: string }]`
- End: `Reason: string` (maps to graphcore's `EndReason`), `OutcomeLabel: string?`
- SubDialogue: `TargetDialogue: string` (id-or-name), `Next: string?` (what happens after the sub-dialogue's own end resolves, if anything further in THIS dialogue — see Assumptions)

## Pivot stage (validated, resolved-within-set — FR-001, FR-002)

### PivotDialogue
- `Id: string`
- `Name: string`
- `EntryNodeId: string` (validated to match a node — FR-006)
- `Nodes: IReadOnlyDictionary<string, PivotDialogueNode>` (keyed by node id, validated unique — FR-006)

### PivotDialogueNode (base)
- `Id: string`

### PivotLine : PivotDialogueNode
- `SpeakerKey: string` (raw, copied through — see research.md §5)
- `Text: string`
- `Next: string` (validated to reference an existing node in the same dialogue — FR-006)

### PivotChoice : PivotDialogueNode
- `Options: IReadOnlyList<PivotChoiceOption>`

### PivotChoiceOption
- `Label: string`
- `Next: string` (validated — FR-006)

### PivotEnd : PivotDialogueNode
- `Reason: string` (raw, e.g. `"Completed"` — kept as a string rather than graphcore's `EndReason` enum so `graphimport`'s Runtime assembly, deliberately `noEngineReferences: true`, never needs to reference `graphcore.Runtime`, which is not itself engine-agnostic; parsed into the real `EndReason` only in `DialogueAssetGenerator`, Editor-side, where that reference already exists)
- `OutcomeLabel: string?`

### PivotSubDialogueLink : PivotDialogueNode
- `TargetDialogueRef: PivotReference` (reuses 048's existing `PivotReference` shape — `TargetTable`/`TargetId` — resolved by id-or-name against the full `InterchangeDialogueSet`, not against arbitrary source tables; validated acyclic across the whole set — FR-007)
- `Next: string?`

**Validation rules** (all at `DialoguePivotBuilder.Build` time, before any asset is touched — mirrors 048's fail-fast precedent):
- Every node id within one `PivotDialogue` MUST be unique (FR-006).
- `EntryNodeId` MUST match a node in the same dialogue (FR-006).
- Every `Next`/`Options[].Next` MUST match a node id in the same dialogue (FR-006) — a sub-dialogue link is a jump to ANOTHER dialogue's own entry, not a `Next` target itself, so this rule is scoped per-dialogue.
- `TargetDialogueRef` MUST resolve to exactly one dialogue in the set by id or name, or the interchange set is rejected outright (this is a hard validation failure, not a "resolved to null at generation time" case — the target dialogue's *identity* must exist in the declared set even if the actual `DialogueGraph` asset for it doesn't exist on disk yet; see `IProjectAssetResolver` below for the disk-level distinction).
- No `PivotSubDialogueLink` cycle may exist across the full set (FR-007).

## Generation stage (Editor)

### IProjectAssetResolver
- `BaseGraph ResolveGraph(string targetTable, string targetId)` — used for both 048's quest-content refs and this feature's sub-dialogue refs.
- `Speaker ResolveSpeaker(string speakerKey)`
- Default V1 implementation: both methods return null (see research.md §3) — a real disk-lookup implementation is out of scope for this feature.

### DialogueAssetGenerator : IAssetGenerator
- Consumes a `PlanEntry` whose `Data` is a `PivotDialogue`.
- Walks the pivot's nodes via `DialogueGraphBuilder` (`AddLine`, `AddChoice`+`Option`, `AddEnd`, the new `AddSubGraph`), wiring `.To(...)` edges per each node's `Next`/`Options[].Next`.
- For each `PivotSubDialogueLink`, calls `IProjectAssetResolver.ResolveGraph` — null result becomes an `AddSubGraph(title, target: null)` (graphcore's documented "incomplete node" state, per FR-005), never a thrown exception.
- For each distinct `SpeakerKey` referenced, calls `IProjectAssetResolver.ResolveSpeaker` once and, if non-null, registers it via `WithSpeaker` (best-effort enrichment, per research.md §5).
- Saves via the same `GraphAssetBuilder.Save` used by 048's other generators.

## Relationship to 048's existing types

```
InterchangeDialogueSet ──(DialoguePivotBuilder, validates)──> PivotDialogue[]
PivotDialogue ──(PlanBuilder-equivalent, one PlanEntry per dialogue, Kind=DialogueAsset)──> PlanEntry
PlanEntry ──(PlanApplier, unchanged from 048)──> ConflictReport / DialogueAssetGenerator ──> DialogueGraph asset
PivotSubDialogueLink.TargetDialogueRef : PivotReference   # the SAME type 048 already uses for quest→content refs
IProjectAssetResolver                                      # the SAME resolver FlowAssetGenerator (048) and DialogueAssetGenerator (this feature) both use
```

## Assumptions carried into this data model

- A `PivotSubDialogueLink`'s own `Next` (what happens in the OUTER dialogue after the sub-dialogue's flow ends) is optional — if absent, the outer dialogue's flow simply ends when the sub-dialogue's own end is reached (matches how `SubGraphNodeData`/the execution stack already handles a graph-call-return at the engine level, per 048's research). This is an implementation-time detail to confirm against the real runtime behavior while building `DialogueAssetGenerator`, not a blocking design question.
