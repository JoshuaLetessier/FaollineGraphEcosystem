using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using Faolline.GraphCore;
using Faolline.StarterGraph.Editor;

namespace Faolline.StarterGraph.Tests
{
    /// <summary>US3 — reload/no-data-loss/reconnect, runtime cycle, nested sub-graphs, sample generation.</summary>
    [TestFixture]
    public class StarterRobustnessTests
    {
        private static void RunToEnd(BaseRunner r) { int g = 0; while (r.State == RunnerState.NodeReady && g++ < 300) r.Proceed(); }

        // ── Reload / no data loss / reconnect ──────────────────────────────────
        [Test]
        public void Reload_PreservesData_AndReconnectsEdges()
        {
            var graph = ScriptableObject.CreateInstance<StarterGraph>();
            var view = new StarterGraphView();
            try
            {
                graph.AddNode(new StartNodeData            { Id = "s", NodeType = StartNodeData.NodeTypeId });
                graph.AddNode(new StarterStatementNodeData { Id = "n", NodeType = StarterStatementNodeData.NodeTypeId });
                graph.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "n", PortName = "out" });

                view.LoadGraph(graph);
                view.LoadGraph(graph); // reload must NOT delete data via the change callback

                Assert.AreEqual(2, graph.Nodes.Count, "Reload must not delete nodes");
                Assert.AreEqual(1, graph.Edges.Count, "Reload must not delete edges");

                var edges = view.edges.ToList();
                Assert.AreEqual(1, edges.Count);
                Assert.IsNotNull(edges[0].output, "Reloaded edge must reconnect to its source port");
                Assert.IsNotNull(edges[0].input,  "Reloaded edge must reconnect to its target port");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        [Test]
        public void RemoveChoice_KeepsSurvivingChoiceEdgeConnected()
        {
            var graph = ScriptableObject.CreateInstance<StarterGraph>();
            var view = new StarterGraphView();
            try
            {
                var choice = new ChoiceNodeData { Id = "c", NodeType = ChoiceNodeData.NodeTypeId };
                var ca = new StarterChoice { Id = "a", Label = "A" };
                var cb = new StarterChoice { Id = "b", Label = "B" };
                choice.Choices.Add(ca); choice.Choices.Add(cb);
                graph.AddNode(choice);
                graph.AddNode(new StarterStatementNodeData { Id = "ta", NodeType = StarterStatementNodeData.NodeTypeId });
                graph.AddNode(new StarterStatementNodeData { Id = "tb", NodeType = StarterStatementNodeData.NodeTypeId });
                graph.AddEdge(new BaseEdgeData { Id = "ea", FromNodeId = "c", ToNodeId = "ta", PortName = "a" });
                graph.AddEdge(new BaseEdgeData { Id = "eb", FromNodeId = "c", ToNodeId = "tb", PortName = "b" });
                view.LoadGraph(graph);

                var inspector = new StarterNodeInspectorView();
                inspector.SetGraph(graph);
                inspector.SetGraphView(view);
                inspector.RemoveChoice(choice, ca);

                var edges = view.edges.ToList();
                Assert.AreEqual(1, edges.Count);
                Assert.AreEqual("b", edges[0].output?.portName, "Surviving choice edge must stay connected after rebuild");
            }
            finally { Object.DestroyImmediate(graph); }
        }

