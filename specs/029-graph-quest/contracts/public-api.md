# Phase 1 Contract: Public API surface — com.faolline.graphquest v1

The library's "interface" is its public C# API (it is a Unity package, not a service). Signatures below are the
contract Phase 2 must fulfil; exact bodies are implementation. Namespace: `Faolline.GraphQuest`.

## States

```csharp
public enum QuestState { Locked, Active, Completed, Failed }

public enum QuestCompletionRule { AllRequired } // v1: only AllRequired; enum reserves room for more
```

## Model

```csharp
public sealed class ObjectiveNodeData : Faolline.GraphCore.BaseNodeData
{
    public const string NodeTypeId = "graphquest.objective";

    public BaseCondition CompletionCondition { get; set; } // holds ⇒ objective recorded completed
    public BaseCondition FailCondition       { get; set; } // optional; holds ⇒ recorded failed (checked first)
    public bool          Required            { get; set; } // default true
    public BaseAction    Reward              { get; set; } // optional; fired once on Completed
}

public sealed class QuestGraph : Faolline.GraphCore.BaseGraph
{
    public BaseCondition        UnlockCondition  { get; set; } // null ⇒ unlockable immediately
    public BaseAction           CompletionReward { get; set; } // optional; fired once on quest Completed
    public QuestCompletionRule  CompletionRule   { get; set; } // default AllRequired
}
```

## Typed context companions (Principle VI)

```csharp
public static class QuestContextKeys
{
    public const string Completed = "quest_completed";
    public const string Failed    = "quest_failed";
    public const string Rewarded  = "quest_rewarded";
}

public sealed class QuestContext : Faolline.GraphCore.BaseContext
{
    public bool IsCompleted(string id);
    public bool IsFailed(string id);
    protected override BaseContext CreateCloneInstance() => new QuestContext();
}
```

## Evaluator

```csharp
public sealed class QuestEvaluator
{
    public QuestEvaluator(QuestGraph quest, BaseContext context); // context may be a host's (e.g. GameFlowContext)

    public void Evaluate();                          // one derivation pass; fires rewards + change events
    public QuestState GetObjectiveState(string objectiveId);
    public QuestState State { get; }                 // aggregated quest state
    public IReadOnlyCollection<string> ActiveObjectiveIds { get; }
    public IReadOnlyCollection<string> CompletedObjectiveIds { get; }

    public event System.Action<string, QuestState> OnObjectiveStateChanged; // (objectiveId, newState)
    public event System.Action<QuestState>         OnQuestStateChanged;     // new aggregate
    public event System.Action<string>             OnRewardFired;           // id whose reward just fired
}
```

## Builder (fluent, code-first)

```csharp
public sealed class QuestBuilder
{
    public static QuestBuilder Create(string questId);
    public QuestBuilder UnlockWhen(BaseCondition condition);
    public ObjectiveBuilder AddObjective(string objectiveId);
    public QuestBuilder RewardQuestWith(BaseAction reward);
    public QuestGraph Build(); // validates acyclic + non-empty; [GraphQuest] diagnostic + throw on a cycle
}

public sealed class ObjectiveBuilder // returned by AddObjective; chains back to the QuestBuilder
{
    public ObjectiveBuilder CompleteWhen(BaseCondition condition);
    public ObjectiveBuilder FailWhen(BaseCondition condition);
    public ObjectiveBuilder Requires(params string[] prerequisiteObjectiveIds);
    public ObjectiveBuilder Optional();
    public ObjectiveBuilder RewardWith(BaseAction reward);
    public ObjectiveBuilder AddObjective(string objectiveId); // declare the next objective
    public QuestGraph Build();                                // finish
}
```

## Contract guarantees (mapped to FR / SC)

- **Determinism** (FR-003 / SC-002): `Evaluate()` derives state purely from the current context; repeated calls
  with an unchanged context produce identical states and no duplicate events; reverting the context reverts derived
  state.
- **Gating** (FR-005 / SC-003): an objective is never `Active` while any prerequisite is not `Completed`; holds for
  linear chains and DAGs.
- **Cycle rejection** (FR-006 / SC-007): `Build()` throws with a cycle-naming `[GraphQuest]` message on a cyclic
  prerequisite topology.
- **Reward once** (FR-008 / SC-004): each `Reward`/`CompletionReward` `Execute`s at most once across any number of
  `Evaluate()` calls and across a save/restore cycle (guarded by the rewarded-set).
- **No new condition language** (FR-004): completion/fail/unlock are graphcore `BaseCondition`; rewards are
  graphcore `BaseAction`.
- **Persistence** (FR-012): no public snapshot type; state round-trips because it lives in `BaseContext`
  collections that `graphsave` already serializes.
- **No host coupling** (FR-013 / SC-006): `QuestEvaluator` accepts any `BaseContext`; the package references
  neither gameflow nor graphsave at runtime.
