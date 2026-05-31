# Public API Contract: graphdialoguesystem (MVP)

**Feature**: `010-graphdialoguesystem-mvp` | **Date**: 2026-05-31

This is the public surface a game (runtime) or tooling (editor) consumer depends on. Signatures are the
**contract**; tests assert against them. Types live in `Faolline.GraphDialogue` unless noted. Built on
graphcore v0.2.0 public types (`BaseGraph`, `BaseContext`, `BaseRunner`, `BaseChoice`, `BaseCondition`,
`BaseAction`, `EndReason`, …).

## Localization

```csharp
public interface ILocalizationProvider
{
    // Active locale code (e.g. "en", "fr"). Never null.
    string CurrentLocale { get; }

    // Resolve key in the given locale. Absent key → defined fallback (e.g. "#"+key) + [GraphDialogue]
    // warning; never returns null/empty for a non-empty key.
    string Resolve(string key, string locale);
}

// Default, dependency-free provider.
public sealed class CsvLocalizationProvider : ILocalizationProvider
{
    public CsvLocalizationProvider(string csvText, string currentLocale);
    public string CurrentLocale { get; }
    public void SetLocale(string locale);
    public string Resolve(string key, string locale);
}

// Lightweight selection of active provider + locale; safe default when unconfigured.
public sealed class LocalizationSettings
{
    public ILocalizationProvider Provider { get; set; }   // null → safe default provider
    public string CurrentLocale { get; set; }
    public string Resolve(string key);                    // uses Provider + CurrentLocale
}
```

Optional adapter (namespace `Faolline.GraphDialogue.Localization.Unity`, separate assembly, present only
when `com.unity.localization` is installed):

```csharp
public sealed class UnityLocalizationProvider : ILocalizationProvider
{
    public UnityLocalizationProvider(string tableCollectionName);
    public string CurrentLocale { get; }
    public string Resolve(string key, string locale);
}
```

## Typed context (Principle VI)

```csharp
public class DialogueContext : BaseContext
{
    public bool   Flag    { get; set; }   // via DialogueContextKeys.Flag
    public int    Counter { get; set; }   // via DialogueContextKeys.Counter
    public float  Amount  { get; set; }   // via DialogueContextKeys.Amount
    public string Tag     { get; set; }   // via DialogueContextKeys.Tag
    protected override BaseContext CreateCloneInstance(); // new DialogueContext()
}

public static class DialogueContextKeys
{
    public const string Flag    = "flag";
    public const string Counter = "counter";
    public const string Amount  = "amount";
    public const string Tag     = "tag";
}
```

## Graph & domain data

```csharp
[CreateAssetMenu(menuName = "GraphDialogue/Dialogue Graph", fileName = "NewDialogueGraph")]
public class DialogueGraph : BaseGraph { }

[Serializable]
public class DialogueLineNodeData : StatementNodeData
{
    public const string NodeTypeId = "graphdialogue/line";
    public string SpeakerKey    { get; set; }
    public string TextKey       { get; set; }
    public string ExpressionKey { get; set; }   // default "neutral"
}

[Serializable]
public class DialogueChoice : BaseChoice
{
    public string DisplayTextKey { get; set; }  // inherits Id, Condition
}

[CreateAssetMenu(menuName = "GraphDialogue/Speaker", fileName = "NewSpeaker")]
public class Speaker : ScriptableObject
{
    public string SpeakerId          { get; }
    public string DisplayNameKey     { get; }
    public string DisplayNameFallback{ get; }
    public IReadOnlyList<SpeakerExpression> Expressions { get; }
    public bool TryGetExpression(string key, out UnityEngine.Object asset);
}

[Serializable] public class SpeakerExpression { public string Key; public UnityEngine.Object Asset; }
```

## Conditions & actions (inline)

