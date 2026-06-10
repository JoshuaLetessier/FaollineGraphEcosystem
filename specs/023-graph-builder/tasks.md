---
description: "Task list for 023-graph-builder (code-first graph ergonomics: builder + persist util + driver time query)"
---

# Tasks: code-first graph ergonomics (slice 4)

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: REQUIRED (TDD — tests before code), all EditMode. Batchmode (no `-quit`; re-run after source
change; verify XML). Branch `023-graph-builder` (stacks on master). **graphcore UNTOUCHED (code + README).**
Two packages: graphstandard `0.3.0 → 0.4.0` (builder + new Editor asm), gameflow `0.3.0 → 0.4.0` (driver query).

## Phase 1: Setup

- [ ] T001 [P] Create `com.faolline.graphstandard/Editor/com.faolline.graphstandard.Editor.asmdef` (name `com.faolline.graphstandard.Editor`; references `com.faolline.graphcore.Runtime` + `com.faolline.graphstandard.Runtime`; `includePlatforms: [Editor]`; `autoReferenced: false`).
- [ ] T002 [P] Update `com.faolline.graphstandard/Tests/EditMode/com.faolline.graphstandard.Tests.EditMode.asmdef`: add reference `com.faolline.graphstandard.Editor` (so the persist-util test can see it).

## Phase 2: US1 — Build a graph in code with a fluent builder (Priority: P1) 🎯 MVP

**Goal**: a public fluent builder constructs any `BaseGraph` subclass over the universal types, no boilerplate.

**Independent test**: build start → statement(action, await/wait) → choice → end with edges + entry; assert
the exact structure and that the built graph runs under a `BaseRunner` like the hand-built equivalent.

- [ ] T003 [P] [US1] Write `GraphBuilderTests` in `com.faolline.graphstandard/Tests/EditMode/GraphBuilderTests.cs`: build a graph of each universal node type (Start/Statement/Choice/SubGraph/End) with titles, edges, an entry node (`AsEntry`/first-Start), an attached `OnEnter`/`OnExit` action and an `EntryCondition`, an `Await` name, a `Wait` duration, a `Checkpoint`, and choices on a Choice node; assert the built `GameFlowGraph` (or a test `BaseGraph` subclass) has exactly those nodes/edges/entry and per-node fields (INV-1/INV-2); a built start→statement→end graph runs to completion under a `BaseRunner` (INV-3); an `Edge` to an unknown node throws (INV-4). Confirm RED.
- [ ] T004 [US1] Create `com.faolline.graphstandard/Runtime/Builder/GraphNodeBuilder.cs`: the fluent node handle (`Node`, `Title`, `At`(x,y/Vector2), `OnEnter`/`OnExit`(params), `When`(params), `Await`, `Wait`, `Checkpoint`, `Choice`(title, cond), `AsEntry`, `To`(target, port)) — each returns the handle; holds a back-ref to its `GraphBuilder` for `To`. XML docs.
- [ ] T005 [US1] Create `com.faolline.graphstandard/Runtime/Builder/GraphBuilder.cs` (`GraphBuilder<TGraph> where TGraph : BaseGraph, new()`): `AddStart/AddStatement/AddChoice/AddSubGraph/AddEnd` (auto-Guid id, auto-column position, return a `GraphNodeBuilder`); `Edge(from,to,portName="out")` (Choice `from` resolves a `portName` matching a choice **title** to its id, else `[GraphStandard]` log + literal; throw on an unknown node); `Build()` (fresh `TGraph`, add nodes+edges, set `EntryNodeId` from `AsEntry` else first Start). XML docs. Confirm T003 GREEN.

## Phase 3: US2 — Persist a built graph with sub-asset actions (Priority: P2)

**Goal**: an editor utility saves a graph asset with its actions/conditions as sub-assets.

**Independent test**: build a graph with attached actions, `Save` to a temp path, reload, assert the actions
are sub-assets of the saved graph; clean up.

- [ ] T006 [P] [US2] Write `GraphAssetBuilderTests` in `com.faolline.graphstandard/Tests/EditMode/GraphAssetBuilderTests.cs`: build (via the US1 builder) a graph whose nodes carry `LoadSceneAction`-like actions (use a small in-test `BaseAction` subclass, or any graphcore action), `GraphAssetBuilder.Save(graph, tempPath)`, reload the asset, assert each attached action is a sub-asset (`AssetDatabase.LoadAllAssetsAtPath` contains them) and an action that was already a persisted asset is NOT double-added (INV-5); `[TearDown]` deletes the temp asset. Confirm RED.
- [ ] T007 [US2] Create `com.faolline.graphstandard/Editor/GraphAssetBuilder.cs` (static `Save(BaseGraph, string path) → BaseGraph`): `AssetDatabase.CreateAsset(graph, path)`; for each node's `OnEnterActions`/`OnExitActions`/`EntryConditions` and each choice's `Condition`, `AddObjectToAsset` only when `!AssetDatabase.Contains(obj)`; `SaveAssets`; return the graph. `[GraphStandard]` on misuse; XML docs. Confirm T006 GREEN.

