using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Step-back / history fidelity across a scope boundary (FR-010 / SC-005): the local-context
    /// overlay must be captured in snapshots and restored exactly — no discarded local reappears and
    /// no closed local lingers.
    /// </summary>
    public class ScopedContextHistoryTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        // ── DeepClone captures the overlay (the mechanism history relies on) ────────

        [Test]
        public void DeepClone_CapturesLocalOverlay_AndIsolatesIt()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);
            ctx.BeginLocalContext();
            ctx.Set<int>("Tmp", 5);

            var snapshot = ctx.DeepClone();

            // Mutate the original after snapshot — the snapshot must be unaffected.
            ctx.Set<int>("Tmp", 99);
            ctx.EndLocalContext();

            Assert.IsTrue(snapshot.HasLocalContext, "Snapshot must preserve the open local context.");
            Assert.AreEqual(5, snapshot.Get<int>("Tmp"), "Snapshot local value must be isolated from later writes.");
            Assert.AreEqual(7, snapshot.Get<int>("Gold"));
        }

        [Test]
        public void DeepClone_NoLocal_CloneHasNoLocal()
        {
            var ctx = new BaseContext();
            ctx.Set<int>("Gold", 7);
            var clone = ctx.DeepClone();
            Assert.IsFalse(clone.HasLocalContext);
        }

        // ── Runner step-back across the scope boundary ──────────────────────────────

        [Test]
        public void StepBack_AcrossScopeBoundary_RestoresOverlayState()
        {
            // child: c-start → c-end ; parent: p-start → sub(child, OpensScope) → p-end
            var child = Track(ScriptableObject.CreateInstance<BaseGraph>());
            child.AddNode(new StartNodeData { Id = "c-start", NodeType = StartNodeData.NodeTypeId });
            child.AddNode(new EndNodeData   { Id = "c-end",   NodeType = EndNodeData.NodeTypeId });
            child.AddEdge(new BaseEdgeData  { Id = "ce", FromNodeId = "c-start", ToNodeId = "c-end" });
            child.EntryNodeId = "c-start";

            var parent = Track(ScriptableObject.CreateInstance<BaseGraph>());
            parent.AddNode(new StartNodeData    { Id = "p-start", NodeType = StartNodeData.NodeTypeId });
            parent.AddNode(new SubGraphNodeData { Id = "p-sub",   NodeType = SubGraphNodeData.NodeTypeId,
                TargetGraph = child, OpensScope = true });
            parent.AddNode(new EndNodeData      { Id = "p-end",   NodeType = EndNodeData.NodeTypeId });
            parent.AddEdge(new BaseEdgeData { Id = "pe1", FromNodeId = "p-start", ToNodeId = "p-sub" });
            parent.AddEdge(new BaseEdgeData { Id = "pe2", FromNodeId = "p-sub",   ToNodeId = "p-end" });
            parent.EntryNodeId = "p-start";

            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId, (n, c) =>
            {
                if (n.Id == "c-start") c.Set<int>("Tmp", 5);   // local scratch inside the scope
            }));

            var ctx = new BaseContext();
            var runner = new BaseRunner();
            runner.Start(parent, ctx, registry);   // at p-start (NodeReady)

            runner.Proceed();                      // snapshot p-start (pre-scope); enter p-sub
            runner.Proceed();                      // snapshot p-sub (pre-scope); open scope; enter c-start
            Assert.IsTrue(ctx.HasLocalContext, "sanity: inside the scope");
            runner.Proceed();                      // snapshot c-start (DURING scope: overlay+Tmp); enter c-end

            // Step back to the c-start snapshot — taken while the scope was open.
            runner.GoBack();
            Assert.IsTrue(ctx.HasLocalContext, "Restoring a during-scope snapshot must re-open the local context.");
            Assert.IsTrue(ctx.Has("Tmp"));
            Assert.AreEqual(5, ctx.Get<int>("Tmp"));

            // Step back again to the p-sub snapshot — taken before the scope opened.
            runner.GoBack();
            Assert.IsFalse(ctx.HasLocalContext, "Restoring a pre-scope snapshot must show no local context.");
            Assert.IsFalse(ctx.Has("Tmp"), "A discarded local value must not reappear before the scope existed.");
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
