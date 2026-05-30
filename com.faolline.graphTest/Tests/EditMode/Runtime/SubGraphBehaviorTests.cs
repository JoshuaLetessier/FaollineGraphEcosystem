using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// Proves the SubGraph mechanism a downstream-starter lib relies on (Principle VII): context
    /// inheritance on/off, multi-level nesting, runtime cycle detection, and sub-graph End-reason
    /// locality. Pure runtime — no editor.
    /// </summary>
    [TestFixture]
    public class SubGraphBehaviorTests
    {
        private static void RunToEnd(BaseRunner runner)
        {
            int guard = 0;
            while (runner.State == RunnerState.NodeReady && guard++ < 300) runner.Proceed();
        }

        // parent: Start → SubGraph(child, inherit) → End ; child: Start → Stmt(set marker=1) → End
        private static (TestGraph parent, TestGraph child, TestSetIntAction marker) BuildParentChild(bool inherit)
        {
            var marker = ScriptableObject.CreateInstance<TestSetIntAction>();
            marker.ParameterKey = "marker"; marker.Value = 1;

            var child = ScriptableObject.CreateInstance<TestGraph>();
            var cstmt = new TestStatementNodeData { Id = "cstmt", NodeType = TestStatementNodeData.NodeTypeId };
            cstmt.OnEnterActions.Add(marker);
            child.AddNode(new StartNodeData { Id = "cs", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(cstmt);
            child.AddNode(new EndNodeData { Id = "ce", NodeType = EndNodeData.NodeTypeId });
            child.EntryNodeId = "cs";
            child.AddEdge(new BaseEdgeData { Id = "c1", FromNodeId = "cs",    ToNodeId = "cstmt", PortName = "out" });
            child.AddEdge(new BaseEdgeData { Id = "c2", FromNodeId = "cstmt", ToNodeId = "ce",    PortName = "out" });

            var parent = ScriptableObject.CreateInstance<TestGraph>();
            parent.AddNode(new StartNodeData    { Id = "ps", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = inherit });
            parent.AddNode(new EndNodeData      { Id = "pe", NodeType = EndNodeData.NodeTypeId });
            parent.EntryNodeId = "ps";
            parent.AddEdge(new BaseEdgeData { Id = "p1", FromNodeId = "ps", ToNodeId = "sg", PortName = "out" });
            parent.AddEdge(new BaseEdgeData { Id = "p2", FromNodeId = "sg", ToNodeId = "pe", PortName = "out" });

            return (parent, child, marker);
        }

        [Test]
        public void SubGraph_InheritTrue_ChildWritesVisibleInParentContext()
        {
            var (parent, child, marker) = BuildParentChild(true);
            var ctx = new BaseContext();
            try
            {
                var runner = new BaseRunner();
                runner.Start(parent, ctx, new NodeExecutorRegistry());
                RunToEnd(runner);
                Assert.IsTrue(ctx.Has("marker"),
                    "InheritParentContext=true → the child shares the parent context, so its writes are visible");
            }
            finally { Cleanup(parent, child, marker); }
        }

        [Test]
        public void SubGraph_InheritFalse_ChildWritesIsolatedFromParentContext()
        {
            var (parent, child, marker) = BuildParentChild(false);
            var ctx = new BaseContext();
            try
            {
                var runner = new BaseRunner();
                runner.Start(parent, ctx, new NodeExecutorRegistry());
                RunToEnd(runner);
                Assert.IsFalse(ctx.Has("marker"),
                    "InheritParentContext=false → the child runs in a fresh context, so its writes do NOT leak to the parent");
            }
            finally { Cleanup(parent, child, marker); }
        }

        [Test]
        public void SubGraph_NestedTwoLevels_ReachesDeepestNode()
        {
            var l3 = ScriptableObject.CreateInstance<TestGraph>();
            l3.AddNode(new StartNodeData         { Id = "l3s",  NodeType = StartNodeData.NodeTypeId });
            l3.AddNode(new TestStatementNodeData { Id = "deep", NodeType = TestStatementNodeData.NodeTypeId, Label = "deep" });
            l3.AddNode(new EndNodeData           { Id = "l3e",  NodeType = EndNodeData.NodeTypeId });
            l3.EntryNodeId = "l3s";
            l3.AddEdge(new BaseEdgeData { Id = "l3a", FromNodeId = "l3s",  ToNodeId = "deep", PortName = "out" });
            l3.AddEdge(new BaseEdgeData { Id = "l3b", FromNodeId = "deep", ToNodeId = "l3e",  PortName = "out" });

            var l2 = WrapInSubGraph(l3, "l2");
            var l1 = WrapInSubGraph(l2, "l1");

            try
            {
                var visited = new List<string>();
                var runner = new BaseRunner();
                runner.OnNodeEntered += n => visited.Add(n.Id);
                runner.Start(l1, new BaseContext(), new NodeExecutorRegistry());
                RunToEnd(runner);

                Assert.AreEqual(RunnerState.Ended, runner.State, "Nested sub-graphs must run to completion");
                Assert.Contains("deep", visited, "Two levels of nesting must reach the deepest node");
            }
            finally
            {
                Object.DestroyImmediate(l1);
                Object.DestroyImmediate(l2);
                Object.DestroyImmediate(l3);
            }
        }

        [Test]
        public void SubGraph_RuntimeCycle_ThrowsGraphCycleException()
        {
            var a = ScriptableObject.CreateInstance<TestGraph>();
            var b = ScriptableObject.CreateInstance<TestGraph>();
            BuildSubGraphOnlyGraph(a, "a", b);
            BuildSubGraphOnlyGraph(b, "b", a); // a → b → a forms a cycle
            try
            {
                var runner = new BaseRunner();
                Assert.Throws<GraphCycleException>(() =>
                {
                    runner.Start(a, new BaseContext(), new NodeExecutorRegistry());
                    RunToEnd(runner);
                }, "Entering a sub-graph already on the execution stack must throw GraphCycleException");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SubGraph_ChildEndReason_DoesNotOverrideRootEndReason()
        {
            var child = ScriptableObject.CreateInstance<TestGraph>();
            child.AddNode(new StartNodeData { Id = "cs", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(new EndNodeData   { Id = "ce", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Cancelled });
            child.EntryNodeId = "cs";
            child.AddEdge(new BaseEdgeData { Id = "c1", FromNodeId = "cs", ToNodeId = "ce", PortName = "out" });

            var parent = ScriptableObject.CreateInstance<TestGraph>();
            parent.AddNode(new StartNodeData    { Id = "ps", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "sg", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child });
            parent.AddNode(new EndNodeData      { Id = "pe", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed });
            parent.EntryNodeId = "ps";
            parent.AddEdge(new BaseEdgeData { Id = "p1", FromNodeId = "ps", ToNodeId = "sg", PortName = "out" });
            parent.AddEdge(new BaseEdgeData { Id = "p2", FromNodeId = "sg", ToNodeId = "pe", PortName = "out" });

            try
            {
                EndReason reason = EndReason.Error;
                int endedCount = 0;
                var runner = new BaseRunner();
                runner.OnEnded += r => { reason = r; endedCount++; };
                runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());
                RunToEnd(runner);

                Assert.AreEqual(1, endedCount, "OnEnded fires once (root only); the sub-graph End just pops to the parent");
                Assert.AreEqual(EndReason.Completed, reason,
                    "A sub-graph End reason is local — the run ends with the ROOT End reason");
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(child);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static TestGraph WrapInSubGraph(TestGraph target, string prefix)
        {
            var g = ScriptableObject.CreateInstance<TestGraph>();
            BuildSubGraphOnlyGraph(g, prefix, target);
            return g;
        }

        // Builds: Start → SubGraph(target) → End, with ids prefixed.
        private static void BuildSubGraphOnlyGraph(TestGraph g, string prefix, TestGraph target)
        {
            g.AddNode(new StartNodeData    { Id = prefix + "s",  NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new SubGraphNodeData { Id = prefix + "sg", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target });
            g.AddNode(new EndNodeData      { Id = prefix + "e",  NodeType = EndNodeData.NodeTypeId });
            g.EntryNodeId = prefix + "s";
            g.AddEdge(new BaseEdgeData { Id = prefix + "1", FromNodeId = prefix + "s",  ToNodeId = prefix + "sg", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = prefix + "2", FromNodeId = prefix + "sg", ToNodeId = prefix + "e",  PortName = "out" });
        }

        private static void Cleanup(TestGraph parent, TestGraph child, Object extra)
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(child);
            if (extra != null) Object.DestroyImmediate(extra);
        }
    }
}
