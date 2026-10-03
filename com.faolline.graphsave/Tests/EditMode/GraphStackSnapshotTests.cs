using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;
using Object = UnityEngine.Object;

namespace Faolline.GraphSave.Tests
{
    /// <summary>
    /// A snapshot captured inside sub-graphs records the whole execution stack, and restores from the ROOT graph the
    /// caller supplies: back inside the deepest sub-graph, then out through every enclosing graph.
    /// </summary>
    public class GraphStackSnapshotTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        // ── Graph building ───────────────────────────────────────────────────────

        private BaseGraph Chain(params BaseNodeData[] nodes)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            _objects.Add(g);
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
        private static EndNodeData End(string id) => new EndNodeData { Id = id, NodeType = EndNodeData.NodeTypeId };

        private static SubGraphNodeData Sub(string id, BaseGraph target, GraphRunContextMode mode = GraphRunContextMode.Shared)
            => new SubGraphNodeData
            {
                Id = id, NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target,
                OpensScope = mode == GraphRunContextMode.Scoped, InheritParentContext = mode == GraphRunContextMode.Shared
            };

        /// <summary>C: c-start → c-sub(→Z) → c-after → c-end ; Z: z-start → z-sub(→R) → z-mid → z-end ; R: r-start → r-mid → r-end.</summary>
        private (BaseGraph c, BaseGraph z, BaseGraph r) ThreeLevels(GraphRunContextMode zMode = GraphRunContextMode.Shared,
                                                                    GraphRunContextMode rMode = GraphRunContextMode.Shared)
        {
            var r = Chain(Start("r-start"), Step("r-mid"), End("r-end"));
            var z = Chain(Start("z-start"), Sub("z-sub", r, rMode), Step("z-mid"), End("z-end"));
            var c = Chain(Start("c-start"), Sub("c-sub", z, zMode), Step("c-after"), End("c-end"));
            return (c, z, r);
        }

        /// <summary>Starts <paramref name="root"/> and proceeds until <paramref name="nodeId"/> is the current node.</summary>
        private static BaseRunner RunTo(BaseGraph root, BaseContext context, string nodeId)
        {
            var runner = new BaseRunner();
            runner.Start(root, context, new NodeExecutorRegistry());
            for (int guard = 0; runner.CurrentNode.Id != nodeId; guard++)
            {
                Assert.Less(guard, 50, $"never reached '{nodeId}'");
                runner.Proceed();
            }
            return runner;
        }

        private static List<string> RecordEntered(BaseRunner runner)
        {
            var entered = new List<string>();
            runner.OnNodeEntered += n => entered.Add(n.Id);
            return entered;
        }

        private static void AutoProceed(BaseRunner runner)
            => runner.OnNodeCompleted += _ => { if (runner.State == RunnerState.NodeReady) runner.Proceed(); };

        private static void AssertFrame(GraphRunSnapshot.Frame frame, BaseGraph graph, string nodeId, GraphRunContextMode mode)
        {
            Assert.AreEqual(graph.GraphId, frame.GraphId, "GraphId");
            Assert.AreEqual(nodeId, frame.NodeId, "NodeId");
            Assert.AreEqual(mode, frame.ContextMode, "ContextMode");
        }

        // ── US1: capture ─────────────────────────────────────────────────────────

        [Test]
        public void Capture_InsideASubGraph_RecordsTheStackRootFirst()
        {
            var (c, z, _) = ThreeLevels();
            var runner = RunTo(c, new BaseContext(), "z-start");

            var snap = GraphRunSnapshot.Capture(runner, new BaseContext());

            Assert.IsTrue(snap.HasStack);
            Assert.AreEqual(2, snap.Frames.Count);
            AssertFrame(snap.Frames[0], c, "c-sub", GraphRunContextMode.Shared);
            AssertFrame(snap.Frames[1], z, "z-start", GraphRunContextMode.Shared);
            Assert.AreEqual(z.GraphId, snap.GraphId, "GraphId stays the deepest level's");
            Assert.AreEqual("z-start", snap.CurrentNodeId, "CurrentNodeId stays the deepest level's");
            Assert.AreEqual(c.GraphId, snap.RootGraphId);
        }

        [Test]
        public void Capture_ThreeLevelsDeep_RecordsEveryLevel()
        {
            var (c, z, r) = ThreeLevels();
            var runner = RunTo(c, new BaseContext(), "r-mid");

            var snap = GraphRunSnapshot.Capture(runner, new BaseContext());

            Assert.AreEqual(3, snap.Frames.Count);
            AssertFrame(snap.Frames[0], c, "c-sub", GraphRunContextMode.Shared);
            AssertFrame(snap.Frames[1], z, "z-sub", GraphRunContextMode.Shared);
            AssertFrame(snap.Frames[2], r, "r-mid", GraphRunContextMode.Shared);
            Assert.AreEqual(c.GraphId, snap.RootGraphId);
        }

