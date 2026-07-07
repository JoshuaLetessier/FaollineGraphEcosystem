using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// US1 — the driver boots the Linear runner, pumps the frame tick, re-exposes lifecycle events, and
    /// supports auto/manual advance. Verified in EditMode via the driver's public methods (no PlayMode).
    /// </summary>
    public class GraphFlowDriverTests
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

        private BaseGraph NewGraph(string entry)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            g.EntryNodeId = entry;
            _so.Add(g);
            return g;
        }

        private static StartNodeData Start(string id) => new StartNodeData { Id = id, NodeType = StartNodeData.NodeTypeId };
        private static StatementNodeData St(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };
        private static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

        private LoadSceneAction MakeLoad(string scene)
        {
            var a = ScriptableObject.CreateInstance<LoadSceneAction>();
            a.SceneName = scene; a.Mode = UnityEngine.SceneManagement.LoadSceneMode.Single;
            _so.Add(a);
            return a;
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

        [Test]
        public void Boot_EntersStart_RaisesNodeEntered()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            var entered = new List<string>();
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.Boot();

            Assert.IsTrue(d.IsRunning);
            CollectionAssert.Contains(entered, "start");
        }

        [Test]
        public void Boot_NoGraph_WarnsAndStaysInert()
        {
            var d = NewDriver(null, autoAdvance: true);
            LogAssert.Expect(LogType.Warning, "[GraphGameFlow] GraphFlowDriver.Boot: no graph assigned; staying inert.");
            d.Boot();
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void Boot_NoValidStart_WarnsAndStaysInert()
        {
            var g = NewGraph("");   // empty EntryNodeId
            g.AddNode(St("a"));
            var d = NewDriver(g, autoAdvance: true);
            LogAssert.Expect(LogType.Warning, "[GraphGameFlow] GraphFlowDriver.Boot: graph has no valid start node (check EntryNodeId); staying inert.");
            d.Boot();
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void AutoAdvance_RunsChainToEnd()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s1")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s1" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s1", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            var entered = new List<string>();
            EndReason? ended = null;
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.OnEnded += r => ended = r;
            d.Boot();

            Assert.AreEqual(new[] { "start", "s1", "end" }, entered.ToArray());
            Assert.AreEqual(EndReason.Completed, ended);
            Assert.IsFalse(d.IsRunning, "no longer running after the flow ends.");
        }

        [Test]
        public void AutoAdvance_PausesOnChoice_AndChooseByIdSelectsBranch()
        {
            var g = NewGraph("start");
            var choice = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
            choice.Choices.Add(new BaseChoice { Id = "a" });
            choice.Choices.Add(new BaseChoice { Id = "b" });
            g.AddNode(Start("start")); g.AddNode(choice); g.AddNode(End("endA")); g.AddNode(End("endB"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "c" });
            g.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "endA", PortName = "a" });
            g.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "endB", PortName = "b" });
            var d = NewDriver(g, autoAdvance: true);

            var entered = new List<string>();
            EndReason? ended = null;
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.OnEnded += r => ended = r;
            d.Boot();

            // Auto-advanced start → choice, then PAUSED (a choice is not auto-resolved under AutoAdvance).
            Assert.AreEqual(new[] { "start", "c" }, entered.ToArray());
            Assert.IsNull(ended, "a choice must not auto-resolve under AutoAdvance");
            Assert.IsTrue(d.IsRunning);

            d.ChooseById("b");   // deliberate pick → endB
            CollectionAssert.Contains(entered, "endB");
            CollectionAssert.DoesNotContain(entered, "endA");
            Assert.AreEqual(EndReason.Completed, ended);
        }

        [Test]
        public void ManualAdvance_OnlyAdvancesOnCall()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s1")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s1" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s1", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            var entered = new List<string>();
            d.OnNodeEntered += n => entered.Add(n.Id);
            d.Boot();
            Assert.AreEqual(new[] { "start" }, entered.ToArray(), "parks on start without auto-advance.");

            d.Advance();
            Assert.AreEqual(new[] { "start", "s1" }, entered.ToArray());
            d.Advance();
            CollectionAssert.Contains(entered, "end");
        }

        [Test]
        public void Tick_ForwardsTime_ResolvingAWaitNode()
        {
            var g = NewGraph("start");
            var wait = St("wait"); wait.WaitDuration = 1.0f;
            g.AddNode(Start("start")); g.AddNode(wait); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            bool ended = false;
            d.OnEnded += _ => ended = true;
            d.Boot();
            Assert.IsFalse(ended, "parked on the wait node.");

            d.Tick(0f);
            d.Tick(-5f);
            Assert.IsFalse(ended, "non-positive dt must not advance time.");

            d.Tick(0.4f);
            Assert.IsFalse(ended);
            d.Tick(0.7f);   // total 1.1 >= 1.0
            Assert.IsTrue(ended, "the wait node resolves once enough time is fed.");
        }

        [Test]
        public void RaiseSignal_WhenNotAwaiting_IsNoOp()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);
            d.Boot();   // parked on start (NodeReady), not awaiting a signal

            Assert.DoesNotThrow(() => d.RaiseSignal("whatever"));
        }

        [Test]
        public void Advance_Tick_Signal_BeforeBoot_AreNoOps()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            Assert.DoesNotThrow(() => { d.Advance(); d.Tick(1f); d.RaiseSignal("x"); });
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void Stop_DetachesFromRunner_NoFurtherCallbacks()
        {
            // OnDestroy calls Stop(); Stop() is the EditMode-callable seam (Unity does not invoke
            // OnDestroy in edit mode). The real-destroy path is covered by the PlayMode tests.
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s1")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s1" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s1", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);
            int entered = 0;
            d.OnNodeEntered += _ => entered++;
            d.Boot();
            int afterBoot = entered;
            var runner = d.Runner;

            d.Stop();

            Assert.IsFalse(d.IsRunning);
            Assert.DoesNotThrow(() => runner.Proceed());
            Assert.AreEqual(afterBoot, entered, "no driver callback fires after Stop().");
        }

        [Test]
        public void WaitingState_ReportsParkedAwaitSignal()
        {
            var g = NewGraph("start");
            var gate = St("gate"); gate.AwaitSignalName = "go";
            g.AddNode(Start("start")); g.AddNode(gate); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            Assert.IsFalse(d.IsWaitingForSignal, "not waiting before boot.");
            Assert.AreEqual("", d.CurrentAwaitSignal);

            d.Boot();   // start → gate (await "go"), parks
            Assert.IsTrue(d.IsWaitingForSignal);
            Assert.AreEqual("go", d.CurrentAwaitSignal);

            d.RaiseSignal("go");   // gate resumes → end → OnEnded
            Assert.IsFalse(d.IsWaitingForSignal, "not waiting after the flow ends.");
            Assert.AreEqual("", d.CurrentAwaitSignal);
        }

        [Test]
        public void TimeWaitQuery_ReportsAndCountsDown()
        {
            var g = NewGraph("start");
            var wait = St("wait"); wait.WaitDuration = 1.0f;
            g.AddNode(Start("start")); g.AddNode(wait); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            Assert.IsFalse(d.IsWaitingForTime, "not time-waiting before boot.");
            Assert.AreEqual(0f, d.WaitRemaining);
            Assert.AreEqual(0f, d.WaitTotal);

            d.Boot();   // start → wait node (WaitingForTime)
            Assert.IsTrue(d.IsWaitingForTime);
            Assert.AreEqual(1.0f, d.WaitTotal, 0.001f);
            Assert.AreEqual(1.0f, d.WaitRemaining, 0.001f);

            d.Tick(0.4f);
            Assert.AreEqual(0.6f, d.WaitRemaining, 0.001f);

            d.Tick(0.7f);   // total 1.1 ≥ 1.0 → resolves → end → OnEnded
            Assert.IsFalse(d.IsWaitingForTime, "no longer time-waiting after the node resolves.");
            Assert.AreEqual(0f, d.WaitRemaining);
            Assert.AreEqual(0f, d.WaitTotal);
        }

        [Test]
        public void OnWaitingForTime_FiresForTimedNode()
        {
            var g = NewGraph("start");
            var wait = St("wait"); wait.WaitDuration = 1.5f;
            g.AddNode(Start("start")); g.AddNode(wait); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            BaseNodeData timed = null; float secs = -1f;
            d.OnWaitingForTime += (n, s) => { timed = n; secs = s; };

            d.Boot();   // start → wait node (WaitingForTime) → OnWaitingForTime fires

            Assert.IsNotNull(timed, "OnWaitingForTime must fire when a timed node is entered.");
            Assert.AreEqual("wait", timed.Id);
            Assert.AreEqual(1.5f, secs, 0.001f);
        }

        // ── Boot seam: Boot(context, registry) ──────────────────────────────────

        private sealed class SentinelExecutor : INodeExecutor
        {
            public string NodeType => StatementNodeData.NodeTypeId;
            public void Execute(BaseNodeData node, BaseContext context) => context.Set<bool>("executorRan", true);
            public void Undo(BaseNodeData node, BaseContext context) { }
        }

        private BaseGraph LinearGraph()   // start → s → end
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(St("s")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s", ToNodeId = "end" });
            return g;
        }

        [Test]
        public void BootWithContext_RunsOnThatContext_AndKeepsSeededState()
        {
            var d = NewDriver(LinearGraph(), autoAdvance: true);
            var ctx = new GameFlowContext();
            ctx.Set<int>("seed", 42);

            d.Boot(ctx, null);

            Assert.AreSame(ctx, d.Context, "the flow runs on the provided context.");
            Assert.AreEqual(42, d.Context.Get<int>("seed"), "seeded state survives.");
        }

        // Builds start → s → end where node 's' references parameter 'p' (default) through a ResumeConditions
        // entry that is scanned by InitFromGraph but inert for linear traversal (never awaited). Lets a test
        // assert whether the graph default was seeded, without the reference altering the flow.
        private BaseGraph GraphDeclaringParam(ParameterName p)
        {
            var g = NewGraph("start");
            var s = St("s");
            var probe = ScriptableObject.CreateInstance<IntCondition>(); probe.Parameter = p; _so.Add(probe);
            s.ResumeConditions.Add(probe);
            g.AddNode(Start("start")); g.AddNode(s); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "s" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "s", ToNodeId = "end" });
            return g;
        }

        [Test]
        public void BootWithContext_DoesNotInitFromGraph()
        {
            var p = ParameterName.Int("p", 1); _so.Add(p);
            var g = GraphDeclaringParam(p);
            var d = NewDriver(g, autoAdvance: true);
            var ctx = new GameFlowContext();
            ctx.Set<int>(p, 5);   // pre-seeded; must NOT be reset to the graph default (1)

            d.Boot(ctx, null);

            Assert.AreEqual(5, d.Context.Get<int>(p), "a provided context is not re-initialised from the graph.");
        }

        [Test]
        public void BootWithContext_FillsSceneLoaderWhenAbsent_ElseKeepsIt()
        {
            // Absent → filled with the driver's loader.
            var g1 = NewGraph("start");
            var load1 = St("load"); load1.OnEnterActions.Add(MakeLoad("X"));
            g1.AddNode(Start("start")); g1.AddNode(load1); g1.AddNode(End("end"));
            g1.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "load" });
            g1.AddEdge(new BaseEdgeData { FromNodeId = "load", ToNodeId = "end" });
            var d1 = NewDriver(g1, autoAdvance: true);
            var driverStub = new StubSceneLoader(); d1.SceneLoader = driverStub;
            var ctxNoLoader = new GameFlowContext();   // SceneLoader == null

            d1.Boot(ctxNoLoader, null);
            Assert.AreEqual("X", driverStub.LastScene, "a context without a loader gets the driver's.");

            // Present → kept.
            var g2 = NewGraph("start");
            var load2 = St("load"); load2.OnEnterActions.Add(MakeLoad("Y"));
            g2.AddNode(Start("start")); g2.AddNode(load2); g2.AddNode(End("end"));
            g2.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "load" });
            g2.AddEdge(new BaseEdgeData { FromNodeId = "load", ToNodeId = "end" });
            var d2 = NewDriver(g2, autoAdvance: true);
            var driverStub2 = new StubSceneLoader(); d2.SceneLoader = driverStub2;
            var ownStub = new StubSceneLoader();
            var ctxOwnLoader = new GameFlowContext { SceneLoader = ownStub };

            d2.Boot(ctxOwnLoader, null);
            Assert.AreEqual("Y", ownStub.LastScene, "a context keeps its own loader.");
            Assert.AreEqual(0, driverStub2.Calls.Count, "the driver's loader is not used when the context has one.");
        }

        [Test]
        public void BootWithRegistry_InvokesCustomExecutor()
        {
            var d = NewDriver(LinearGraph(), autoAdvance: true);
            var ctx = new GameFlowContext();
            var registry = new NodeExecutorRegistry();
            registry.Register(new SentinelExecutor());

            d.Boot(ctx, registry);

            Assert.IsTrue(ctx.TryGet<bool>("executorRan", out var ran) && ran,
                "the provided registry's executor ran for the statement node.");
        }

        [Test]
        public void BootNoArgs_StillInitialisesFromGraph()
        {
            var p = ParameterName.Int("p", 7); _so.Add(p);
            var g = GraphDeclaringParam(p);
            var d = NewDriver(g, autoAdvance: true);

            d.Boot();   // unchanged: fresh context + InitFromGraph (seeds referenced ParameterName defaults)

            Assert.IsNotNull(d.Context);
            Assert.AreEqual(7, d.Context.Get<int>(p), "no-arg Boot still initialises from the graph.");
        }

        [Test]
        public void BootWithContext_HonoursAlreadyRunningGuard()
        {
            var d = NewDriver(LinearGraph(), autoAdvance: false);
            d.Boot();
            LogAssert.Expect(LogType.Warning, "[GraphGameFlow] GraphFlowDriver.Boot: already running; ignored.");
            d.Boot(new GameFlowContext(), null);   // same guard as Boot()
        }

        // ── Edge cases ──────────────────────────────────────────────────────

        private sealed class FalseCond : BaseCondition { public override bool Evaluate(BaseContext c) => false; }

        [Test]
        public void OnStuck_FiresWhenAllOutgoingEdgesBlocked()
        {
            // "Stuck" = an outgoing edge EXISTS but no branch is traversable (all conditions false), so the flow
            // dead-locks. (A node with NO outgoing edge is a dead-end that ENDS the flow — see DeadEnd test.)
            var cond = ScriptableObject.CreateInstance<FalseCond>();
            _so.Add(cond);
            var g = NewGraph("start");
            g.AddNode(Start("start"));
            g.AddNode(St("mid"));
            g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "mid" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "mid", ToNodeId = "end", Condition = cond }); // blocked
            var d = NewDriver(g, autoAdvance: true);

            bool stuck = false;
            d.OnStuck += () => stuck = true;
            d.Boot();

            Assert.IsTrue(stuck, "OnStuck must fire when an edge exists but every branch is condition-blocked.");
        }

        [Test]
        public void DeadEndNode_EndsFlow_NotStuck()
        {
            // A node with NO outgoing edge is a terminal node: the runner treats it as an implicit completion
            // (mirrors BaseRunner's documented "terminal non-end node → Completed" and the core dead-end tests),
            // so the flow ENDS rather than getting stuck.
            var g = NewGraph("start");
            g.AddNode(Start("start"));
            g.AddNode(St("dead"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "dead" });
            // "dead" has no outgoing edge
            var d = NewDriver(g, autoAdvance: true);

            bool stuck = false;
            EndReason? ended = null;
            d.OnStuck += () => stuck = true;
            d.OnEnded += r => ended = r;
            d.Boot();

            Assert.IsFalse(stuck, "a dead-end node ends the flow; it is not 'stuck'.");
            Assert.AreEqual(EndReason.Completed, ended, "a terminal non-end node completes the flow.");
        }

        [Test]
        public void Stop_ThenBoot_CanRestart()
        {
            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: false);

            d.Boot();
            Assert.IsTrue(d.IsRunning);
            d.Stop();
            Assert.IsFalse(d.IsRunning);

            d.Boot();
            Assert.IsTrue(d.IsRunning, "can reboot after a clean stop.");
        }

        [Test]
        public void DoubleStop_DoesNotThrow()
        {
            var d = NewDriver(LinearGraph(), autoAdvance: false);
            d.Boot();
            Assert.DoesNotThrow(() => { d.Stop(); d.Stop(); });
        }

        [Test]
        public void SubGraph_TraversesNestedGraphAndReturns()
        {
            var sub = NewGraph("sub_start");
            sub.AddNode(Start("sub_start")); sub.AddNode(End("sub_end"));
            sub.AddEdge(new BaseEdgeData { FromNodeId = "sub_start", ToNodeId = "sub_end" });

            var subNode = new SubGraphNodeData
            {
                Id = "sg", NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = sub, InheritParentContext = true
            };

            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(subNode); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "sg" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "sg", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            EndReason? ended = null;
            d.OnEnded += r => ended = r;
            d.Boot();

            Assert.AreEqual(EndReason.Completed, ended, "sub-graph traversed and flow completed.");
            Assert.IsFalse(d.IsRunning);
        }

        [Test]
        public void SubGraph_InheritContext_SharesState()
        {
            var sub = NewGraph("sub_start");
            var subSt = St("sub_st");
            sub.AddNode(Start("sub_start")); sub.AddNode(subSt); sub.AddNode(End("sub_end"));
            sub.AddEdge(new BaseEdgeData { FromNodeId = "sub_start", ToNodeId = "sub_st" });
            sub.AddEdge(new BaseEdgeData { FromNodeId = "sub_st", ToNodeId = "sub_end" });

            var fromSub = ParameterName.Bool("from_sub"); _so.Add(fromSub);
            var setAction = ScriptableObject.CreateInstance<SetBoolAction>();
            setAction.Parameter = fromSub; setAction.Value = true;
            _so.Add(setAction);
            subSt.OnEnterActions.Add(setAction);

            var subNode = new SubGraphNodeData
            {
                Id = "sg", NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = sub, InheritParentContext = true
            };

            var g = NewGraph("start");
            g.AddNode(Start("start")); g.AddNode(subNode); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "sg" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "sg", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            d.Boot();

            Assert.IsTrue(d.Context.TryGet<bool>(fromSub, out var v) && v,
                "sub-graph with inherited context writes into the parent's context.");
        }

        [Test]
        public void RaiseSignal_ResolvesAwaitAndContinues()
        {
            var g = NewGraph("start");
            var gate = St("gate"); gate.AwaitSignalName = "unlock";
            g.AddNode(Start("start")); g.AddNode(gate); g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            BaseNodeData signalNode = null; string signalName = null;
            d.OnWaitingForSignal += (n, s) => { signalNode = n; signalName = s; };
            EndReason? ended = null;
            d.OnEnded += r => ended = r;
            d.Boot();

            Assert.IsNotNull(signalNode, "OnWaitingForSignal must fire.");
            Assert.AreEqual("gate", signalNode.Id);
            Assert.AreEqual("unlock", signalName);
            Assert.IsNull(ended);

            d.RaiseSignal("unlock");
            Assert.AreEqual(EndReason.Completed, ended, "signal resolved the await and the flow completed.");
        }

        // ── Auto-advance pump: iterative, not recursive (a cycle with no pause node must not crash) ──

        [Test]
        public void AutoAdvance_LongPassThroughChain_CompletesInOneBoot()
        {
            // A long linear chain (no pause anywhere) must still auto-advance start-to-end in a single
            // Boot() call — proves the iterative drain handles ordinary long chains exactly like the old
            // recursive one did, just without growing the call stack.
            const int chainLength = 300;
            var g = NewGraph("start");
            g.AddNode(Start("start"));
            var prev = "start";
            for (int i = 0; i < chainLength; i++)
            {
                var id = "n" + i;
                g.AddNode(St(id));
                g.AddEdge(new BaseEdgeData { FromNodeId = prev, ToNodeId = id });
                prev = id;
            }
            g.AddNode(End("end"));
            g.AddEdge(new BaseEdgeData { FromNodeId = prev, ToNodeId = "end" });
            var d = NewDriver(g, autoAdvance: true);

            EndReason? ended = null;
            d.OnEnded += r => ended = r;
            var enteredCount = 0;
            d.OnNodeEntered += _ => enteredCount++;

            d.Boot();

            Assert.AreEqual(EndReason.Completed, ended);
            Assert.AreEqual(chainLength + 2, enteredCount, "start + every chain node + end all fire OnNodeEntered, in order, exactly once.");
        }

        [Test]
        public void AutoAdvance_CycleWithNoPauseNode_StopsAtCapInsteadOfCrashing()
        {
            // a -> b -> a, forever, with no await/wait/choice/end anywhere on the loop: the historical
            // recursive implementation would grow the native call stack without bound (an uncatchable
            // StackOverflowException). The iterative drain must instead stop at MaxAutoAdvanceSteps and warn.
            var g = NewGraph("start");
            g.AddNode(Start("start"));
            g.AddNode(St("a"));
            g.AddNode(St("b"));
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "a" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "a", ToNodeId = "b" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "b", ToNodeId = "a" });
            var d = NewDriver(g, autoAdvance: true);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Auto-advance exceeded"));
            Assert.DoesNotThrow(() => d.Boot(), "a pause-free cycle must stop cleanly, never crash.");

            Assert.IsTrue(d.IsRunning, "the flow is still alive — stopped, not ended, not crashed.");
            Assert.IsTrue(d.Runner.CurrentNode.Id == "a" || d.Runner.CurrentNode.Id == "b",
                "the runner is left parked somewhere on the cycle, in a consistent state.");
        }

        [Test]
        public void AutoAdvance_CycleBrokenByAwait_NeverHitsTheCap()
        {
            // The same shape as above, but "b" awaits a signal — a legitimate game-shell loop (documented
            // pattern: a looping flow bounded by HistoryDepth). This must run indefinitely fine and never
            // trip the safety cap, since each lap genuinely pauses.
            var g = NewGraph("start");
            var b = St("b"); b.AwaitSignalName = "tick";
            g.AddNode(Start("start"));
            g.AddNode(St("a"));
            g.AddNode(b);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "a" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "a", ToNodeId = "b" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "b", ToNodeId = "a" });
            var d = NewDriver(g, autoAdvance: true);

            d.Boot();
            for (int i = 0; i < 50; i++) d.RaiseSignal("tick");   // 50 laps — well past MaxAutoAdvanceSteps if it were buggy

            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(d.IsRunning);
            Assert.AreEqual("b", d.Runner.CurrentNode.Id, "parked back on the await after each lap.");
        }
    }
}