        // ── SubGraph runtime: nesting + cycle ──────────────────────────────────
        private static void BuildSubOnly(StarterGraph g, string p, StarterGraph target)
        {
            g.AddNode(new StartNodeData    { Id = p + "s",  NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new SubGraphNodeData { Id = p + "sg", NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = target });
            g.AddNode(new EndNodeData      { Id = p + "e",  NodeType = EndNodeData.NodeTypeId });
            g.EntryNodeId = p + "s";
            g.AddEdge(new BaseEdgeData { Id = p + "1", FromNodeId = p + "s",  ToNodeId = p + "sg", PortName = "out" });
            g.AddEdge(new BaseEdgeData { Id = p + "2", FromNodeId = p + "sg", ToNodeId = p + "e",  PortName = "out" });
        }

        [Test]
        public void SubGraph_NestedTwoLevels_ReachesDeepestNode()
        {
            var l3 = ScriptableObject.CreateInstance<StarterGraph>();
            l3.AddNode(new StartNodeData            { Id = "l3s",  NodeType = StartNodeData.NodeTypeId });
            l3.AddNode(new StarterStatementNodeData { Id = "deep", NodeType = StarterStatementNodeData.NodeTypeId });
            l3.AddNode(new EndNodeData              { Id = "l3e",  NodeType = EndNodeData.NodeTypeId });
            l3.EntryNodeId = "l3s";
            l3.AddEdge(new BaseEdgeData { Id = "l3a", FromNodeId = "l3s",  ToNodeId = "deep", PortName = "out" });
            l3.AddEdge(new BaseEdgeData { Id = "l3b", FromNodeId = "deep", ToNodeId = "l3e",  PortName = "out" });
            var l2 = ScriptableObject.CreateInstance<StarterGraph>(); BuildSubOnly(l2, "l2", l3);
            var l1 = ScriptableObject.CreateInstance<StarterGraph>(); BuildSubOnly(l1, "l1", l2);
            try
            {
                var visited = new List<string>();
                var runner = new BaseRunner();
                runner.OnNodeEntered += n => visited.Add(n.Id);
                runner.Start(l1, new StarterContext(), new NodeExecutorRegistry());
                RunToEnd(runner);
                Assert.AreEqual(RunnerState.Ended, runner.State);
                Assert.Contains("deep", visited, "Two levels of sub-graph nesting must reach the deepest node");
            }
            finally { Object.DestroyImmediate(l1); Object.DestroyImmediate(l2); Object.DestroyImmediate(l3); }
        }

        [Test]
        public void SubGraph_RuntimeCycle_ThrowsGraphCycleException()
        {
            var a = ScriptableObject.CreateInstance<StarterGraph>();
            var b = ScriptableObject.CreateInstance<StarterGraph>();
            BuildSubOnly(a, "a", b);
            BuildSubOnly(b, "b", a);
            try
            {
                var runner = new BaseRunner();
                Assert.Throws<GraphCycleException>(() =>
                {
                    runner.Start(a, new StarterContext(), new NodeExecutorRegistry());
                    RunToEnd(runner);
                });
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        // ── Sample generator ───────────────────────────────────────────────────
        [Test]
        public void SampleBuilder_GeneratesRunnableGraph()
        {
            // Generate into a TEMP folder (not the committed Samples asset): building in place rewrote
            // StarterSampleGraph.asset with a fresh GraphId on every test run, dirtying the working tree.
            const string tempFolder = "Assets/Temp_StarterSampleBuilderTest";
            var path = tempFolder + "/StarterSampleGraph_Test.asset";
            if (!UnityEditor.AssetDatabase.IsValidFolder(tempFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Temp_StarterSampleBuilderTest");
            try
            {
                var sample = StarterSampleBuilder.CreateSampleAt(path);
                Assert.IsNotNull(sample, "Sample asset must be generated");
                Assert.IsTrue(sample.Nodes.OfType<ChoiceNodeData>().Any(), "Sample must contain a Choice node");
                Assert.IsTrue(sample.Nodes.OfType<EndNodeData>().Any(), "Sample must contain an End node");

                // Run it: it must reach and pause at the choice.
                var ctx = new StarterContext(); ctx.InitFromGraph(sample);
                var runner = new BaseRunner();
                runner.Start(sample, ctx, new NodeExecutorRegistry());
                int guard = 0;
                while (runner.State == RunnerState.NodeReady && guard++ < 200 && !(runner.CurrentNode is ChoiceNodeData))
                    runner.Proceed();
                Assert.IsInstanceOf<ChoiceNodeData>(runner.CurrentNode, "Running the sample must reach the Choice node");
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(tempFolder);
            }
        }
    }
}
