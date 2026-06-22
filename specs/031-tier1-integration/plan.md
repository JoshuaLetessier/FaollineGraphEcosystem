# Implementation Plan: Tier-1 Integration Improvements

**Branch**: `031-tier1-integration` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

## Summary

Three features that reduce integration boilerplate and improve observability:

1. **PlayDialogueAction + DialogueBus** — a `BaseAction` + static event bus in graphdialoguesystem
   that lets a gameflow node play a dialogue with zero custom code. The node's `AwaitSignalName`
   parks the runner; the action starts a `DialoguePlayer` via the bus; on end, the bus raises the
   signal and the flow resumes. Any UI subscribes to the bus once.

2. **Context Watch** — an EditorWindow in graphcore that shows the live `BaseContext` parameters
   and collections during Play Mode. Uses a new `GraphRunContextRegistry` (parallel to
   `GraphRunMonitor`) to access contexts, and new `OnAnyParameterChanged`/`OnAnyCollectionChanged`
   wildcard subscriptions on BaseContext for event-driven refresh.

3. **QuestEvaluator auto-evaluate** — opt-in push mode via `EnableAutoEvaluate()` that subscribes
   to the same wildcard context events, replacing frame-polling with change-driven evaluation.

## Technical Context

**Language/Version**: C# 9 / Unity 6000.3.6f1.

**Primary Dependencies**:
- graphcore 0.19.0 (BaseContext, BaseAction, BaseRunner, GraphRunMonitor, IGraphRunProbe)
- graphdialoguesystem 0.8.0 (DialoguePlayer, DialogueGraph, Speaker, LineStep, ChoiceStep, EndStep)
- graphquest 0.2.0 (QuestEvaluator, QuestGraph)
- graphstandard 0.12.1 (ReactiveEvaluator — also registers with GraphRunContextRegistry)

**Testing**: EditMode (NUnit) via Unity batchmode. No PlayMode required.

**Constraints**: graphcore references nothing downstream. PlayDialogueAction and DialogueBus
live in graphdialoguesystem. QuestEvaluator changes live in graphquest. Only graphcore gets
the BaseContext additions and the editor window.

## Constitution Check

- **I. Foundation Stability** — PASS. BaseContext gets append-only wildcard subscriptions (MINOR).
  IGraphRunProbe is NOT modified. New GraphRunContextRegistry is a parallel editor-only registry.
  BaseRunner's EditorWireProbe gets a one-line registration addition (editor-only, PATCH).

- **II. Universal Abstractions Only** — PASS. Wildcard change notifications on a blackboard
  context are universal. GraphRunContextRegistry is a universal probe→context map. The Context
  Watch window is domain-agnostic. PlayDialogueAction and DialogueBus live in graphdialoguesystem
  (not graphcore). QuestEvaluator changes live in graphquest.

- **III. Specification-First** — PASS. spec.md approved before this plan.

- **IV. Test-Driven Development** — PASS (enforced in tasks). EditMode tests written first for:
  OnAnyParameterChanged/CollectionChanged, PlayDialogueAction lifecycle, DialogueBus relay,
  QuestEvaluator auto-evaluate enable/disable/coalescing.

- **V. Simplicity (YAGNI)** — PASS. Each addition is minimal: wildcard subscriptions are a
  multicast delegate. DialogueBus is a static relay (no framework). Auto-evaluate is a flag +
  two subscriptions. Context Watch is a simple IMGUI window.

- **VI. Typed Context Contract** — N/A for graphcore additions (wildcard subs are generic).
  PlayDialogueAction uses BaseContext (generic). QuestEvaluator already has QuestContext.

- **VII. Cross-lib Compatibility via SubGraph Only** — PASS. PlayDialogueAction references
  DialogueGraph (a BaseGraph). No cross-lib package dependencies added. graphcore still
  references nothing downstream.

**Result: PASS — no violations.**

## Project Structure

### Source Code

```text
com.faolline.graphcore/
├── Runtime/
│   └── Context/
│       └── BaseContext.cs                    # MODIFIED — add OnAny*/OffAny* wildcard subs
├── Editor/
│   ├── Window/
│   │   └── ContextWatchWindow.cs            # NEW — live parameter/collection inspector
│   └── Registry/
│       └── GraphRunContextRegistry.cs       # NEW — probe→context map (editor-only)
└── Tests/EditMode/
    └── WildcardContextSubscriptionTests.cs  # NEW

com.faolline.graphdialoguesystem/
├── Runtime/
│   ├── Playback/
│   │   └── DialogueBus.cs                   # NEW — static event relay
│   └── Actions/
│       └── PlayDialogueAction.cs            # NEW — BaseAction that drives DialogueBus
└── Tests/EditMode/
    └── Runtime/
        ├── DialogueBusTests.cs              # NEW
        └── PlayDialogueActionTests.cs       # NEW

com.faolline.graphquest/
├── Runtime/
│   └── QuestEvaluator.cs                   # MODIFIED — add Enable/DisableAutoEvaluate
└── Tests/EditMode/
    └── QuestEvaluatorAutoEvaluateTests.cs   # NEW
```

### Documentation

```text
specs/031-tier1-integration/
├── spec.md
├── plan.md          # This file
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── api.md
└── tasks.md         # Phase 2 (via /speckit-tasks)
```

## Implementation Order

### Slice 1: BaseContext wildcard subscriptions (graphcore)
Foundation for slices 2 and 3. Adds `OnAnyParameterChanged`, `OffAnyParameterChanged`,
`OnAnyCollectionChanged`, `OffAnyCollectionChanged` to BaseContext. Fires after per-key
handlers. Tests first.

### Slice 2: GraphRunContextRegistry + Context Watch (graphcore)
Editor-only. New registry + EditorWindow. BaseRunner.EditorWireProbe registers context.
ReactiveEvaluator and FlowRunner do the same (if they have probes).

### Slice 3: DialogueBus + PlayDialogueAction (graphdialoguesystem)
New static bus + action. Tests: lifecycle (start → line → advance → end → signal raised),
null graph handling, force-stop.

### Slice 4: QuestEvaluator auto-evaluate (graphquest)
Enable/Disable + re-entrancy guard. Tests: enable fires on change, disable stops,
coalescing during evaluate, timer not auto-ticked.

### Slice 5: Version bumps + integration test
Bump package.json versions. Cross-package sanity: a gameflow node with PlayDialogueAction +
a quest with auto-evaluate sharing one context. Full suite green.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
