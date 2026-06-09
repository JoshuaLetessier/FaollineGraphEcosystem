using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// US3 — the full reference scene-flow: start → load A → await "advance" → load B → end, over one shared
    /// context, driven by the host bridge with a recording stub loader.
    /// </summary>
    public class SceneFlowReferenceTests
    {
        private readonly List<Object> _so = new List<Object>();
        private readonly List<GameObject> _go = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _go) if (g) Object.DestroyImmediate(g);
            _go.Clear();
            foreach (var o in _so) if (o) Object.DestroyImmediate(o);
            _so.Clear();
        }

        private LoadSceneAction Load(string scene)
        {
            var a = ScriptableObject.CreateInstance<LoadSceneAction>();
            a.SceneName = scene; a.Mode = LoadSceneMode.Single;
            _so.Add(a);
            return a;
        }

        private (GraphFlowDriver driver, StubSceneLoader loader) BuildReferenceFlow()
        {
            var g = ScriptableObject.CreateInstance<BaseGraph>(); _so.Add(g);
            g.EntryNodeId = "start";

            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var loadA = new StatementNodeData { Id = "loadA", NodeType = StatementNodeData.NodeTypeId };
            loadA.OnEnterActions.Add(Load("A"));
            var gate = new StatementNodeData { Id = "gate", NodeType = StatementNodeData.NodeTypeId };
            gate.AwaitSignalName = "advance";
            var loadB = new StatementNodeData { Id = "loadB", NodeType = StatementNodeData.NodeTypeId };
            loadB.OnEnterActions.Add(Load("B"));
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };

            g.AddNode(start); g.AddNode(loadA); g.AddNode(gate); g.AddNode(loadB); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "loadA" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "loadA", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "loadB" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "loadB", ToNodeId = "end" });

            var go = new GameObject("driver"); _go.Add(go);
            var d = go.AddComponent<GraphFlowDriver>();
            d.Graph = g; d.AutoAdvance = true;
            var loader = new StubSceneLoader();
            d.SceneLoader = loader;
            return (d, loader);
        }

        [Test]
        public void Boot_LoadsA_ThenParksOnAwait()
        {
            var (d, loader) = BuildReferenceFlow();
            string waiting = null;
            d.OnWaitingForSignal += (n, sig) => waiting = sig;
            d.Boot();

            Assert.AreEqual("A", loader.LastScene, "scene A loads on entering the first node.");
            Assert.AreEqual(1, loader.Calls.Count, "B has not loaded yet.");
            Assert.AreEqual("advance", waiting, "the flow parks awaiting 'advance'.");
        }

        [Test]
        public void NonMatchingSignal_DoesNotAdvance()
        {
            var (d, loader) = BuildReferenceFlow();
            d.Boot();
            d.RaiseSignal("nope");
            Assert.AreEqual(1, loader.Calls.Count, "a non-matching signal must not load B.");
        }

        [Test]
        public void MatchingSignal_ResumesAndLoadsB_ThenEnds()
        {
            var (d, loader) = BuildReferenceFlow();
            EndReason? ended = null;
            d.OnEnded += r => ended = r;
            d.Boot();

            d.RaiseSignal("advance");

            Assert.AreEqual(2, loader.Calls.Count);
            Assert.AreEqual("B", loader.LastScene, "the matching signal resumes and loads B.");
            Assert.AreEqual(EndReason.Completed, ended, "the flow reaches its end.");
        }

        [Test]
        public void Signal_AfterEnd_IsNoOp()
        {
            var (d, loader) = BuildReferenceFlow();
            d.Boot();
            d.RaiseSignal("advance");   // runs to end
            Assert.DoesNotThrow(() => d.RaiseSignal("advance"));
            Assert.AreEqual(2, loader.Calls.Count, "no extra load after the flow ended.");
        }
    }
}
