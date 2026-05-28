using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Faolline.GraphCore.Tests
{
    public class SubGraphTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        /// <summary>Builds a minimal two-node graph: entryId → endId.</summary>
        private static BaseGraph BuildLinearGraph(string entryId, string endId)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            g.AddNode(new StartNodeData { Id = entryId, NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new EndNodeData   { Id = endId,   NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData  { Id = $"e-{entryId}-{endId}", FromNodeId = entryId, ToNodeId = endId });
            g.EntryNodeId = entryId;
            return g;
        }

        // ── SubGraph push ──────────────────────────────────────────────────────

        [Test]
        public void SubGraph_Push_EntersChildGraphEntryNode()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var visited = new List<string>();
            var runner  = new BaseRunner();
            runner.OnNodeEntered += n => visited.Add(n.Id);

            runner.Start(parent, new BaseContext(), new NodeExecutorRegistry()); // enters p-start
            runner.Proceed(); // enters p-sub
            runner.Proceed(); // pushes into child → enters c-start

            Assert.Contains("c-start", visited);
        }

        [Test]
        public void SubGraph_Push_ChildNodesVisitedInOrder()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var visited = new List<string>();
            var runner  = new BaseRunner();
            runner.OnNodeEntered += n => visited.Add(n.Id);
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());

            // Expected traversal: p-start → p-sub → c-start → c-end → p-end
            Assert.That(visited, Is.EqualTo(new[] { "p-start", "p-sub", "c-start", "c-end", "p-end" }));
        }

        // ── SubGraph pop ───────────────────────────────────────────────────────

        [Test]
        public void SubGraph_Pop_ChildEndNode_ResumesParent()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            BaseNodeData lastEntered = null;
            var runner = new BaseRunner();
            runner.OnNodeEntered    += n => lastEntered = n;
            runner.OnNodeCompleted  += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());

            Assert.AreEqual("p-end", lastEntered?.Id);
        }

        [Test]
        public void SubGraph_Pop_ParentCompletesNormally()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());

            Assert.AreEqual(RunnerState.Ended, runner.State);
        }

        [Test]
        public void SubGraph_Pop_OnEndedFiresExactlyOnce()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            int endedCount = 0;
            var runner = new BaseRunner();
            runner.OnEnded         += _ => endedCount++;
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());

            Assert.AreEqual(1, endedCount);
        }

        // ── Context inheritance / isolation ────────────────────────────────────

        [Test]
        public void SubGraph_InheritContext_True_SharedWriteVisibleInParent()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var ctx      = new BaseContext();
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) => { if (n.Id == "c-start") c.Set<int>("subValue", 99); }));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, ctx, registry);

            Assert.AreEqual(99, ctx.Get<int>("subValue"));
        }

        [Test]
        public void SubGraph_InheritContext_False_ParentValuesNotVisible()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = false });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var ctx = new BaseContext();
            ctx.Set<int>("parentVal", 42);
            bool subSawParentValue = false;

            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) => { if (n.Id == "c-start") subSawParentValue = c.Has("parentVal"); }));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, ctx, registry);

            Assert.IsFalse(subSawParentValue, "Isolated sub-graph must not see parent context values.");
        }

        [Test]
        public void SubGraph_InheritContext_False_ChildWriteNotVisibleInParent()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = false });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var ctx      = new BaseContext();
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) => { if (n.Id == "c-start") c.Set<int>("childVal", 42); }));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, ctx, registry);

            Assert.IsFalse(ctx.Has("childVal"),
                "Write in isolated sub-graph context must not leak into parent context.");
        }

        // ── Null target / Nested ───────────────────────────────────────────────

        [Test]
        public void SubGraph_NullTargetGraph_RaisesOnStuck()
        {
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());
            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = null, InheritParentContext = true });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.EntryNodeId = "p-start";

            bool stuck  = false;
            var  runner = new BaseRunner();
            runner.OnStuck         += () => stuck = true;
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            LogAssert.Expect(LogType.Error, "[GraphCore] SubGraphNodeData.TargetGraph is null.");
            Assert.DoesNotThrow(() => runner.Start(parent, new BaseContext(), new NodeExecutorRegistry()));
            Assert.IsTrue(stuck);
        }

        [Test]
        public void SubGraph_Nested_DepthGreaterThanOne_Completes()
        {
            // grandchild: gc-start → gc-end
            var grandchild = Track(BuildLinearGraph("gc-start", "gc-end"));

            // child: c-start → sub(grandchild) → c-end
            var child = Track(ScriptableObject.CreateInstance<BaseGraph>());
            child.AddNode(new StartNodeData    { Id = "c-start", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(new SubGraphNodeData { Id = "c-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = grandchild, InheritParentContext = true });
            child.AddNode(new EndNodeData      { Id = "c-end",   NodeType = EndNodeData.NodeTypeId });
            child.AddEdge(new BaseEdgeData { Id = "ce1", FromNodeId = "c-start", ToNodeId = "c-sub" });
            child.AddEdge(new BaseEdgeData { Id = "ce2", FromNodeId = "c-sub",   ToNodeId = "c-end" });
            child.EntryNodeId = "c-start";

            // parent: p-start → sub(child) → p-end
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());
            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var visited = new List<string>();
            var runner  = new BaseRunner();
            runner.OnNodeEntered   += n => visited.Add(n.Id);
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            runner.Start(parent, new BaseContext(), new NodeExecutorRegistry());

            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.Contains("gc-start", visited);
        }

        // ── Inner stubs ───────────────────────────────────────────────────────

        private class LambdaExecutor : INodeExecutor
        {
            private readonly Action<BaseNodeData, BaseContext> _exec;
            public string NodeType { get; }
            public LambdaExecutor(string type, Action<BaseNodeData, BaseContext> exec)
            { NodeType = type; _exec = exec; }
            public void Execute(BaseNodeData node, BaseContext context) => _exec(node, context);
        }
    }
}
