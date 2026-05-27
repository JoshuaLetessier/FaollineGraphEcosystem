using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseRunnerSubGraphTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private BaseGraph BuildLinearGraph(string id, string entryId, string endId)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            var start = new StartNodeData { Id = entryId, NodeType = StartNodeData.NodeTypeId };
            var end   = new EndNodeData   { Id = endId,   NodeType = EndNodeData.NodeTypeId };
            g.AddNode(start);
            g.AddNode(end);
            g.AddEdge(new BaseEdgeData { Id = $"e-{entryId}-{endId}", FromNodeId = entryId, ToNodeId = endId });
            g.EntryNodeId = entryId;
            return g;
        }

        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        // ── SubGraph push / pop ────────────────────────────────────────────────

        [Test]
        public void SubGraphNode_EntersSubGraph_OnProceed()
        {
            var child  = Track(BuildLinearGraph("child", "c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            var pStart  = new StartNodeData    { Id = "p-start",  NodeType = StartNodeData.NodeTypeId };
            var subNode = new SubGraphNodeData { Id = "p-sub",    NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true };
            var pEnd    = new EndNodeData      { Id = "p-end",    NodeType = EndNodeData.NodeTypeId };

            parent.AddNode(pStart);
            parent.AddNode(subNode);
            parent.AddNode(pEnd);
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

            // After full traversal: p-start → p-sub → c-start → c-end → p-end
            Assert.That(visited, Is.EqualTo(new[] { "p-start", "p-sub", "c-start", "c-end", "p-end" }));
            Assert.AreEqual(RunnerState.Ended, runner.State);
        }

        [Test]
        public void SubGraph_InheritParentContext_True_SharesContext()
        {
            var child  = Track(BuildLinearGraph("child", "c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            var pStart  = new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId };
            var subNode = new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = true };
            var pEnd    = new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId };

            parent.AddNode(pStart);
            parent.AddNode(subNode);
            parent.AddNode(pEnd);
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var ctx      = new BaseContext();
            var registry = new NodeExecutorRegistry();

            // Executor for child's start node — writes to the context
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) =>
                {
                    if (n.Id == "c-start") c.Set<int>("subValue", 99);
                }));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };
            runner.Start(parent, ctx, registry);

            // Shared context — write inside sub-graph is visible in parent context
            Assert.AreEqual(99, ctx.Get<int>("subValue"));
        }

        [Test]
        public void SubGraph_InheritParentContext_False_GetsFreshContext()
        {
            var child  = Track(BuildLinearGraph("child", "c-start", "c-end"));
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            var pStart  = new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId };
            var subNode = new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = false };
            var pEnd    = new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId };

            parent.AddNode(pStart);
            parent.AddNode(subNode);
            parent.AddNode(pEnd);
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var ctx      = new BaseContext();
            ctx.Set<int>("parentVal", 42);
            bool subGraphSawParentValue = false;

            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) =>
                {
                    if (n.Id == "c-start")
                        subGraphSawParentValue = c.Has("parentVal");
                }));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };
            runner.Start(parent, ctx, registry);

            Assert.IsFalse(subGraphSawParentValue,
                "Isolated sub-graph must not see parent context values.");
        }

        // ── Cycle detection ────────────────────────────────────────────────────

        [Test]
        public void CycleDetection_ThrowsGraphCycleException()
        {
            // Graph A → SubGraphNode pointing to Graph A (self-cycle)
            var graphA = Track(ScriptableObject.CreateInstance<BaseGraph>());

            var start   = new StartNodeData    { Id = "a-start", NodeType = StartNodeData.NodeTypeId };
            var subNode = new SubGraphNodeData { Id = "a-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphA, InheritParentContext = true };

            graphA.AddNode(start);
            graphA.AddNode(subNode);
            graphA.AddEdge(new BaseEdgeData { Id = "ae1", FromNodeId = "a-start", ToNodeId = "a-sub" });
            graphA.EntryNodeId = "a-start";

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            Assert.Throws<GraphCycleException>(() =>
                runner.Start(graphA, new BaseContext(), new NodeExecutorRegistry()));
        }

        [Test]
        public void GraphCycleException_CarriesOffendingGraphId()
        {
            var graphA = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var cycleId = graphA.GraphId;

            var start   = new StartNodeData    { Id = "a-start", NodeType = StartNodeData.NodeTypeId };
            var subNode = new SubGraphNodeData { Id = "a-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphA, InheritParentContext = true };

            graphA.AddNode(start);
            graphA.AddNode(subNode);
            graphA.AddEdge(new BaseEdgeData { Id = "ae1", FromNodeId = "a-start", ToNodeId = "a-sub" });
            graphA.EntryNodeId = "a-start";

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            var ex = Assert.Throws<GraphCycleException>(() =>
                runner.Start(graphA, new BaseContext(), new NodeExecutorRegistry()));

            Assert.AreEqual(cycleId, ex.CyclicGraphId);
        }

        [Test]
        public void NestedSubGraph_DepthGreaterThanOne_Works()
        {
            // grandchild: start → end
            var grandchild = Track(BuildLinearGraph("gc", "gc-start", "gc-end"));

            // child: start → subgraph(grandchild) → end
            var child = Track(ScriptableObject.CreateInstance<BaseGraph>());
            child.AddNode(new StartNodeData    { Id = "c-start", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(new SubGraphNodeData { Id = "c-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = grandchild, InheritParentContext = true });
            child.AddNode(new EndNodeData      { Id = "c-end",   NodeType = EndNodeData.NodeTypeId });
            child.AddEdge(new BaseEdgeData { Id = "ce1", FromNodeId = "c-start", ToNodeId = "c-sub" });
            child.AddEdge(new BaseEdgeData { Id = "ce2", FromNodeId = "c-sub",   ToNodeId = "c-end" });
            child.EntryNodeId = "c-start";

            // parent: start → subgraph(child) → end
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

            Assert.AreEqual(RunnerState.Ended, runner.State);
            CollectionAssert.Contains(visited, "gc-start");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

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
