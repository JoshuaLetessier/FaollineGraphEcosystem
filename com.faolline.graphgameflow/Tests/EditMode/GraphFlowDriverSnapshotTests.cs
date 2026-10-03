using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphSave;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// <see cref="GraphFlowDriver.Boot(GraphRunSnapshot, GameFlowContext, NodeExecutorRegistry)"/> with a snapshot
    /// taken inside sub-graphs: the driver's <see cref="GraphFlowDriver.Graph"/> is the ROOT, the whole stack is
    /// rebuilt, and a snapshot that does not match that root leaves the driver inert.
    /// </summary>
    public class GraphFlowDriverSnapshotTests
    {
        private readonly List<Object> _so = new List<Object>();
        private readonly List<GameObject> _go = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _go) if (g) Object.DestroyImmediate(g);
            _go.Clear();
            foreach (var o in _so) if (o) Object.DestroyImmediate(o);
            _so.Clear();
        }

        private BaseGraph Chain(params BaseNodeData[] nodes)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            _so.Add(g);
            for (int i = 0; i < nodes.Length; i++)
            {
                g.AddNode(nodes[i]);
                if (i > 0) g.AddEdge(new BaseEdgeData { Id = $"e-{nodes[i - 1].Id}-{nodes[i].Id}", FromNodeId = nodes[i - 1].Id, ToNodeId = nodes[i].Id });
            }
            g.EntryNodeId = nodes[0].Id;
            return g;
        }

        private static StartNodeData Start(string id) => new StartNodeData { Id = id, NodeType = StartNodeData.NodeTypeId };
        private static StatementNodeData Step(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        private static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
        private static SubGraphNodeData Sub(string id, BaseGraph target)
            => new SubGraphNodeData { Id = id, NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target, InheritParentContext = true };

        /// <summary>Chapter C: c-start → c-sub(→Z) → c-after → c-end ; zone Z: z-start → z-sub(→R) → z-mid → z-end ; R: r-start → r-mid → r-end.</summary>
        private (BaseGraph c, BaseGraph z, BaseGraph r) ThreeLevels()
        {
            var r = Chain(Start("r-start"), Step("r-mid"), End("r-end"));
            var z = Chain(Start("z-start"), Sub("z-sub", r), Step("z-mid"), End("z-end"));
            var c = Chain(Start("c-start"), Sub("c-sub", z), Step("c-after"), End("c-end"));
            return (c, z, r);
        }

        private GraphFlowDriver NewDriver(BaseGraph graph, bool autoAdvance)
        {
            var go = new GameObject("driver");
            _go.Add(go);
            var d = go.AddComponent<GraphFlowDriver>();
            d.Graph = graph;
            d.AutoAdvance = autoAdvance;
            d.SceneLoader = new StubSceneLoader();
            return d;
        }

        /// <summary>Plays <paramref name="root"/> manually up to <paramref name="nodeId"/> and returns the snapshot as saved JSON.</summary>
        private string SaveAt(BaseGraph root, string nodeId, System.Action<GameFlowContext> seed = null)
        {
            var d = NewDriver(root, autoAdvance: false);
            var ctx = new GameFlowContext();
            seed?.Invoke(ctx);
            d.Boot(ctx, null);
            for (int guard = 0; d.Runner.CurrentNode.Id != nodeId; guard++)
            {
                Assert.Less(guard, 50, $"never reached '{nodeId}'");
                d.Advance();
            }
            var json = JsonUtility.ToJson(GraphRunSnapshot.Capture(d.Runner, d.Context));
            d.Stop();
            return json;
        }

        private static GraphRunSnapshot Load(string json) => JsonUtility.FromJson<GraphRunSnapshot>(json);

        // ── US3 ──────────────────────────────────────────────────────────────────

        [Test]
        public void Boot_StackedSnapshot_ResumesInsideTheSubGraph_WithTheSavedValues()
        {
            var (c, z, _) = ThreeLevels();
            var json = SaveAt(c, "z-mid", ctx => ctx.Set<int>("gold", 7));

            var d = NewDriver(c, autoAdvance: false);
            d.Boot(Load(json));

            Assert.IsTrue(d.IsRunning);
            Assert.AreSame(z, d.Runner.CurrentGraph);
            Assert.AreEqual("z-mid", d.Runner.CurrentNode.Id);
            Assert.AreEqual(7, d.Context.Get<int>("gold"));
        }

        [Test]
        public void Boot_StackedSnapshot_SubGraphEndsBackIntoTheRoot_WithTheDriversEvents()
        {
            var (c, _, _) = ThreeLevels();
            var json = SaveAt(c, "r-mid");

            var d = NewDriver(c, autoAdvance: true);
            var entered = new List<string>();
            var ended = new List<EndReason>();
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.OnEnded += reason => ended.Add(reason);
            d.Boot(Load(json));

            CollectionAssert.AreEqual(new[] { "r-mid", "r-end", "z-mid", "z-end", "c-after", "c-end" }, entered);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, ended, "the flow ends once, at the root's end");
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void Boot_StackedSnapshot_OnAGraphThatIsNotItsRoot_StaysInert_AndLeavesTheContextUntouched()
        {
            var (c, z, _) = ThreeLevels();
            var json = SaveAt(c, "z-mid", ctx => ctx.Set<int>("gold", 7));

            var d = NewDriver(z, autoAdvance: false);   // the zone, not the chapter
            var ctx2 = new GameFlowContext();
            ctx2.Set<int>("gold", 1);
            LogAssert.Expect(LogType.Error, new Regex(@"^\[GraphGameFlow\] GraphFlowDriver\.Boot: the snapshot does not match graph .*Staying inert\.$"));

            d.Boot(Load(json), ctx2);

            Assert.IsFalse(d.IsRunning);
            Assert.IsNull(d.Runner, "no runner is created for a refused snapshot");
            Assert.AreEqual(1, ctx2.Get<int>("gold"), "the supplied context is not modified");
        }

        [Test]
        public void Boot_StackedSnapshot_RequestedFromOnEnded_IsDeferred_ThenRestoresTheStack()
        {
            var (c, _, r) = ThreeLevels();
            var snapshot = Load(SaveAt(c, "r-mid"));
            var intro = Chain(Start("i-start"), End("i-end"));

            var d = NewDriver(intro, autoAdvance: false);
            d.OnEnded += _ => { d.Graph = c; d.Boot(snapshot); };   // reentrant: queued until the dispatch unwinds
            d.Boot();
            d.Advance();   // i-end
            d.Advance();   // ends → OnEnded → deferred Boot(snapshot)

            Assert.IsTrue(d.IsRunning);
            Assert.AreSame(r, d.Runner.CurrentGraph);
            Assert.AreEqual("r-mid", d.Runner.CurrentNode.Id);
        }

        [Test]
        public void ConsumerPattern_SaveOnStartNodes_LoadTheRootByItsId_BootResumesEachSave()
        {
            var (c, _, _) = ThreeLevels();
            var chapters = new Dictionary<string, BaseGraph> { [c.GraphId] = c };   // the game's own root registry

            var live = NewDriver(c, autoAdvance: true);
            var saves = new List<(string node, string json)>();
            var liveEntered = new List<string>();
            live.OnNodeEntered += n =>
            {
                liveEntered.Add(n.Id);
                if (n is StartNodeData || n.IsCheckpoint)
                    saves.Add((n.Id, JsonUtility.ToJson(GraphRunSnapshot.Capture(live.Runner, live.Context))));
            };
            live.Boot();
            Assert.IsFalse(live.IsRunning, "the live flow ran to its end");
            CollectionAssert.AreEqual(new[] { "c-start", "z-start", "r-start" }, saves.ConvertAll(s => s.node));

            foreach (var (node, json) in saves)
            {
                var snapshot = Load(json);
                var root = chapters[snapshot.RootGraphId];
                Assert.IsTrue(snapshot.TryValidate(root, out var error), error?.Message);

                var d = NewDriver(root, autoAdvance: true);
                var entered = new List<string>();
                d.OnNodeEntered += n => entered.Add(n.Id);
                d.Boot(snapshot);

                var expected = liveEntered.GetRange(liveEntered.IndexOf(node), liveEntered.Count - liveEntered.IndexOf(node));
                CollectionAssert.AreEqual(expected, entered, $"save taken on '{node}'");
            }
        }

        // ── US4: a snapshot without a stack boots exactly as before ─────────────

        [Test]
        public void Boot_SnapshotWithoutStack_StartsFromItsNodeInTheDriversGraph()
        {
            var (c, _, _) = ThreeLevels();
            var ctx = new BaseContext();
            ctx.Set<int>("gold", 3);
            var snapshot = Load(JsonUtility.ToJson(GraphRunSnapshot.Capture(ctx, c.GraphId, "c-after")));
            Assert.IsFalse(snapshot.HasStack);

            var d = NewDriver(c, autoAdvance: false);
            d.Boot(snapshot);

            Assert.IsTrue(d.IsRunning);
            Assert.AreSame(c, d.Runner.CurrentGraph);
            Assert.AreEqual("c-after", d.Runner.CurrentNode.Id);
            Assert.AreEqual(3, d.Context.Get<int>("gold"));
        }
    }
}