        [Test]
        public void Capture_AtRootLevel_RecordsOneFrame()
        {
            var (c, _, _) = ThreeLevels();
            var runner = RunTo(c, new BaseContext(), "c-after");

            var snap = GraphRunSnapshot.Capture(runner, new BaseContext());

            Assert.AreEqual(1, snap.Frames.Count);
            AssertFrame(snap.Frames[0], c, "c-after", GraphRunContextMode.Shared);
            Assert.AreEqual(c.GraphId, snap.RootGraphId);
        }

        [Test]
        public void JsonUtility_RoundTrip_KeepsTheStack_IncludingContextModes()
        {
            var (c, z, r) = ThreeLevels(GraphRunContextMode.Scoped, GraphRunContextMode.Shared);
            var runner = RunTo(c, new BaseContext(), "r-mid");

            var back = JsonUtility.FromJson<GraphRunSnapshot>(JsonUtility.ToJson(GraphRunSnapshot.Capture(runner, new BaseContext())));

            Assert.AreEqual(3, back.Frames.Count);
            AssertFrame(back.Frames[0], c, "c-sub", GraphRunContextMode.Shared);
            AssertFrame(back.Frames[1], z, "z-sub", GraphRunContextMode.Scoped);
            AssertFrame(back.Frames[2], r, "r-mid", GraphRunContextMode.Shared);
            Assert.AreEqual(c.GraphId, back.RootGraphId);
            Assert.AreEqual(3, back.GetExecutionPath().Count);
            Assert.AreEqual(GraphRunContextMode.Scoped, back.GetExecutionPath()[1].ContextMode);
        }

        // ── US1: restore from the root ───────────────────────────────────────────

        [Test]
        public void Restore_FromTheRoot_ResumesInsideTheSubGraph_WithTheSavedValues()
        {
            var (c, z, _) = ThreeLevels();
            var context = new BaseContext();
            context.Set<int>("gold", 7);
            context.AddToCollection("keys", "silver");
            var json = JsonUtility.ToJson(GraphRunSnapshot.Capture(RunTo(c, context, "z-mid"), context));

            var restoredContext = new BaseContext();
            var runner = new BaseRunner();
            JsonUtility.FromJson<GraphRunSnapshot>(json).Restore(runner, c, restoredContext);

            Assert.AreEqual("z-mid", runner.CurrentNode.Id);
            Assert.AreSame(z, runner.CurrentGraph);
            Assert.AreEqual(7, restoredContext.Get<int>("gold"));
            Assert.IsTrue(restoredContext.CollectionContains("keys", "silver"));
        }

        [Test]
        public void Restore_ThreeLevels_ReturnsThroughEveryEnclosingGraph_ToTheRootsEnd()
        {
            var (c, _, _) = ThreeLevels();
            var json = JsonUtility.ToJson(GraphRunSnapshot.Capture(RunTo(c, new BaseContext(), "r-mid"), new BaseContext()));

            var runner = new BaseRunner();
            var entered = RecordEntered(runner);
            int ended = 0;
            runner.OnEnded += _ => ended++;
            AutoProceed(runner);
            JsonUtility.FromJson<GraphRunSnapshot>(json).Restore(runner, c, new BaseContext());

            CollectionAssert.AreEqual(new[] { "r-mid", "r-end", "z-mid", "z-end", "c-after", "c-end" }, entered);
            Assert.AreEqual(1, ended, "the run ends once, at the root's end — not at the sub-graph's");
        }

        [Test]
        public void ConsumerPattern_SaveOnEveryStartAndCheckpoint_EachSaveResumesWhereItWasTaken()
        {
            // The game saves from inside OnNodeEntered on every Start node (sub-graph Starts included) and checkpoint.
            var (c, _, _) = ThreeLevels();
            var context = new BaseContext();
            var live = new BaseRunner();
            var saves = new List<(string node, string json)>();
            live.OnNodeEntered += n =>
            {
                if (n is StartNodeData || n.IsCheckpoint)
                    saves.Add((n.Id, JsonUtility.ToJson(GraphRunSnapshot.Capture(live, context))));
            };
            var liveEntered = RecordEntered(live);
            AutoProceed(live);

            Assert.DoesNotThrow(() => live.Start(c, context, new NodeExecutorRegistry()));
            Assert.AreEqual(RunnerState.Ended, live.State, "capturing inside OnNodeEntered must not disturb the run");
            CollectionAssert.AreEqual(new[] { "c-start", "z-start", "r-start" }, saves.ConvertAll(s => s.node));

            foreach (var (node, json) in saves)
            {
                var runner = new BaseRunner();
                var entered = RecordEntered(runner);
                AutoProceed(runner);
                JsonUtility.FromJson<GraphRunSnapshot>(json).Restore(runner, c, new BaseContext());

                var expected = liveEntered.GetRange(liveEntered.IndexOf(node), liveEntered.Count - liveEntered.IndexOf(node));
                CollectionAssert.AreEqual(expected, entered, $"save taken on '{node}'");
            }
        }

