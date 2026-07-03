using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class HistoryTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) UnityEngine.Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        // ── GoBack ─────────────────────────────────────────────────────────────

        [Test]
        public void GoBack_RestoresPreviousNode()
        {
            var graph  = Track(BuildChainGraph(3)); // n0 → n1 → n2
            var runner = new BaseRunner();
            var entered = new List<string>();
            runner.OnNodeEntered += n => entered.Add(n.Id);

            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // at n0
            runner.Proceed();                                                    // at n1

            runner.GoBack(); // restore to n0

            Assert.AreEqual("n0", entered[entered.Count - 1]);
        }

        [Test]
        public void GoBack_RestoresContextValues()
        {
            var graph    = Track(BuildChainGraph(2)); // n0 → n1
            var ctx      = new BaseContext();
            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) => { if (n.Id == "n0") c.Set<int>("score", 99); }));

            var runner = new BaseRunner();
            runner.Start(graph, ctx, registry); // enters n0, score → 99
            runner.Proceed();                   // exits n0 (snapshot), enters n1

            runner.GoBack(); // restore snapshot taken after n0's exit

            Assert.AreEqual(99, ctx.Get<int>("score"));
        }

        [Test]
        public void GoBack_EmptyHistory_IsNoOp()
        {
            var graph  = Track(BuildChainGraph(2));
            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());

            Assert.DoesNotThrow(() => runner.GoBack());
            Assert.AreEqual(RunnerState.NodeReady, runner.State);
        }

        [Test]
        public void GoBack_TruncatesHistoryFromRestoredEntry()
        {
            var graph  = Track(BuildChainGraph(4)); // n0 → n1 → n2 → n3
            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0
            runner.Proceed(); // n1  (snapshot: [n0])
            runner.Proceed(); // n2  (snapshot: [n0, n1])

            runner.GoBack();  // restore n1, truncate history to [n0]
            runner.GoBack();  // restore n0, truncate history to []

            // A third GoBack must be a no-op — history is empty
            var stateBefore = runner.State;
            runner.GoBack();
            Assert.AreEqual(stateBefore, runner.State);
        }

        // ── GoBackToCheckpoint ─────────────────────────────────────────────────

        [Test]
        public void GoBackToCheckpoint_RestoresNearestCheckpointNode()
        {
            var graph = Track(BuildChainGraph(4)); // n0 → n1 → n2 → n3
            // Mark n0 as checkpoint
            var nodes = new List<BaseNodeData>(graph.Nodes);
            nodes[0].IsCheckpoint = true;

            var runner  = new BaseRunner();
            var entered = new List<string>();
            runner.OnNodeEntered += n => entered.Add(n.Id);

            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0
            runner.Proceed(); // n1
            runner.Proceed(); // n2

            runner.GoBackToCheckpoint(); // must restore to n0 (nearest checkpoint)

            Assert.AreEqual("n0", entered[entered.Count - 1]);
        }

        [Test]
        public void GoBackToCheckpoint_NoCheckpointInHistory_IsNoOp()
        {
            var graph  = Track(BuildChainGraph(3)); // no checkpoints
            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();

            Assert.DoesNotThrow(() => runner.GoBackToCheckpoint());
        }

        [Test]
        public void GoBackToCheckpoint_MultipleCheckpoints_RestoresMostRecent()
        {
            var graph = Track(BuildChainGraph(4)); // n0 → n1 → n2 → n3
            var nodes = new List<BaseNodeData>(graph.Nodes);
            nodes[0].IsCheckpoint = true; // n0 checkpoint
            nodes[1].IsCheckpoint = true; // n1 checkpoint

            var runner  = new BaseRunner();
            var entered = new List<string>();
            runner.OnNodeEntered += n => entered.Add(n.Id);

            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0
            runner.Proceed(); // n1  (snap n0)
            runner.Proceed(); // n2  (snap n1)
            runner.Proceed(); // n3  (snap n2)

            runner.GoBackToCheckpoint(); // n1 is more recent than n0 → restore n1

            Assert.AreEqual("n1", entered[entered.Count - 1]);
        }

        // ── HistoryDepth cap ───────────────────────────────────────────────────

        [Test]
        public void History_CappedByHistoryDepth_EvictsOldestEntry()
        {
            var graph = Track(BuildChainGraph(6)); // n0…n5
            graph.HistoryDepth = 3;

            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0

            // 4 advances produce 4 snapshots; capped at 3 → oldest evicted
            runner.Proceed(); // n1, snap n0
            runner.Proceed(); // n2, snap n1
            runner.Proceed(); // n3, snap n2 → now 3 entries: [n0, n1, n2]; next evicts n0
            runner.Proceed(); // n4, snap n3 → entries: [n1, n2, n3]

            // 3 GoBack calls should succeed (n3 → n2 → n1)
            runner.GoBack();
            runner.GoBack();
            runner.GoBack();

            // A 4th GoBack is a no-op — n0 was evicted
            var stateBefore = runner.State;
            runner.GoBack();
            Assert.AreEqual(stateBefore, runner.State);
        }

        [Test]
        public void History_CappedByHistoryDepth_ExtraGoBackIsNoOp()
        {
            var graph = Track(BuildChainGraph(4));
            graph.HistoryDepth = 2;

            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0
            runner.Proceed(); // n1
            runner.Proceed(); // n2
            runner.Proceed(); // n3 → history capped: only [n1, n2] remain

            runner.GoBack(); // restore n2
            runner.GoBack(); // restore n1

            // History now empty — extra GoBack is no-op
            Assert.DoesNotThrow(() => runner.GoBack());
        }

        // ── Unlimited depth ────────────────────────────────────────────────────

        [Test]
        public void History_DepthZero_AllAdvancesUndoable()
        {
            var graph = Track(BuildChainGraph(7)); // n0…n6
            graph.HistoryDepth = 0; // unlimited

            var runner = new BaseRunner();
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());

            for (int i = 0; i < 5; i++) runner.Proceed();

            // All 5 advances must be undoable
            for (int i = 0; i < 5; i++)
                Assert.DoesNotThrow(() => runner.GoBack());
        }

        // ── Saturation warning ─────────────────────────────────────────────────

        private static int CountSaturationWarnings(System.Action body)
        {
            int warnings = 0;
            Application.LogCallback handler = (msg, stack, type) =>
            { if (type == LogType.Warning && msg.Contains("History saturated")) warnings++; };
            Application.logMessageReceived += handler;
            try { body(); }
            finally { Application.logMessageReceived -= handler; }
            return warnings;
        }

        [Test]
        public void History_Saturation_WarnsExactlyOnce()
        {
            var graph = Track(BuildChainGraph(8)); // n0…n7
            graph.HistoryDepth = 2;

            int warnings = CountSaturationWarnings(() =>
            {
                var runner = new BaseRunner();
                runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
                for (int i = 0; i < 5; i++) runner.Proceed();   // several trims past the cap
            });

            Assert.AreEqual(1, warnings, "history saturation warns once per run, not on every trim");
        }

        [Test]
        public void History_Saturation_Unlimited_NeverWarns()
        {
            var graph = Track(BuildChainGraph(8));
            graph.HistoryDepth = 0; // unlimited → never trims → never warns

            int warnings = CountSaturationWarnings(() =>
            {
                var runner = new BaseRunner();
                runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
                for (int i = 0; i < 5; i++) runner.Proceed();
            });

            Assert.AreEqual(0, warnings);
        }

        [Test]
        public void History_Saturation_WarningReArmsOnFreshStart()
        {
            var graph = Track(BuildChainGraph(8));
            graph.HistoryDepth = 2;
            var runner = new BaseRunner();

            int warnings = CountSaturationWarnings(() =>
            {
                runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
                for (int i = 0; i < 5; i++) runner.Proceed();
                runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());   // new run re-arms the one-shot
                for (int i = 0; i < 5; i++) runner.Proceed();
            });

            Assert.AreEqual(2, warnings, "the one-shot re-arms on a fresh Start");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a linear chain: n0 → n1 → … → n(count-1).
        /// n0 = StartNodeData, n(last) = EndNodeData, all others = StatementNodeData.
        /// </summary>
        private static BaseGraph BuildChainGraph(int count)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            for (int i = 0; i < count; i++)
            {
                BaseNodeData node = i == 0
                    ? (BaseNodeData)new StartNodeData     { Id = $"n{i}", NodeType = StartNodeData.NodeTypeId }
                    : i == count - 1
                        ? new EndNodeData      { Id = $"n{i}", NodeType = EndNodeData.NodeTypeId }
                        : new StatementNodeData{ Id = $"n{i}", NodeType = StatementNodeData.NodeTypeId };
                g.AddNode(node);
                if (i > 0)
                    g.AddEdge(new BaseEdgeData
                    {
                        Id         = $"e{i - 1}{i}",
                        FromNodeId = $"n{i - 1}",
                        ToNodeId   = $"n{i}"
                    });
            }
            g.EntryNodeId = "n0";
            return g;
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
