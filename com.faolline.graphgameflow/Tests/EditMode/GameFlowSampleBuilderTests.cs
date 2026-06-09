using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphGameFlow.Editor;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// The sample builder is the test-driven heart of the editor slice: it must produce the exact reference
    /// scene-flow structure and that structure must run under the slice-1 driver.
    /// </summary>
    public class GameFlowSampleBuilderTests
    {
        private readonly List<GameObject> _go = new List<GameObject>();
        private string _createdPath;

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _go) if (g) Object.DestroyImmediate(g);
            _go.Clear();
            if (!string.IsNullOrEmpty(_createdPath))
            {
                AssetDatabase.DeleteAsset(_createdPath);
                _createdPath = null;
            }
        }

        [Test]
        public void CreateSample_ProducesReferenceStructure()
        {
            var graph = GameFlowSampleBuilder.CreateSample();
            _createdPath = AssetDatabase.GetAssetPath(graph);

            Assert.IsInstanceOf<GameFlowGraph>(graph);

            var nodes = graph.Nodes;
            Assert.AreEqual(1, nodes.Count(n => n is StartNodeData), "exactly one Start node.");
            Assert.AreEqual(1, nodes.Count(n => n is EndNodeData), "exactly one End node.");
            Assert.AreEqual(3, nodes.Count(n => n.NodeType == StatementNodeData.NodeTypeId), "three statement nodes.");
            Assert.AreEqual(4, graph.Edges.Count, "four edges.");
            Assert.IsTrue(nodes.Any(n => n.AwaitSignalName == "advance"), "one await-'advance' node.");

            var loadScenes = nodes.SelectMany(n => n.OnEnterActions)
                                  .OfType<LoadSceneAction>()
                                  .Select(a => a.SceneName)
                                  .OrderBy(s => s)
                                  .ToList();
            CollectionAssert.AreEqual(new[] { "A", "B" }, loadScenes, "two Load Scene actions for A and B.");
        }

        [Test]
        public void CreateSample_RunsUnderDriver_AThenAwaitThenB()
        {
            var graph = GameFlowSampleBuilder.CreateSample();
            _createdPath = AssetDatabase.GetAssetPath(graph);

            var go = new GameObject("driver"); _go.Add(go);
            var d = go.AddComponent<GraphFlowDriver>();
            var loader = new StubSceneLoader();
            d.Graph = graph; d.AutoAdvance = true; d.SceneLoader = loader;

            d.Boot();
            Assert.AreEqual("A", loader.LastScene, "loads A on boot, then parks on the await node.");
            Assert.AreEqual(1, loader.Calls.Count, "B not loaded while parked.");

            d.RaiseSignal("advance");
            Assert.AreEqual("B", loader.LastScene, "the matching signal resumes and loads B.");
            Assert.AreEqual(2, loader.Calls.Count);
        }
    }
}