        // ── US5: fresh-context levels ───────────────────────────────────────────

        [Test]
        public void Capture_FreshEnclosingLevel_ReportsAnError_AndStillReturnsTheSnapshot()
        {
            var (c, _, _) = ThreeLevels(GraphRunContextMode.Fresh, GraphRunContextMode.Shared);
            var context = new BaseContext();
            var runner = RunTo(c, context, "r-mid");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(@"^\[GraphSave\] Capture: frame 1 \(graph '[^']+'\) runs on its own fresh context, whose values are not saved"));

            var snap = GraphRunSnapshot.Capture(runner, context);

            Assert.AreEqual(3, snap.Frames.Count);
            Assert.AreEqual(GraphRunContextMode.Fresh, snap.Frames[1].ContextMode);
        }

        [Test]
        public void Capture_FreshDeepestLevelPastItsEntry_ReportsAnError()
        {
            var (c, _, _) = ThreeLevels(GraphRunContextMode.Fresh);
            var context = new BaseContext();
            var runner = RunTo(c, context, "z-mid");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(@"^\[GraphSave\] Capture: frame 1 "));

            GraphRunSnapshot.Capture(runner, context);
        }

        [Test]
        public void Capture_FreshDeepestLevelOnItsEntry_IsExact_NoError_AndRestores()
        {
            var (c, z, _) = ThreeLevels(GraphRunContextMode.Fresh);
            var context = new BaseContext();
            var live = RunTo(c, context, "z-start");

            var json = JsonUtility.ToJson(GraphRunSnapshot.Capture(live, context));
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();

            var runner = new BaseRunner();
            JsonUtility.FromJson<GraphRunSnapshot>(json).Restore(runner, c, new BaseContext());
            Assert.AreEqual("z-start", runner.CurrentNode.Id);
            Assert.AreSame(z, runner.CurrentGraph);
        }

        [Test]
        public void Capture_ScopedLevel_ReportsNothing()
        {
            var (c, _, _) = ThreeLevels(GraphRunContextMode.Scoped);
            var context = new BaseContext();
            var runner = RunTo(c, context, "z-mid");

            var snap = GraphRunSnapshot.Capture(runner, context);

            Assert.AreEqual(GraphRunContextMode.Scoped, snap.Frames[1].ContextMode);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        // ── US4: snapshots without a stack behave as before ─────────────────────

        [Test]
        public void JsonWrittenBeforeFramesExisted_ReadsAsNoStack()
        {
            const string oldJson = "{\"GraphId\":\"g-old\",\"CurrentNodeId\":\"n\",\"Variables\":[{\"Key\":\"gold\",\"Type\":\"int\",\"Value\":\"4\"}],\"Collections\":[],\"RaisedSignals\":[]}";

            var snap = JsonUtility.FromJson<GraphRunSnapshot>(oldJson);

            Assert.IsNotNull(snap.Frames);
            Assert.IsFalse(snap.HasStack);
            Assert.AreEqual("g-old", snap.RootGraphId, "without a stack, the root is the snapshot's own graph");
            Assert.AreEqual(0, snap.GetExecutionPath().Count);
        }

        [Test]
        public void ExplicitIdsCapture_HasNoStack_AndRestoresIntoTheGivenGraphAsBefore()
        {
            var (c, _, _) = ThreeLevels();
            var ctx = new BaseContext();
            ctx.Set<int>("gold", 4);
            var snap = JsonUtility.FromJson<GraphRunSnapshot>(JsonUtility.ToJson(GraphRunSnapshot.Capture(ctx, "any-id", "c-after")));
            Assert.IsFalse(snap.HasStack);
            Assert.IsTrue(snap.TryValidate(c, out var error), "nothing to check without a stack");
            Assert.IsNull(error);
            Assert.IsFalse(snap.TryValidate(null, out error));
            Assert.AreEqual(GraphRunPathErrorReason.NullRootGraph, error.Reason);

            var restored = new BaseContext();
            var runner = new BaseRunner();
            snap.Restore(runner, c, restored);

            Assert.AreEqual("c-after", runner.CurrentNode.Id, "re-entered in the supplied graph, as before");
            Assert.AreEqual(4, restored.Get<int>("gold"));
        }

        // ── US2: mismatches are reported, nothing is changed ────────────────────

        private static BaseNodeData FindNode(BaseGraph g, string id)
        {
            foreach (var n in g.Nodes) if (n.Id == id) return n;
            return null;
        }

        [Test]
        public void TryValidate_AgainstItsRoot_Succeeds_WithoutSideEffect()
        {
            var (c, _, _) = ThreeLevels();
            var snap = GraphRunSnapshot.Capture(RunTo(c, new BaseContext(), "r-mid"), new BaseContext());

            Assert.IsTrue(snap.TryValidate(c, out var error));
            Assert.IsNull(error);
            Assert.AreEqual(3, snap.Frames.Count);
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryValidate_AgainstAnotherGraph_ReportsWrongRoot()
        {
            var (c, z, _) = ThreeLevels();
            var snap = GraphRunSnapshot.Capture(RunTo(c, new BaseContext(), "z-mid"), new BaseContext());

            Assert.IsFalse(snap.TryValidate(z, out var error), "the deepest graph is not the root");
            Assert.AreEqual(GraphRunPathErrorReason.RootGraphMismatch, error.Reason);
            Assert.AreEqual(0, error.FrameIndex);
            Assert.AreEqual(-1, error.DeepestValidFrameIndex);
        }

        [Test]
        public void TryValidate_NodeRemovedSinceTheSave_ReportsTheLevel()
        {
            var (c, z, _) = ThreeLevels();
            var snap = GraphRunSnapshot.Capture(RunTo(c, new BaseContext(), "z-mid"), new BaseContext());
            snap.Frames[1].NodeId = "z-removed";   // same effect as deleting the node from Z after saving

            Assert.IsFalse(snap.TryValidate(c, out var error));
            Assert.AreEqual(GraphRunPathErrorReason.NodeMissing, error.Reason);
            Assert.AreEqual(1, error.FrameIndex);
            Assert.AreEqual(0, error.DeepestValidFrameIndex);
        }

        [Test]
        public void TryValidate_SubGraphRetargetedSinceTheSave_ReportsTheLevel()
        {
            var (c, _, _) = ThreeLevels();
            var snap = GraphRunSnapshot.Capture(RunTo(c, new BaseContext(), "z-mid"), new BaseContext());
            ((SubGraphNodeData)FindNode(c, "c-sub")).TargetGraph = Chain(Start("o-start"), End("o-end"));

            Assert.IsFalse(snap.TryValidate(c, out var error));
            Assert.AreEqual(GraphRunPathErrorReason.SubGraphTargetMismatch, error.Reason);
            Assert.AreEqual(0, error.FrameIndex);
            Assert.AreEqual(0, error.DeepestValidFrameIndex);
        }

        [Test]
        public void Restore_Mismatch_LogsAnError_AndChangesNothing()
        {
            var (c, z, _) = ThreeLevels();
            var saved = new BaseContext();
            saved.Set<int>("gold", 7);
            saved.AddToCollection("keys", "silver");
            var snap = GraphRunSnapshot.Capture(RunTo(c, saved, "z-mid"), saved);

            var context = new BaseContext();
            context.Set<int>("gold", 1);
            var runner = new BaseRunner();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[GraphSave\] Restore refused: Frame 0: .*Nothing was changed\.$"));

            snap.Restore(runner, z, context);   // z is not the root

            Assert.AreEqual(1, context.Get<int>("gold"), "values are not applied when the restore is refused");
            Assert.IsFalse(context.CollectionContains("keys", "silver"));
            Assert.AreEqual(RunnerState.Idle, runner.State);
            Assert.IsNull(runner.CurrentNode);
        }

        [Test]
        public void Restore_SingleLevelStackedSnapshot_IntoAnotherGraph_IsRefused()
        {
            // Captured from a runner at root level: the snapshot knows its root, so it is not re-entered into a graph
            // that merely happens to contain a node with the same id.
            var a = Chain(Start("s"), Step("n"), End("e"));
            var b = Chain(Start("s"), Step("n"), End("e"));
            var snap = GraphRunSnapshot.Capture(RunTo(a, new BaseContext(), "n"), new BaseContext());

            var runner = new BaseRunner();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[GraphSave\] Restore refused: "));
            snap.Restore(runner, b, new BaseContext());

            Assert.AreEqual(RunnerState.Idle, runner.State);
        }
    }
}
