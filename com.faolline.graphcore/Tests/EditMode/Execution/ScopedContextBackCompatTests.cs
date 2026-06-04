using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Proves the scoped (global+local) behaviour is a strictly additive third option: the existing
    /// inherit / fresh-blank sub-graph behaviours open no local context and are unchanged (US3).
    /// </summary>
    public class ScopedContextBackCompatTests
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

        private BaseGraph BuildParent(BaseGraph child, bool inherit, bool opensScope)
        {
            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());
            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, InheritParentContext = inherit, OpensScope = opensScope });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";
            return parent;
        }

        private static void AutoDrive(BaseRunner runner)
            => runner.OnNodeCompleted += _ => { if (runner.State == RunnerState.NodeReady) runner.Proceed(); };

        [Test]
        public void InheritContext_OpensNoLocal_AndWriteLeaksAsBefore()   // US3.1
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildParent(child, inherit: true, opensScope: false);

            bool localOpenInside = true;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start") { localOpenInside = c.HasLocalContext; c.Set<int>("v", 99); }
            }));

            var ctx = new BaseContext();
            var runner = new BaseRunner();
            AutoDrive(runner);
            runner.Start(parent, ctx, registry);

            Assert.IsFalse(localOpenInside, "Inherit must not open a local context.");
            Assert.AreEqual(99, ctx.Get<int>("v"), "Inherited write must remain visible in the parent (unchanged).");
        }

        [Test]
        public void FreshContext_OpensNoLocal_AndIsIsolatedAsBefore()     // US3.2
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildParent(child, inherit: false, opensScope: false);

            bool localOpenInside = true;
            bool sawParentVal    = true;
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start")
                {
                    localOpenInside = c.HasLocalContext;
                    sawParentVal    = c.Has("parentVal");
                    c.Set<int>("childVal", 1);
                }
            }));

            var ctx = new BaseContext();
            ctx.Set<int>("parentVal", 42);
            var runner = new BaseRunner();
            AutoDrive(runner);
            runner.Start(parent, ctx, registry);

            Assert.IsFalse(localOpenInside, "Fresh context must not open a local context.");
            Assert.IsFalse(sawParentVal, "Fresh context must not see parent values (unchanged).");
            Assert.IsFalse(ctx.Has("childVal"), "Fresh context write must not leak to parent (unchanged).");
        }

        [Test]
        public void OpensScopeFalse_NeverOpensLocalContext()             // US3.3
        {
            var child  = Track(BuildLinearGraph("c-start", "c-end"));
            var parent = BuildParent(child, inherit: true, opensScope: false);

            var registry = new NodeExecutorRegistry();
            var runner   = new BaseRunner();
            AutoDrive(runner);

            var ctx = new BaseContext();
            runner.Start(parent, ctx, registry);

            Assert.IsFalse(ctx.HasLocalContext, "No overlay may remain after a non-scoped run.");
            Assert.AreEqual(RunnerState.Ended, runner.State);
        }

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
