# Quickstart: com.faolline.graphquest (v1)

A code-first walkthrough of authoring a quest, deriving its progress from a context, firing a reward once, and
restoring it from a save. Headless — no editor, no MonoBehaviour.

## Install

Add the package (and its floors) to your project:

- `com.faolline.graphquest` → depends on `com.faolline.graphcore` 0.14.0 and `com.faolline.graphstandard` 0.10.1.

## 1. Author a quest (linear chain + a DAG join)

```csharp
using Faolline.GraphCore;
using Faolline.GraphStandard;      // standard conditions/actions for code-first authoring
using Faolline.GraphQuest;
using UnityEngine;

// Helpers: graphstandard ships ready-made BaseCondition/BaseAction ScriptableObjects.
BaseCondition Flag(string key)  { var c = ScriptableObject.CreateInstance<BoolCondition>(); /* set key=key,true */ return c; }
BaseCondition Has3(string col)  { var c = ScriptableObject.CreateInstance<CollectionCountAtLeastCondition>(); /* col, 3 */ return c; }
BaseAction    Give(string what) { var a = ScriptableObject.CreateInstance<AddToCollectionAction>(); /* inventory += what */ return a; }

QuestGraph rescue = QuestBuilder.Create("rescue")
    .AddObjective("find_clue").CompleteWhen(Flag("found_clue"))
    .AddObjective("pick_lock").Requires("find_clue").CompleteWhen(Flag("lock_open"))
    .AddObjective("gather_herbs").Optional().CompleteWhen(Has3("herbs")).RewardWith(Give("potion"))
    .AddObjective("escape")
        .Requires("pick_lock")                       // a chain: escape needs pick_lock needs find_clue
        .CompleteWhen(Flag("outside"))
    .RewardQuestWith(Give("freedom_badge"))
    .Build();   // throws a [GraphQuest] cycle diagnostic if prerequisites form a loop
```

For a DAG join, list several prerequisites: `.AddObjective("boss").Requires("pick_lock", "gather_herbs")` makes
`boss` Active only once **both** complete.

## 2. Evaluate against a context

```csharp
var ctx = new QuestContext();            // or a host's BaseContext / GameFlowContext
var quest = new QuestEvaluator(rescue, ctx);

quest.OnObjectiveStateChanged += (id, state) => Debug.Log($"[{id}] -> {state}");
quest.OnQuestStateChanged     += state       => Debug.Log($"Quest -> {state}");

quest.Evaluate();
// find_clue: Active, pick_lock: Locked, gather_herbs: Active, escape: Locked, Quest: Active

ctx.Set<bool>("found_clue", true);
quest.Evaluate();
// find_clue: Completed -> pick_lock becomes Active

ctx.Set<bool>("lock_open", true);
ctx.Set<bool>("outside", true);
quest.Evaluate();
// pick_lock: Completed -> escape Active -> escape Completed -> Quest Completed
// the quest reward (freedom_badge) fires exactly once here
```

The host calls `Evaluate()` after it mutates the context (same contract as graphstandard's
`ReactiveEvaluator.MarkCompleted`). Reverting the context (history/step-back) and re-evaluating re-locks downstream
objectives — "back" is a re-pass, not an undo.

## 3. Rewards fire once

```csharp
ctx.AddToCollection("herbs", "a"); ctx.AddToCollection("herbs", "b"); ctx.AddToCollection("herbs", "c");
quest.Evaluate();   // gather_herbs Completed -> potion granted (once)
quest.Evaluate();   // no change: potion is NOT granted again (rewarded-set guard)
```

## 4. Save & restore

Quest state lives in context collections, so the existing save layer handles it with no quest-specific code:

```csharp
// SAVE
var snap = Faolline.GraphSave.GraphRunSnapshot.Capture(ctx, graphId: "rescue", currentNodeId: null);
// ... persist snap however you like (JsonUtility / your IGraphSaveStore) ...

// LOAD into a fresh context + evaluator
var restored = new QuestContext();
snap.ApplyTo(restored);
var quest2 = new QuestEvaluator(rescue, restored);
quest2.Evaluate();   // every objective/quest state matches pre-save; already-granted rewards do NOT re-fire
```

## 5. Drive from a gameflow host (optional)

```csharp
// The host owns the shared context; quests evaluate against it. No gameflow dependency in graphquest.
var quest = new QuestEvaluator(rescue, hostDriver.Context);
hostDriver.Context.Set<bool>("found_clue", true);
quest.Evaluate();
```

## Notes / boundaries

- Authoring is code-first in v1; the visual quest editor is deferred.
- The library ships data + change events + reward *seams* only — the quest journal / tracker UI is yours to build.
- Completion/fail/unlock are graphcore `BaseCondition`; rewards are graphcore `BaseAction` — no new condition
  language. Use graphstandard's standard conditions/actions, or your own.
