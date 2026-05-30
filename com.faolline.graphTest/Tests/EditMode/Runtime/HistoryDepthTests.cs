using NUnit.Framework;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphTest.Tests
{
    /// <summary>
    /// Verifies that the runner caps its GoBack history at the graph's <see cref="BaseGraph.HistoryDepth"/>
    /// (0 = unlimited) on a long linear chain.
    /// </summary>
    [TestFixture]
    public class HistoryDepthTests
    {
        private static TestGraph BuildChain(int steps, int historyDepth)
        {
            var g = ScriptableObject.CreateInstance<TestGraph>();
            g.HistoryDepth = historyDepth;

            g.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
            g.EntryNodeId = "s";

            string prev = "s";
            for (int i = 1; i <= steps; i++)
            {
                var id = $"n{i}";
                g.AddNode(new TestStatementNodeData { Id = id, NodeType = TestStatementNodeData.NodeTypeId, Label = $"Step {i}" });
                g.AddEdge(new BaseEdgeData { Id = $"e{i}", FromNodeId = prev, ToNodeId = id, PortName = "out" });
                prev = id;
            }
            g.AddNode(new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData { Id = "eEnd", FromNodeId = prev, ToNodeId = "end", PortName = "out" });
            return g;
        }

        private static void RunToEnd(BaseRunner runner)
        {
            int guard = 0;
            while (runner.State == RunnerState.NodeReady && guard++ < 2000) runner.Proceed();
        }

        [Test]
        public void GoBack_CappedByHistoryDepth_CannotRewindToStart()
        {
            var g = BuildChain(30, 5);
            try
            {
                var runner = new BaseRunner();
                runner.Start(g, new BaseContext(), new NodeExecutorRegistry());
                RunToEnd(runner);

                for (int i = 0; i < 100; i++) runner.GoBack(); // exhaust history

                Assert.IsNotNull(runner.CurrentNode);
                Assert.AreNotEqual("s", runner.CurrentNode.Id,
                    "HistoryDepth=5 must prevent rewinding a 30-step run back to the start (older snapshots dropped)");
            }
            finally { Object.DestroyImmediate(g); }
        }

        [Test]
        public void GoBack_UnlimitedHistory_RewindsToStart()
        {
            var g = BuildChain(30, 0); // 0 = unlimited
            try
            {
                var runner = new BaseRunner();
                runner.Start(g, new BaseContext(), new NodeExecutorRegistry());
                RunToEnd(runner);

                string last = null;
                for (int i = 0; i < 500; i++)
                {
                    runner.GoBack();
                    var id = runner.CurrentNode?.Id;
                    if (id == last) break; // history exhausted — node stops changing
                    last = id;
                }

                Assert.AreEqual("s", runner.CurrentNode?.Id,
                    "HistoryDepth=0 (unlimited) must allow rewinding all the way back to the start");
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