## Phase 4: US3 — Read an in-progress timed wait from the driver (Priority: P2)

**Goal**: `IsWaitingForTime` / `WaitRemaining` / `WaitTotal` on the gameflow driver, symmetric with the signal
query, computed driver-side (no graphcore change).

**Independent test**: a `WaitDuration` node — the driver reports time-waiting with the total + remaining, which
counts down to 0 as `Tick` is fed, and false/0 before boot, after end, and off a timed node.

- [ ] T008 [P] [US3] Extend `com.faolline.graphgameflow/Tests/EditMode/GraphFlowDriverTests.cs`: a graph with a `WaitDuration=1.0` node; after `Boot` parks on it, `IsWaitingForTime` is true, `WaitTotal ≈ 1.0`, `WaitRemaining ≈ 1.0`; `Tick(0.4)` ⇒ `WaitRemaining ≈ 0.6`; `Tick(0.7)` ⇒ the node resolves and `IsWaitingForTime` is false, `WaitRemaining == 0`; before `Boot` and after `OnEnded`, `IsWaitingForTime` is false and `WaitRemaining`/`WaitTotal` are 0 (INV-6). Confirm RED.
- [ ] T009 [US3] In `com.faolline.graphgameflow/Runtime/Driver/GraphFlowDriver.cs` add (additive): read-only `IsWaitingForTime` (`_running && Runner.State == WaitingForTime`), `WaitRemaining` (`IsWaitingForTime ? Mathf.Max(0, _waitTotal - _waitElapsed) : 0`), `WaitTotal` (`IsWaitingForTime ? _waitTotal : 0`); in `HandleWaitingForTime(node, duration)` set `_waitTotal = duration`, `_waitElapsed = 0`; in `Tick(dt)` add `dt` to `_waitElapsed` while time-waiting. XML docs; append-only. Confirm T008 GREEN.

## Phase 5: US4 — Doc the cyclic shell (Priority: P3) + Polish

- [ ] T010 [US4] Document the cyclic no-End game-shell pattern in `com.faolline.graphgameflow/README.md` (a short note under the cross-scene / flow section) and in `com.faolline.graphstandard/README.md` (the builder section): a cyclic Linear graph with no End is supported — it never ends (`IsRunning` stays true, no `OnEnded`), history is bounded by `BaseGraph.HistoryDepth`, use a small depth for a forever-looping shell.
- [ ] T011 [P] Add the builder + persist-util sections to `com.faolline.graphstandard/README.md` and the time-wait query to `com.faolline.graphgameflow/README.md`; bump `com.faolline.graphstandard/package.json` `0.3.0 → 0.4.0` and `com.faolline.graphgameflow/package.json` `0.3.0 → 0.4.0`; update both `CHANGELOG.md`.
- [ ] T012 Run the ENTIRE suite via batchmode: EditMode (661 prior + the new builder/persist/time tests) green AND PlayMode (9) green (graphcore untouched, INV-7). Record totals.
- [ ] T013 [P] Verify `[GraphStandard]` / `[GraphGameFlow]` prefixes, XML docs on every new public member, and that all changes are append-only (no changed existing signatures; graphcore untouched).

## Dependencies

- **Setup (T001–T002)** → US2 (the Editor asmdef + the test ref).
- **US1 (T003 → T004 → T005)** the MVP, independent (graphstandard Runtime). T004 before T005 (the builder uses the handle).
- **US2 (T006 → T007)** depends on Setup + reuses the US1 builder in its test.
- **US3 (T008 → T009)** independent (gameflow driver).
- **US4 + Polish (T010–T013)** last.

## Parallel opportunities

- T001 ‖ T002 (different asmdefs). The three RED tests T003 ‖ T006 ‖ T008 are in different files/packages.
- Implementations T005 (graphstandard Runtime) ‖ T007 (graphstandard Editor) ‖ T009 (gameflow) touch different
  files. T011/T013 ‖ in Polish.

## Implementation strategy

- **MVP = US1** (the fluent builder) — the headline gap hit in both dogfooding rounds.
- **+ US2** (persist) and **+ US3** (time query) — independent companions.
- **+ US4 + Polish** — doc the shell, bump 0.4.0 ×2, full suite green.

## Notes

- Only graphstandard (Runtime builder + new Editor asm + tests) and gameflow (driver + test + docs) change,
  plus package bumps. graphcore is UNTOUCHED (code AND README).
- The builder constructs only graphcore's universal types (zero domain vocabulary) — it belongs in
  graphstandard, the buffer/helper lib.
