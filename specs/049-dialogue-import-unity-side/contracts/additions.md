# Contracts: additions for Dialogue Graph Generation

Everything here is additive to 048's existing `Faolline.GraphImport.*` contracts (see `specs/048-quest-data-import/contracts/runtime-api.md`), plus one addition to `com.faolline.graphdialoguesystem`.

## `com.faolline.graphdialoguesystem` (Runtime — namespace `Faolline.GraphDialogue`)

```csharp
public sealed class DialogueGraphBuilder
{
    // ... existing AddLine/AddChoice/AddEnd/WithSpeaker/Build unchanged ...

    /// Adds a SubGraph node delegating to `target` (mirrors graphstandard's GraphBuilderBase.AddSubGraph).
    public DialogueSubGraphHandle AddSubGraph(string title = null, BaseGraph target = null);
}

public sealed class DialogueSubGraphHandle : DialogueNodeHandle<DialogueSubGraphHandle>
{
    // Inherits .To(target), .AsEntry(), etc. from DialogueNodeHandle<T> — no members of its own beyond the base.
}
```

## `com.faolline.graphimport` (Runtime — namespace `Faolline.GraphImport`)

```csharp
// Interchange (raw deserialized JSON)
public sealed class InterchangeDialogueSet
{
    public static InterchangeDialogueSet LoadFromJson(string json);
    public IReadOnlyList<InterchangeDialogue> Dialogues { get; }
}

// Pivot
public sealed class PivotDialogue
{
    public string Id { get; }
    public string Name { get; }
    public string EntryNodeId { get; }
    public IReadOnlyDictionary<string, PivotDialogueNode> Nodes { get; }
}

public abstract class PivotDialogueNode { public string Id { get; } }
public sealed class PivotLine : PivotDialogueNode { public string SpeakerKey; public string Text; public string Next; }
public sealed class PivotChoice : PivotDialogueNode { public IReadOnlyList<PivotChoiceOption> Options; }
public sealed class PivotChoiceOption { public string Label; public string Next; }
public sealed class PivotEnd : PivotDialogueNode { public EndReason Reason; public string OutcomeLabel; }
public sealed class PivotSubDialogueLink : PivotDialogueNode { public PivotReference TargetDialogueRef; public string Next; }

public sealed class DialoguePivotBuilder
{
    // Validates uniqueness, entry point, dangling Next refs, and cross-dialogue reference cycles
    // (FR-006, FR-007) before returning anything. Throws a specific exception type per failure kind
    // (mirrors 048's ReferenceResolutionException/PivotFieldParseException/BranchDetectionException
    // pattern — table/dialogue/node identifying context on every thrown exception).
    public IReadOnlyList<PivotDialogue> Build(InterchangeDialogueSet interchange);
}

// Planning (extends 048's existing PlanEntryKind)
public enum PlanEntryKind { QuestAsset, FlowAsset, DialogueAsset }
```

## `com.faolline.graphimport` (Editor — namespace `Faolline.GraphImport.Editor`)

```csharp
// The shared seam (048's FlowAssetGenerator is retrofitted to take this instead of its old
// Func<PivotReference, BaseGraph> contentResolver parameter).
public interface IProjectAssetResolver
{
    BaseGraph ResolveGraph(string targetTable, string targetId);
    Speaker ResolveSpeaker(string speakerKey);
}
public sealed class NullProjectAssetResolver : IProjectAssetResolver
{
    // Both methods return null — V1's only implementation, matching 048's existing precedent
    // that a null SubGraphNodeData.TargetGraph is a valid, documented "incomplete" state.
}

public sealed class DialogueAssetGenerator : IAssetGenerator
{
    public DialogueAssetGenerator(IProjectAssetResolver resolver);
    public void Generate(PlanEntry entry); // entry.Data is a PivotDialogue
}
```