```csharp
public enum ComparisonOperator { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }

// Conditions : BaseCondition  →  bool Evaluate(BaseContext)
public class AlwaysTrueCondition  : BaseCondition { }
public class AlwaysFalseCondition : BaseCondition { }
public class BoolCondition   : BaseCondition { public string ParameterKey; public bool   ExpectedValue; }
public class IntCondition    : BaseCondition { public string ParameterKey; public ComparisonOperator Operator; public int   ExpectedValue; }
public class FloatCondition  : BaseCondition { public string ParameterKey; public ComparisonOperator Operator; public float ExpectedValue; }
public class StringCondition : BaseCondition { public string ParameterKey; public string ExpectedValue; public bool Negate; }

// Actions : BaseAction  →  void Execute(BaseContext)
public class LogAction       : BaseAction { public string Message; }
public class SetBoolAction   : BaseAction { public string ParameterKey; public bool   Value; }
public class SetIntAction    : BaseAction { public string ParameterKey; public int    Value; }
public class SetFloatAction  : BaseAction { public string ParameterKey; public float  Value; }
public class SetStringAction : BaseAction { public string ParameterKey; public string Value; }
```

Null-safety contract: a condition whose key is missing/mistyped returns `false` and logs a
`[GraphDialogue]` warning; it never throws.

## Execution

```csharp
public sealed class DialogueLineExecutor : INodeExecutor
{
    public string NodeType { get; }                          // DialogueLineNodeData.NodeTypeId
    public void Execute(BaseNodeData node, BaseContext context);
    // Undo: default no-op
}

public static class DialogueExecutorRegistryFactory
{
    public static NodeExecutorRegistry Create();             // line executor registered
}
```

## Playback facade

```csharp
public abstract class DialogueStep { public string NodeId { get; } }

public sealed class LineStep : DialogueStep
{
    public string SpeakerId           { get; }
    public string ResolvedSpeakerName { get; }
    public string ResolvedText        { get; }
    public string ExpressionKey       { get; }
}

public sealed class ChoiceOption
{
    public string ChoiceId      { get; }
    public string ResolvedLabel { get; }
    public bool   Available     { get; }
}

public sealed class ChoiceStep : DialogueStep
{
    public IReadOnlyList<ChoiceOption> Options { get; }
}

public sealed class EndStep : DialogueStep { public EndReason EndReason { get; } }

public sealed class DialoguePlayer
{
    public DialoguePlayer(
        DialogueGraph graph,
        DialogueContext context,
        ILocalizationProvider localization,
        Func<string, Speaker> speakerLookup);

    public RunnerState State { get; }
    public DialogueStep CurrentStep { get; }

    public event Action<LineStep>   OnLine;
    public event Action<ChoiceStep> OnChoices;
    public event Action<EndStep>    OnEnded;
    public event Action             OnStuck;

    public void Start();
    public void Advance();                  // Proceed (linear)
    public void Choose(string choiceId);    // ChooseById
    public void Back();                     // GoBack
    public void BackToCheckpoint();         // GoBackToCheckpoint
}
```

## Behavioral guarantees (asserted by tests)

- `Start()` begins at `graph.EntryNodeId` and emits the first step; missing entry → diagnostic, no crash.
- `LineStep.ResolvedText` / `ResolvedSpeakerName` reflect the active locale; switching locale changes
  them with no graph edits.
- `ChoiceStep.Options` lists every option with its localized label and `Available` from condition
  evaluation; `Choose` on an unavailable option does not advance.
- `OnEnded` fires exactly once with the `EndReason`; sub-dialogues resume the parent on end.
- Cyclic sub-dialogue → `GraphCycleException` before recursion (runtime) / refused at edit time.
- `Back()` restores prior context values (snapshot/restore via `BaseRunner` history).
- Round-trip save/reload of a `DialogueGraph` preserves ids, fields, option order, edges (graphcore
  determinism).
- Runtime core compiles/runs without `com.unity.localization`; both providers resolve the same graph.
