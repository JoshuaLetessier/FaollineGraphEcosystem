using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Runner-level acceptance tests for scope-opening sub-graphs: US1 (local isolation) and
    /// US2 (fall-through reads + durable global writes), driven headlessly via BaseRunner.
    /// </summary>
    public class ScopedSubGraphRunnerTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        private static BaseGraph BuildLinearGraph(string entryId, string endId)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            g.AddNode(new StartNodeData { Id = entryId, NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new EndNodeData   { Id = endId,   NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData  { Id = $"e-{entryId}-{endId}", FromNodeId = entryId, ToNodeId = endId });
            g.EntryNodeId = entryId;
            return g;
        }

        /// <summary>Parent: p-start → sub(child, OpensScope) → p-end.</summary>
        private BaseGraph BuildScopedParent(BaseGraph child)
        {
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());
            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, OpensScope = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";
            return parent;
        }

        private static BaseRunner AutoRunner(NodeExecutorRegistry registry, out BaseRunner runner)
        {
            runner = new BaseRunner();
            var r = runner;
            runner.OnNodeCompleted += _ => { if (r.State == RunnerState.NodeReady) r.Proceed(); };
            return runner;
        }

        // ── US1: isolation ────────────────────────────────────────────────────────

        [Test]
        public void US1_ScopedSubGraph_Temporaries_DiscardedOnEnd()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildScopedParent(child);

            bool hadLocalInside = false;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start") { c.Set<int>("Tmp", 11); hadLocalInside = c.HasLocalContext; }
            }));

            var ctx = new BaseContext();
            AutoRunner(registry, out var runner);
            runner.Start(parent, ctx, registry);

            Assert.IsTrue(hadLocalInside, "A local context must be open inside the scoped sub-graph.");
            Assert.AreEqual(RunnerState.Ended, runner.State);
            Assert.IsFalse(ctx.Has("Tmp"), "Scoped temporaries must be gone after the sub-graph ends.");
            Assert.IsFalse(ctx.HasLocalContext, "Local context must be closed after the sub-graph ends.");
        }

        [Test]
        public void US1_SequentialScopedSubGraphs_EachGetFreshLocal()
        {
            var child1 = Track(BuildLinearGraph("c1-start", "c1-end"));
            var child2 = Track(BuildLinearGraph("c2-start", "c2-end"));

            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());
            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub1",  NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child1, OpensScope = true });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub2",  NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child2, OpensScope = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub1" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub1",  ToNodeId = "p-sub2" });
            parent.AddEdge(new BaseEdgeData { Id = "pe3", FromNodeId = "p-sub2",  ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            bool secondSawFirstsTmp = true;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c1-start") c.Set<int>("Tmp", 11);
                if (n.Id == "c2-start") secondSawFirstsTmp = c.Has("Tmp");
            }));

            AutoRunner(registry, out var runner);
            runner.Start(parent, new BaseContext(), registry);

            Assert.IsFalse(secondSawFirstsTmp,
                "A second scoped sub-graph must start with a fresh, empty local context.");
        }

        // ── US2: fall-through reads + durable global writes ─────────────────────────

        [Test]
        public void US2_ScopedSubGraph_ReadsHostGlobal_ViaFallthrough()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildScopedParent(child);

            int seenGold = -1;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start") seenGold = c.Get<int>("Gold");
            }));

            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);             // host global
            AutoRunner(registry, out var runner);
            runner.Start(parent, ctx, registry);

            Assert.AreEqual(7, seenGold, "Scoped sub-graph must read host global via fall-through.");
        }

        [Test]
        public void US2_ScopedSubGraph_DurableGlobalWrite_Persists()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildScopedParent(child);

            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start") c.Set<bool>("BossDefeated", true);
            }));

            var ctx = new BaseContext();
            ctx.Set<bool>("BossDefeated", false);   // global-resident before the scope opens
            AutoRunner(registry, out var runner);
            runner.Start(parent, ctx, registry);

            Assert.IsTrue(ctx.Get<bool>("BossDefeated"),
                "A write to a global-resident key from inside a scope must persist past the scope.");
        }

        [Test]
        public void US2_ScopedSubGraph_UndeclaredScratch_Discarded()
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildScopedParent(child);

            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start") c.Set<int>("Scratch", 3);   // never declared in global
            }));

            var ctx = new BaseContext();
            AutoRunner(registry, out var runner);
            runner.Start(parent, ctx, registry);

            Assert.IsFalse(ctx.Has("Scratch"), "Undeclared scratch must default to local and be discarded.");
        }

        // ── Inner stub ──────────────────────────────────────────────────────────────

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
