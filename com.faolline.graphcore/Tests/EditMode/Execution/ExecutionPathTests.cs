using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Faolline.GraphCore.Tests.ExecutionPathFixture;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="BaseRunner.GetExecutionPath"/> reads every level of a live run (root first), and
    /// <see cref="BaseRunner.StartFromPath"/> rebuilds such a path from the root graph: only the deepest node is
    /// re-entered, and the run then behaves exactly like one built by uninterrupted play.
    /// </summary>
    public class ExecutionPathTests
    {
        private ExecutionPathFixture _f;

        [SetUp]
        public void SetUp() => _f = new ExecutionPathFixture();

        [TearDown]
        public void TearDown() => _f.Dispose();

        private static List<GraphRunFrame> Path(params GraphRunFrame[] frames) => new List<GraphRunFrame>(frames);

        private static void AssertFrame(GraphRunFrame frame, BaseGraph graph, string nodeId, GraphRunContextMode mode)
        {
            Assert.AreEqual(graph.GraphId, frame.GraphId, "GraphId");
            Assert.AreEqual(nodeId, frame.NodeId, "NodeId");
            Assert.AreEqual(mode, frame.ContextMode, "ContextMode");
        }

        private static void AutoProceed(BaseRunner runner)
            => runner.OnNodeCompleted += _ => { if (runner.State == RunnerState.NodeReady) runner.Proceed(); };

        /// <summary>C: c-start → c-sub(→Z) → c-after → c-end ; Z: z-start → z-sub(→R) → z-after → z-end ; R: r-start → r-mid → r-end.</summary>
        private (BaseGraph c, BaseGraph z, BaseGraph r) ThreeLevels(GraphRunContextMode zMode = GraphRunContextMode.Shared,
                                                                    GraphRunContextMode rMode = GraphRunContextMode.Shared)
        {
            var r = _f.Chain(Start("r-start"), Step("r-mid"), End("r-end"));
            var z = _f.Chain(Start("z-start"), Sub("z-sub", r, rMode), Step("z-after"), End("z-end"));
            var c = _f.Chain(Start("c-start"), Sub("c-sub", z, zMode), Step("c-after"), End("c-end"));
            return (c, z, r);
        }

        // ── GetExecutionPath ─────────────────────────────────────────────────────

        [Test]
        public void GetExecutionPath_BeforeStart_IsEmpty()
        {
            Assert.AreEqual(0, new BaseRunner().GetExecutionPath().Count);
        }

        [Test]
        public void GetExecutionPath_AtRootLevel_HasOneFrame()
        {
            var (c, _, _) = ThreeLevels();
            var runner = new BaseRunner();
            runner.Start(c, new BaseContext(), new NodeExecutorRegistry());

            var path = runner.GetExecutionPath();

            Assert.AreEqual(1, path.Count);
            AssertFrame(path[0], c, "c-start", GraphRunContextMode.Shared);
        }

        [Test]
        public void GetExecutionPath_ThreeLevelsDeep_ListsRootFirst_EnclosingLevelsOnTheirSubGraphNode()
        {
            var (c, z, r) = ThreeLevels();
            var runner = new BaseRunner();
            runner.Start(c, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();   // c-sub
            runner.Proceed();   // → z-start
            runner.Proceed();   // z-sub
            runner.Proceed();   // → r-start
            runner.Proceed();   // r-mid

            var path = runner.GetExecutionPath();

            Assert.AreEqual(3, path.Count);
            AssertFrame(path[0], c, "c-sub", GraphRunContextMode.Shared);
            AssertFrame(path[1], z, "z-sub", GraphRunContextMode.Shared);
            AssertFrame(path[2], r, "r-mid", GraphRunContextMode.Shared);
        }

        [Test]
        public void GetExecutionPath_ReadsEachLevelsContextMode_FromTheLiveRun()
        {
            var (c, z, r) = ThreeLevels(GraphRunContextMode.Scoped, GraphRunContextMode.Fresh);
            var runner = new BaseRunner();
            runner.Start(c, new BaseContext(), new NodeExecutorRegistry());
            for (int i = 0; i < 4; i++) runner.Proceed();   // c-sub, z-start, z-sub, r-start

            var path = runner.GetExecutionPath();

            AssertFrame(path[0], c, "c-sub", GraphRunContextMode.Shared);
            AssertFrame(path[1], z, "z-sub", GraphRunContextMode.Scoped);
            AssertFrame(path[2], r, "r-start", GraphRunContextMode.Fresh);
        }

        [Test]
        public void GetExecutionPath_SharedLevelUnderAFreshLevel_IsShared()
        {
            var (c, z, r) = ThreeLevels(GraphRunContextMode.Fresh, GraphRunContextMode.Shared);
            var runner = new BaseRunner();
            runner.Start(c, new BaseContext(), new NodeExecutorRegistry());
            for (int i = 0; i < 4; i++) runner.Proceed();

            var path = runner.GetExecutionPath();

            Assert.AreEqual(GraphRunContextMode.Fresh, path[1].ContextMode);
            Assert.AreEqual(GraphRunContextMode.Shared, path[2].ContextMode, "R shares Z's (fresh) context");
        }

        [Test]
        public void GetExecutionPath_OnASubGraphNodeBeforeProceed_EndsOnThatNode()
        {
            var (c, _, _) = ThreeLevels();
            var runner = new BaseRunner();
            runner.Start(c, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();   // c-sub, not yet entered

            var path = runner.GetExecutionPath();

            Assert.AreEqual(1, path.Count);
            AssertFrame(path[0], c, "c-sub", GraphRunContextMode.Shared);
        }

        [Test]
        public void GetExecutionPath_IsACopy_NotALiveView()
        {
            var (c, _, _) = ThreeLevels();
            var runner = new BaseRunner();
            runner.Start(c, new BaseContext(), new NodeExecutorRegistry());

            var before = runner.GetExecutionPath();
            runner.Proceed();

            Assert.AreEqual("c-start", before[0].NodeId);
        }

        // ── StartFromPath: only the deepest node is re-entered ──────────────────

        [Test]
        public void StartFromPath_TwoLevels_ReentersOnlyTheDeepestNode()
        {
            var z = _f.Chain(Start("z-start"), Step("z-mid"), End("z-end"));
            var sub = Sub("c-sub", z);
            var c = _f.Chain(Start("c-start"), sub, End("c-end"));
            var mid = (StatementNodeData)z.Nodes[1];

            var subEnter = _f.Counter();
            var midEnter = _f.Counter();
            sub.OnEnterActions.Add(subEnter);
            mid.OnEnterActions.Add(midEnter);

            int subExecutor = 0, statementExecutor = 0;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaNodeExecutor(SubGraphNodeData.NodeTypeId, (n, _) => subExecutor++));
            registry.Register(new LambdaNodeExecutor(StatementNodeData.NodeTypeId, (n, _) => statementExecutor++));

            var entered = new List<string>();
            var runner = new BaseRunner();
            runner.OnNodeEntered += n => entered.Add(n.Id);

            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-mid")), new BaseContext(), registry);

            Assert.AreEqual(RunnerState.NodeReady, runner.State);
            Assert.AreEqual("z-mid", runner.CurrentNode.Id);
            Assert.AreSame(z, runner.CurrentGraph);
            CollectionAssert.AreEqual(new[] { "z-mid" }, entered, "only the deepest node raises OnNodeEntered");
            Assert.AreEqual(1, midEnter.Count, "the deepest node's enter actions run, as with StartFrom");
            Assert.AreEqual(1, statementExecutor);
            Assert.AreEqual(0, subEnter.Count, "the enclosing SubGraph node's enter actions are NOT replayed");
            Assert.AreEqual(0, subExecutor, "the enclosing SubGraph node's executor is NOT replayed");
        }

        [Test]
        public void StartFromPath_TwoLevels_ChildEndReturnsIntoTheParent()
        {
            var z = _f.Chain(Start("z-start"), Step("z-mid"), End("z-end"));
            var c = _f.Chain(Start("c-start"), Sub("c-sub", z), Step("c-after"), End("c-end"));

            var entered = new List<string>();
            var ended = new List<EndReason>();
            var runner = new BaseRunner();
            runner.OnNodeEntered += n => entered.Add(n.Id);
            runner.OnEnded += reason => ended.Add(reason);
            AutoProceed(runner);

            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-mid")), new BaseContext(), new NodeExecutorRegistry());

            CollectionAssert.AreEqual(new[] { "z-mid", "z-end", "c-after", "c-end" }, entered);
            Assert.AreEqual(RunnerState.Ended, runner.State);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, ended, "the run ends once, at the root's end");
        }

        [Test]
        public void StartFromPath_ThreeLevels_VisitsTheSameNodesAsAnUninterruptedRun()
        {
            var (c, z, r) = ThreeLevels();

            var uninterrupted = new List<string>();
            var live = new BaseRunner();
            live.OnNodeEntered += n => uninterrupted.Add(n.Id);
            AutoProceed(live);
            live.Start(c, new BaseContext(), new NodeExecutorRegistry());
            var expected = uninterrupted.GetRange(uninterrupted.IndexOf("r-mid"), uninterrupted.Count - uninterrupted.IndexOf("r-mid"));

            var restored = new List<string>();
            int endedCount = 0;
            var runner = new BaseRunner();
            runner.OnNodeEntered += n => restored.Add(n.Id);
            runner.OnEnded += _ => endedCount++;
            AutoProceed(runner);
            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-sub"), Frame(r, "r-mid")),
                                 new BaseContext(), new NodeExecutorRegistry());

            CollectionAssert.AreEqual(new[] { "r-mid", "r-end", "z-after", "z-end", "c-after", "c-end" }, restored);
            CollectionAssert.AreEqual(expected, restored);
            Assert.AreEqual(1, endedCount);
        }

        [Test]
        public void StartFromPath_DeepestLevelOnASubGraphNode_ProceedEntersTheChild()
        {
            var (c, z, _) = ThreeLevels();
            var runner = new BaseRunner();

            runner.StartFromPath(c, Path(Frame(c, "c-sub")), new BaseContext(), new NodeExecutorRegistry());
            Assert.AreEqual("c-sub", runner.CurrentNode.Id);

            runner.Proceed();

            Assert.AreEqual("z-start", runner.CurrentNode.Id);
            Assert.AreSame(z, runner.CurrentGraph);
        }

        [Test]
        public void StartFromPath_RoundTripsAPathReadFromALiveRun()
        {
            var (c, _, _) = ThreeLevels();
            var live = new BaseRunner();
            live.Start(c, new BaseContext(), new NodeExecutorRegistry());
            for (int i = 0; i < 5; i++) live.Proceed();   // … r-mid
            var path = live.GetExecutionPath();

            var runner = new BaseRunner();
            runner.StartFromPath(c, path, new BaseContext(), new NodeExecutorRegistry());
            var again = runner.GetExecutionPath();

            Assert.AreEqual(path.Count, again.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Assert.AreEqual(path[i].GraphId, again[i].GraphId);
                Assert.AreEqual(path[i].NodeId, again[i].NodeId);
                Assert.AreEqual(path[i].ContextMode, again[i].ContextMode);
            }
        }

        [Test]
        public void StartFromPath_SharedLevel_WritesIntoTheRootContext()
        {
            var z = _f.Chain(Start("z-start"), Step("z-mid"), End("z-end"));
            var c = _f.Chain(Start("c-start"), Sub("c-sub", z), End("c-end"));
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaNodeExecutor(StatementNodeData.NodeTypeId, (n, ctx) => ctx.Set<int>("gold", 5)));
            var root = new BaseContext();

            new BaseRunner().StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-mid")), root, registry);

            Assert.AreEqual(5, root.Get<int>("gold"));
        }

        // ── StartFromPath: scoped and fresh levels ───────────────────────────────

        [Test]
        public void StartFromPath_ScopedLevel_ReopensTheLocalLayer_SeededWithDeclaredDefaults()
        {
            var tmp = _f.IntVariable("Tmp", 3);
            var mid = Step("z-mid");
            var z = _f.Chain(Start("z-start"), mid, End("z-end"));
            _f.Reference(mid, tmp);
            var c = _f.Chain(Start("c-start"), Sub("c-sub", z, GraphRunContextMode.Scoped), Step("c-after"), End("c-end"));

            var root = new BaseContext();
            root.Set<int>("gold", 7);   // a durable global value, already applied by the caller
            bool layerOpen = false;
            int tmpInside = -1;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaNodeExecutor(StatementNodeData.NodeTypeId, (n, ctx) =>
            {
                if (n.Id != "z-mid") return;
                layerOpen = ctx.HasLocalContext;
                tmpInside = ctx.Get<int>(tmp);
                ctx.Set<int>("scratch", 1);   // undeclared → lands in the local layer
            }));

            var runner = new BaseRunner();
            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-mid", GraphRunContextMode.Scoped)), root, registry);

            Assert.IsTrue(layerOpen, "the scoped level's local layer is open again");
            Assert.AreEqual(3, tmpInside, "the layer is seeded with the sub-graph's declared defaults");
            Assert.IsTrue(root.Has("scratch"));

            runner.Proceed();   // z-end
            runner.Proceed();   // pop → c-after

            Assert.AreEqual("c-after", runner.CurrentNode.Id);
            Assert.IsFalse(root.HasLocalContext, "the layer closes when the sub-graph ends, as in a live run");
            Assert.IsFalse(root.Has("scratch"), "local writes vanish with the layer");
            Assert.AreEqual(7, root.Get<int>("gold"), "global values are untouched");
        }

        [Test]
        public void StartFromPath_FreshLevel_RunsOnItsOwnContext_SeededWithDeclaredDefaults()
        {
            var hp = _f.IntVariable("Hp", 10);
            var mid = Step("z-mid");
            var z = _f.Chain(Start("z-start"), mid, End("z-end"));
            _f.Reference(mid, hp);
            var c = _f.Chain(Start("c-start"), Sub("c-sub", z, GraphRunContextMode.Fresh), Step("c-after"), End("c-end"));

            var root = new BaseContext();
            root.Set<int>("gold", 7);
            BaseContext zContext = null, afterContext = null;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaNodeExecutor(StatementNodeData.NodeTypeId, (n, ctx) =>
            {
                if (n.Id == "z-mid") { zContext = ctx; ctx.Set<int>("z-only", 1); }
                if (n.Id == "c-after") afterContext = ctx;
            }));

            var runner = new BaseRunner();
            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-mid", GraphRunContextMode.Fresh)), root, registry);

            Assert.IsNotNull(zContext);
            Assert.AreNotSame(root, zContext, "a fresh level runs on its own context");
            Assert.AreEqual(10, zContext.Get<int>(hp), "seeded with the sub-graph's declared defaults");
            Assert.IsFalse(zContext.Has("gold"), "it does not see the root's values");
            Assert.IsFalse(root.Has("z-only"), "and does not write into them");

            runner.Proceed();   // z-end
            runner.Proceed();   // pop → c-after

            Assert.AreSame(root, afterContext, "the root context is back once the fresh level ends");
        }

        [Test]
        public void StartFromPath_MixedModes_RoundTripAPathReadFromALiveRun()
        {
            var (c, _, _) = ThreeLevels(GraphRunContextMode.Scoped, GraphRunContextMode.Fresh);
            var live = new BaseRunner();
            live.Start(c, new BaseContext(), new NodeExecutorRegistry());
            for (int i = 0; i < 5; i++) live.Proceed();   // … r-mid
            var path = live.GetExecutionPath();

            var runner = new BaseRunner();
            runner.StartFromPath(c, path, new BaseContext(), new NodeExecutorRegistry());
            var again = runner.GetExecutionPath();

            Assert.AreEqual(GraphRunContextMode.Scoped, again[1].ContextMode);
            Assert.AreEqual(GraphRunContextMode.Fresh, again[2].ContextMode);
            Assert.AreEqual("r-mid", again[2].NodeId);
        }

        [Test]
        public void StartFromPath_TwoNestedScopedLevels_ReproduceTheLiveRunsSingleLayer()
        {
            // BaseContext holds ONE flat local layer: entering the inner scope replaces the outer one. A restore
            // must reproduce exactly that, not more.
            var (c, _, _) = ThreeLevels(GraphRunContextMode.Scoped, GraphRunContextMode.Scoped);

            var liveContext = new BaseContext();
            var live = new BaseRunner();
            live.Start(c, liveContext, new NodeExecutorRegistry());
            for (int i = 0; i < 4; i++) live.Proceed();   // … r-start
            var path = live.GetExecutionPath();

            var context = new BaseContext();
            var runner = new BaseRunner();
            runner.StartFromPath(c, path, context, new NodeExecutorRegistry());

            Assert.AreEqual(liveContext.HasLocalContext, context.HasLocalContext);
            while (live.State == RunnerState.NodeReady) live.Proceed();
            while (runner.State == RunnerState.NodeReady) runner.Proceed();
            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.AreEqual(liveContext.HasLocalContext, context.HasLocalContext);
        }

        // ── StartFromPath: history ───────────────────────────────────────────────

        [Test]
        public void StartFromPath_HistoryStartsEmpty()
        {
            var (c, z, _) = ThreeLevels();
            var runner = new BaseRunner();
            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-start")), new BaseContext(), new NodeExecutorRegistry());

            runner.GoBack();

            Assert.AreEqual("z-start", runner.CurrentNode.Id, "nothing to rewind before the load point");
            Assert.AreSame(z, runner.CurrentGraph);
        }

        [Test]
        public void StartFromPath_HistoryDepth_IsTheRootGraphs()
        {
            var z = _f.Chain(Start("z-start"), Step("z-a"), Step("z-b"), Step("z-c"), End("z-end"));
            var c = _f.Chain(Start("c-start"), Sub("c-sub", z), End("c-end"));
            c.HistoryDepth = 1;
            z.HistoryDepth = 50;
            var runner = new BaseRunner();
            runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "z-a")), new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();   // z-b
            runner.Proceed();   // z-c

            runner.GoBack();
            runner.GoBack();    // capped by the ROOT's depth (1): no second step back

            Assert.AreEqual("z-b", runner.CurrentNode.Id);
            Assert.AreSame(z, runner.CurrentGraph, "a rewind keeps the restored stack");
        }

        // ── StartFromPath: refusal ───────────────────────────────────────────────

        [Test]
        public void StartFromPath_PathNotMatchingTheContent_ThrowsBeforeTouchingTheRunner()
        {
            var (c, z, _) = ThreeLevels();
            var context = new BaseContext();
            var runner = new BaseRunner();
            runner.Start(c, context, new NodeExecutorRegistry());

            var ex = Assert.Throws<GraphRunPathException>(() =>
                runner.StartFromPath(c, Path(Frame(c, "c-sub"), Frame(z, "gone")), new BaseContext(), new NodeExecutorRegistry()));

            Assert.AreEqual(GraphRunPathErrorReason.NodeMissing, ex.Error.Reason);
            Assert.AreEqual(1, ex.Error.FrameIndex);
            Assert.AreEqual("c-start", runner.CurrentNode.Id, "the run in progress is untouched");
            Assert.AreSame(c, runner.CurrentGraph);
            Assert.AreEqual(RunnerState.NodeReady, runner.State);
            Assert.AreEqual(1, runner.GetExecutionPath().Count);
        }

        [Test]
        public void StartFromPath_NullArguments_Throw()
        {
            var (c, _, _) = ThreeLevels();
            var path = Path(Frame(c, "c-start"));
            var runner = new BaseRunner();

            Assert.Throws<ArgumentNullException>(() => runner.StartFromPath(null, path, new BaseContext(), null));
            Assert.Throws<ArgumentNullException>(() => runner.StartFromPath(c, null, new BaseContext(), null));
            Assert.Throws<ArgumentNullException>(() => runner.StartFromPath(c, path, null, null));
        }
    }
}
