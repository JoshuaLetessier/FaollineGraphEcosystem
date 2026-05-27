using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    public class BaseRunnerHistoryTests
    {
        private readonly List<BaseGraph> _graphs = new List<BaseGraph>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _graphs) Object.DestroyImmediate(g);
            _graphs.Clear();
        }

        private BaseGraph Track(BaseGraph g) { _graphs.Add(g); return g; }

        // ── GoBack ─────────────────────────────────────────────────────────────

        [Test]
        public void GoBack_RestoresPreviousNode()
        {
            var graph = Track(BuildChainGraph(3)); // n0 → n1 → n2
            var ctx   = new BaseContext();
            var runner = new BaseRunner();

            var lastEntered = new List<string>();
            runner.OnNodeEntered += n => lastEntered.Add(n.Id);

            // Advance to n1
            runner.OnNodeCompleted += _ => { };
            runner.Start(graph, ctx, new NodeExecutorRegistry()); // at n0
            runner.Proceed(); // at n1

            runner.GoBack(); // should restore to n0

            Assert.AreEqual("n0", lastEntered[lastEntered.Count - 1]);
        }

        [Test]
        public void GoBack_RestoresContextValues()
        {
            var graph = Track(BuildChainGraph(2)); // n0 → n1
            var ctx   = new BaseContext();
            ctx.Set<int>("score", 10);

            var registry = new NodeExecutorRegistry();
            registry.Register(new LambdaExecutor(StartNodeData.NodeTypeId,
                (n, c) => { if (n.Id == "n0") c.Set<int>("score", 99); }));

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => { };
            runner.Start(graph, ctx, registry); // enters n0: score → 99
            runner.Proceed();                   // exits n0 (snapshot), enters n1

            runner.GoBack(); // restore to snapshot before n1

            Assert.AreEqual(99, ctx.Get<int>("score"),
                "Context after GoBack should reflect state at the snapshot point.");
        }

        [Test]
        public void GoBack_EmptyHistory_IsNoOp()
        {
            var graph = Track(BuildChainGraph(2));
            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => { };
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());

            Assert.DoesNotThrow(() => runner.GoBack());
            Assert.AreEqual(RunnerState.NodeReady, runner.State);
        }

        // ── GoBackToCheckpoint ─────────────────────────────────────────────────

        [Test]
        public void GoBackToCheckpoint_RestoresNearestCheckpointNode()
        {
            // n0(checkpoint) → n1 → n2
            var graph = Track(BuildChainGraph(3));
            var nodes = new List<BaseNodeData>(graph.Nodes);
            nodes[0].IsCheckpoint = true; // n0 is a checkpoint

            var visited = new List<string>();
            var runner = new BaseRunner();
            runner.OnNodeEntered += n => visited.Add(n.Id);
            runner.OnNodeCompleted += _ => { };

            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0
            runner.Proceed();  // n1
            runner.Proceed();  // n2

            runner.GoBackToCheckpoint(); // should restore to n0

            Assert.AreEqual("n0", visited[visited.Count - 1]);
        }

        [Test]
        public void GoBackToCheckpoint_NoCheckpointInHistory_IsNoOp()
        {
            var graph = Track(BuildChainGraph(3)); // no checkpoints
            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => { };

            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            runner.Proceed();

            Assert.DoesNotThrow(() => runner.GoBackToCheckpoint());
        }

        // ── History cap ────────────────────────────────────────────────────────

        [Test]
        public void History_CappedByHistoryDepth()
        {
            var graph = Track(BuildChainGraph(6)); // n0…n5
            graph.HistoryDepth = 3;

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => { };
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry()); // n0

            // Advance 4 steps: snapshots for n0, n1, n2, n3 → capped to last 3
            runner.Proceed(); // n1
            runner.Proceed(); // n2
            runner.Proceed(); // n3
            runner.Proceed(); // n4

            // GoBack 3 times should reach n2 (not n0, which was evicted)
            runner.GoBack(); // back to n3
            runner.GoBack(); // back to n2
            runner.GoBack(); // back to n1

            // A 4th GoBack should be a no-op (history capped to 3)
            var state = runner.State;
            runner.GoBack();
            Assert.AreEqual(state, runner.State, "No further GoBack should be possible.");
        }

        [Test]
        public void History_DepthZero_Unlimited()
        {
            var graph = Track(BuildChainGraph(6));
            graph.HistoryDepth = 0; // unlimited

            var runner = new BaseRunner();
            runner.OnNodeCompleted += _ => { };
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());

            for (int i = 0; i < 5; i++) runner.Proceed();

            // All 5 advances should be undoable
            for (int i = 0; i < 5; i++)
                Assert.DoesNotThrow(() => runner.GoBack());
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// Builds a linear chain: n0 → n1 → … → n(count-1). Last node is EndNode.
        private BaseGraph BuildChainGraph(int count)
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            for (int i = 0; i < count; i++)
            {
                BaseNodeData node = (i == 0)
                    ? (BaseNodeData)new StartNodeData     { Id = $"n{i}", NodeType = StartNodeData.NodeTypeId }
                    : (i == count - 1)
                        ? new EndNodeData      { Id = $"n{i}", NodeType = EndNodeData.NodeTypeId }
                        : new StatementNodeData { Id = $"n{i}", NodeType = StatementNodeData.NodeTypeId };
                g.AddNode(node);
                if (i > 0)
                    g.AddEdge(new BaseEdgeData
                    {
                        Id = $"e{i-1}{i}",
                        FromNodeId = $"n{i-1}",
                        ToNodeId   = $"n{i}"
                    });
            }
            g.EntryNodeId = "n0";
            return g;
        }

        private class LambdaExecutor : INodeExecutor
        {
            private readonly System.Action<BaseNodeData, BaseContext> _exec;
            public string NodeType { get; }
            public LambdaExecutor(string type, System.Action<BaseNodeData, BaseContext> exec)
            { NodeType = type; _exec = exec; }
            public void Execute(BaseNodeData node, BaseContext context) => _exec(node, context);
        }
    }
}
