# Quickstart: verifying the soft graph-link feature

## 1. GraphLink no longer pulls its target (Lot 1 — the primary acceptance gate)

1. In a scratch/test project (or an isolated test scene folder), create two graph assets, `RootGraph` and `AnnotatedGraph`.
2. Add a `GraphLinkNodeData` node to `RootGraph`, target = `AnnotatedGraph`.
3. Before building: `AssetDatabase.GetDependencies("RootGraph.asset", recursive: true)` — confirm it **does** include `AnnotatedGraph.asset` (baseline, pre-existing behavior, sanity check the test setup is real).
4. Apply this feature's `GraphLinkNodeData` change.
5. Re-run step 3 — confirm `AnnotatedGraph.asset` is **no longer** listed.
6. **Do the real check**: put `RootGraph` in an Addressables group (or a build's included-scenes/assets), run **Addressables ▸ Analyze** (or a Player build + build report), and confirm `AnnotatedGraph`'s content does not appear pulled into `RootGraph`'s bundle/group. Code inspection alone does not satisfy spec SC-001 — this is the step that catches a silent regression.
7. Confirm the inspector's `ObjectField` for the GraphLink node still accepts drag-and-drop of `AnnotatedGraph` and that double-clicking the node still opens it (spec SC-002 — zero ergonomics regression).

## 2. Validator catches a broken/unresolved link (Lot 1)

1. Assign a GraphLink target, then delete the target asset (or manually corrupt the GUID field in the `.asset` YAML to a nonexistent GUID).
2. Run `GraphValidator.Validate(graph)` — confirm a `Warning` issue is reported for that node.
3. Confirm a link that was **never assigned** (empty GUID) produces no such warning (only a broken *assigned* reference is a mistake).

## 3. Multi-root-graph save/restore via IGraphCatalog (Lot 2)

1. In an EditMode test with zero Addressables package assumptions, create a `DirectGraphCatalog`, register two `BaseGraph` assets under distinct `GraphId`s.
2. Capture a `GraphRunSnapshot` from a run on one of them.
3. Using only the snapshot's `GraphId` and the catalog's `Resolve`, obtain the `BaseGraph` and call `Restore` — confirm the run resumes on the correct graph, with no hand-written lookup table in the test.
4. Confirm resolving an unregistered `GraphId` invokes `onFailed`, never a null-but-"succeeded" result.

## 4. Graph Key Registry tool (Lot 3)

1. Open `Faolline ▸ Graph ▸ Graph Key Registry`.
2. Confirm it lists project `BaseGraph` assets with their `GraphId`.
3. With no provider registered, confirm the window shows no promotion options (empty by default, matching `SceneKeySourceRegistry`'s "opt-in, empty by default" contract).

## 5. SubGraph-crosses-chapter-root validator rule (Lot 1+3, via the new extension seam)

1. In a `graphcore`-only EditMode test, register a fake `IGraphValidatorExtension` directly against `GraphValidatorExtensionRegistry` (no `graphgameflow` involved) whose `CheckSubGraphTarget` returns a fixed warning for one specific graph — confirms the generic seam itself works with zero knowledge of "chapters".
2. Separately, in a `graphgameflow` EditMode test, register a fake `IGraphKeySourceProvider` whose `TryResolveGuid` returns true for a specific graph's GUID, then confirm `ChapterRootSubGraphValidatorExtension.CheckSubGraphTarget` returns a non-empty message for that graph and null for an unregistered one.
3. End-to-end: build a graph with a `SubGraphNodeData` whose target is registered as a chapter root (via the real `ChapterRootSubGraphValidatorExtension`, self-registered), run `GraphValidator.Validate` — confirm a `Warning` is reported identifying the crossed boundary.
4. Confirm a `SubGraphNodeData` targeting a **non**-registered graph produces no such warning (normal intra-chapter composition stays silent).

## 6. Addressables preload (Lot 4 — only if Addressables is available)

1. Mark a chapter's root graph Addressable (via the Graph Key Registry's "Mark as Addressable" or manually).
2. Wire a `PreloadNextChapterAction` early in the current chapter, `_nextChapter` pointing at the marked graph.
3. Run the chapter to its end; confirm the preloaded `BaseGraph` is available with zero additional wait (no visible stall) when the driver reboots onto it.
4. Run Addressables ▸ Analyze on the *current* chapter's group; confirm the next chapter's content is absent (spec SC-007).
