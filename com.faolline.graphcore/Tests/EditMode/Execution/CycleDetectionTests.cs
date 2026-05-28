using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class CycleDetectionTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        // ── Direct cycle ──────────────────────────────────────────────────────

        [Test]
        public void Cycle_Direct_SelfReference_ThrowsGraphCycleException()
        {
            // Graph A: start → sub(A)  — self-cycle
            var graphA = Track(ScriptableObject.CreateInstance<BaseGraph>());
            graphA.AddNode(new StartNodeData    { Id = "a-start", NodeType = StartNodeData.NodeTypeId });
            graphA.AddNode(new SubGraphNodeData { Id = "a-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphA, InheritParentContext = true });
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
        public void Cycle_Direct_ThrowsBeforeAnyNodeEntered()
        {
            // Verifies no executor ran for the cyclic re-entry (exactly 1 call for the root start).
            var graphA = Track(ScriptableObject.CreateInstance<BaseGraph>());
            graphA.AddNode(new StartNodeData    { Id = "a-start", NodeType = StartNodeData.NodeTypeId });
            graphA.AddNode(new SubGraphNodeData { Id = "a-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphA, InheritParentContext = true });
            graphA.AddEdge(new BaseEdgeData { Id = "ae1", FromNodeId = "a-start", ToNodeId = "a-sub" });
            graphA.EntryNodeId = "a-start";

            int callCount = 0;
            var registry  = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (_, __) => callCount++));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            try { runner.Start(graphA, new BaseContext(), registry); }
            catch (GraphCycleException) { }

            Assert.AreEqual(1, callCount,
                "Executor must have been called exactly once (for root start); " +
                "the cyclic re-entry target must not have been entered.");
        }

        // ── Indirect cycle ────────────────────────────────────────────────────

        [Test]
        public void Cycle_Indirect_ThreeGraphChain_ThrowsGraphCycleException()
        {
            // A → sub(B) → sub(A)  — indirect cycle A → B → A
            var graphA = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var graphB = Track(ScriptableObject.CreateInstance<BaseGraph>());

            graphA.AddNode(new StartNodeData    { Id = "a-start", NodeType = StartNodeData.NodeTypeId });
            graphA.AddNode(new SubGraphNodeData { Id = "a-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphB, InheritParentContext = true });
            graphA.AddEdge(new BaseEdgeData { Id = "ae1", FromNodeId = "a-start", ToNodeId = "a-sub" });
            graphA.EntryNodeId = "a-start";

            graphB.AddNode(new StartNodeData    { Id = "b-start", NodeType = StartNodeData.NodeTypeId });
            graphB.AddNode(new SubGraphNodeData { Id = "b-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphA, InheritParentContext = true });
            graphB.AddEdge(new BaseEdgeData { Id = "be1", FromNodeId = "b-start", ToNodeId = "b-sub" });
            graphB.EntryNodeId = "b-start";

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            Assert.Throws<GraphCycleException>(() =>
                runner.Start(graphA, new BaseContext(), new NodeExecutorRegistry()));
        }

        [Test]
        public void Cycle_Indirect_ExceptionCarriesOffendingGraphId()
        {
            var graphA = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var graphB = Track(ScriptableObject.CreateInstance<BaseGraph>());
            string cycleId = graphA.GraphId;

            graphA.AddNode(new StartNodeData    { Id = "a-start", NodeType = StartNodeData.NodeTypeId });
            graphA.AddNode(new SubGraphNodeData { Id = "a-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphB, InheritParentContext = true });
            graphA.AddEdge(new BaseEdgeData { Id = "ae1", FromNodeId = "a-start", ToNodeId = "a-sub" });
            graphA.EntryNodeId = "a-start";

            graphB.AddNode(new StartNodeData    { Id = "b-start", NodeType = StartNodeData.NodeTypeId });
            graphB.AddNode(new SubGraphNodeData { Id = "b-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = graphA, InheritParentContext = true });
            graphB.AddEdge(new BaseEdgeData { Id = "be1", FromNodeId = "b-start", ToNodeId = "b-sub" });
            graphB.EntryNodeId = "b-start";

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ =>
            {
                if (runner.State == RunnerState.NodeReady) runner.Proceed();
            };

            var ex = Assert.Throws<GraphCycleException>(() =>
                runner.Start(graphA, new BaseContext(), new NodeExecutorRegistry()));

            Assert.AreEqual(cycleId, ex.CyclicGraphId);
        }

        // ── Valid acyclic graph ────────────────────────────────────────────────

        [Test]
        public void Cycle_Valid_AcyclicGraph_CompletesWithoutException()
        {
            var child  = Track(ScriptableObject.CreateInstance<BaseGraph>());
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());

            child.AddNode(new StartNodeData { Id = "c-start", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(new EndNodeData   { Id = "c-end",   NodeType = EndNodeData.NodeTypeId });
            child.AddEdge(new BaseEdgeData  { Id = "ce1", FromNodeId = "c-start", ToNodeId = "c-end" });
            child.EntryNodeId = "c-start";

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

            Assert.DoesNotThrow(() =>
                runner.Start(parent, new BaseContext(), new NodeExecutorRegistry()));
            Assert.AreEqual(RunnerState.Ended, runner.State);
        }

        [Test]
        public void Cycle_Valid_NestedSubGraphs_NoException()
        {
            // grandchild → child → parent, no cycles
            var grandchild = Track(ScriptableObject.CreateInstance<BaseGraph>());
            grandchild.AddNode(new StartNodeData { Id = "gc-start", NodeType = StartNodeData.NodeTypeId });
            grandchild.AddNode(new EndNodeData   { Id = "gc-end",   NodeType = EndNodeData.NodeTypeId });
            grandchild.AddEdge(new BaseEdgeData  { Id = "gce1", FromNodeId = "gc-start", ToNodeId = "gc-end" });
            grandchild.EntryNodeId = "gc-start";

            var child = Track(ScriptableObject.CreateInstance<BaseGraph>());
            child.AddNode(new StartNodeData    { Id = "c-start", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(new SubGraphNodeData { Id = "c-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = grandchild, InheritParentContext = true });
            child.AddNode(new EndNodeData      { Id = "c-end",   NodeType = EndNodeData.NodeTypeId });
            child.AddEdge(new BaseEdgeData { Id = "ce1", FromNodeId = "c-start", ToNodeId = "c-sub" });
            child.AddEdge(new BaseEdgeData { Id = "ce2", FromNodeId = "c-sub",   ToNodeId = "c-end" });
            child.EntryNodeId = "c-start";

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

            Assert.DoesNotThrow(() =>
                runner.Start(parent, new BaseContext(), new NodeExecutorRegistry()));
            Assert.AreEqual(RunnerState.Ended, runner.State);
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
