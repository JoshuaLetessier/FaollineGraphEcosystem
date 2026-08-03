using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphSave;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// End-to-end proof that <see cref="IGraphCatalog"/> closes the gap <c>GraphRunSnapshot.GraphId</c> leaves
    /// open: with more than one root graph, restoring a save needs a <c>GraphId → BaseGraph</c> resolution step,
    /// and this is that step — with zero hand-written lookup table in the calling code, and zero dependency
    /// on any asynchronous asset-loading technology (a <see cref="DirectGraphCatalog"/> is enough).
    /// </summary>
    public class GraphCatalogSaveRestoreTests
    {
        private static BaseGraph StartEndGraph()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>();
            g.AddNode(new StartNodeData { Id = "s", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(new EndNodeData { Id = "e", NodeType = EndNodeData.NodeTypeId });
            g.AddEdge(new BaseEdgeData { Id = "e1", FromNodeId = "s", ToNodeId = "e", PortName = "out" });
            g.EntryNodeId = "s";
            return g;
        }

        [Test]
        public void RestoreByGraphIdAlone_ResumesOnCorrectGraph_AmongMultipleRoots()
        {
            var graphA = StartEndGraph();
            var graphB = StartEndGraph();
            try
            {
                var catalog = new DirectGraphCatalog();
                catalog.Register(graphA.GraphId, graphA);
                catalog.Register(graphB.GraphId, graphB);

                var context = new BaseContext();
                context.Set<int>("hp", 7);
                var snapshot = GraphRunSnapshot.Capture(context, graphB.GraphId, "s");

                BaseGraph resolved = null;
                catalog.Resolve(snapshot.GraphId, g => resolved = g, r => Assert.Fail(r));
                Assert.AreSame(graphB, resolved, "resolution must pick the graph the snapshot actually refers to, not just any registered one.");

                var restoredContext = new BaseContext();
                var runner = new BaseRunner();
                snapshot.Restore(runner, resolved, restoredContext);

                Assert.AreEqual(7, restoredContext.Get<int>("hp"), "captured parameters round-trip through the resolved graph.");
                Assert.AreEqual(RunnerState.NodeReady, runner.State);
                Assert.AreEqual("s", runner.CurrentNode.Id);
            }
            finally { Object.DestroyImmediate(graphA); Object.DestroyImmediate(graphB); }
        }

        [Test]
        public void ResolveUnknownGraphId_FailsCleanly_CallerCanDetect()
        {
            var catalog = new DirectGraphCatalog();
            var snapshot = GraphRunSnapshot.Capture(new BaseContext(), "never-registered", "s");
            LogAssert.Expect(LogType.Error, "[GraphGameFlow] DirectGraphCatalog has no graph registered for id 'never-registered'.");

            bool failed = false;
            catalog.Resolve(snapshot.GraphId, g => Assert.Fail("must not resolve an unregistered id"), r => failed = true);

            Assert.IsTrue(failed, "a save referring to content that's gone must fail detectably, not silently produce a null/wrong graph.");
        }
    }
}
