using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// A <see cref="GraphLinkNodeData"/> is a NON-executing documentary reference: off the path it is never
    /// reached, and on the path the runner passes straight through it (no pause, no actions, not even
    /// OnNodeEntered) — so a run is unaffected by its presence.
    /// </summary>
    public class GraphLinkRunnerPassThroughTests
    {
        private static List<string> RunCollectingEntered(BaseGraph graph)
        {
            var entered = new List<string>();
            var runner = new BaseRunner();
            runner.OnNodeEntered += n => entered.Add(n.Id);
            runner.Start(graph, new BaseContext(), new NodeExecutorRegistry());
            int guard = 0;
            while (runner.State == RunnerState.NodeReady && guard++ < 100) runner.Proceed();
            Assert.AreEqual(RunnerState.Ended, runner.State, "the run reaches Ended");
            return entered;
        }

        private static BaseGraph StartEnd()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            g.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new EndNodeData   { Id = "e", NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        [Test]
        public void GraphLinkOffPath_RunIsIdentical_AndLinkNeverEntered()
        {
            var with = StartEnd();
            with.AddNode(new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId });  // not wired
            var without = StartEnd();
            try
            {
                var seqWith = RunCollectingEntered(with);
                var seqWithout = RunCollectingEntered(without);
                CollectionAssert.AreEqual(seqWithout, seqWith, "an off-path GraphLink does not change the run.");
                CollectionAssert.DoesNotContain(seqWith, "link", "an off-path GraphLink is never entered.");
            }
            finally { Object.DestroyImmediate(with); Object.DestroyImmediate(without); }
        }

        [Test]
        public void GraphLinkOnPath_PassesThrough_ReachesEnded_AndIsTransparent()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                g.AddNode(new StartNodeData    { Id = "s",    NodeType = StartNodeData.NodeTypeId });
                g.AddNode(new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId });
                g.AddNode(new EndNodeData      { Id = "e",    NodeType = EndNodeData.NodeTypeId });
                g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s",    ToNodeId = "link", PortName = "out" });
                g.AddEdge(new BaseEdgeData { Id = "e2", FromNodeId = "link", ToNodeId = "e",    PortName = "out" });
                g.EntryNodeId = "s";

                var seq = RunCollectingEntered(g);   // also asserts Ended
                CollectionAssert.AreEqual(new[] { "s", "e" }, seq,
                    "a GraphLink on the path is transparent — passed through, never entered (no OnNodeEntered).");
            }
            finally { Object.DestroyImmediate(g); }
        }

        [Test]
        public void GraphLinkOnPath_NoOutgoingEdge_Ends()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                g.AddNode(new StartNodeData    { Id = "s",    NodeType = StartNodeData.NodeTypeId });
                g.AddNode(new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId });  // dead-end
                g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "link", PortName = "out" });
                g.EntryNodeId = "s";

                var runner = new BaseRunner();
                runner.Start(g, new BaseContext(), new NodeExecutorRegistry());
                int guard = 0;
                Assert.DoesNotThrow(() => { while (runner.State == RunnerState.NodeReady && guard++ < 100) runner.Proceed(); });
                Assert.AreEqual(RunnerState.Ended, runner.State, "a dead-end GraphLink terminates like any dead-end.");
            }
            finally { Object.DestroyImmediate(g); }
        }
    }
}
