using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests.PlayMode
{
    /// <summary>
    /// The regression that was missing: a REAL cross-scene run. One graph spans scenes via single-mode loads;
    /// a persistent driver must survive each load (which actually tears the old scene down) and run the flow
    /// to the end. The slice-1/2 stub loader recorded loads without ever destroying a scene — which is exactly
    /// why this class of bug shipped green. Uses two committed test scenes registered in Build Settings.
    /// </summary>
    public sealed class CrossSceneSurvivalTests
    {
        private const string SceneA = "GameFlowCrossSceneA";
        private const string SceneB = "GameFlowCrossSceneB";

        private const string ScenesDir = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow/Tests/PlayMode/Scenes";

        private readonly List<Object> _objects = new List<Object>();

        // Loads the committed test scenes BY PATH during play mode, so the test is self-contained: it needs no
        // Build Settings entry (that lives in the parent project's ProjectSettings, outside this repo, and a
        // play-mode registration does not take effect for a runtime load anyway). It still performs a REAL
        // single-mode load that tears the old scene down — the destruction the persistent driver must survive.
        private sealed class EditorPathSceneLoader : ISceneLoader
        {
            public void LoadScene(string sceneName, LoadSceneMode mode)
            {
#if UNITY_EDITOR
                var path = $"{ScenesDir}/{sceneName}.unity";
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(mode));
#else
                SceneManager.LoadScene(sceneName, mode);
#endif
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o) Object.Destroy(o);
            _objects.Clear();
        }

        private T Track<T>(T o) where T : Object { _objects.Add(o); return o; }

        private LoadSceneAction Load(string scene)
        {
            var a = Track(ScriptableObject.CreateInstance<LoadSceneAction>());
            a.SceneName = scene; a.Mode = LoadSceneMode.Single;
            return a;
        }

        private GameFlowGraph BuildCrossSceneGraph()
        {
            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start = new StartNodeData     { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var loadA = new StatementNodeData { Id = "loadA", NodeType = StatementNodeData.NodeTypeId };
            loadA.OnEnterActions.Add(Load(SceneA));
            var gate  = new StatementNodeData { Id = "gate",  NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = "advance" };
            var loadB = new StatementNodeData { Id = "loadB", NodeType = StatementNodeData.NodeTypeId };
            loadB.OnEnterActions.Add(Load(SceneB));
            var end   = new EndNodeData       { Id = "end",   NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(start); g.AddNode(loadA); g.AddNode(gate); g.AddNode(loadB); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "loadA" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "loadA", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate",  ToNodeId = "loadB" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "loadB", ToNodeId = "end" });
            return g;
        }

        [UnityTest]
        public IEnumerator PersistentDriver_SurvivesSingleLoads_AndCompletes()
        {
            var graph = BuildCrossSceneGraph();

            // Configure persistence BEFORE Awake via the inactive-GameObject pattern.
            var go = Track(new GameObject("persistent-driver"));   // tracked so TearDown clears it (and Active) on failure
            go.SetActive(false);
            var d = go.AddComponent<GraphFlowDriver>();
            d.PersistAcrossScenes = true;
            d.BootOnStart         = false;   // we boot explicitly
            d.Graph               = graph;
            d.SceneLoader         = new EditorPathSceneLoader();   // real single-mode loads, by path
            go.SetActive(true);              // Awake → DontDestroyOnLoad; Active = d

            bool ended = false;
            d.OnEnded += _ => ended = true;

            yield return null;               // Start() runs — with BootOnStart=false it must NOT boot
            Assert.IsFalse(d.IsRunning, "BootOnStart=false: Start must not auto-boot.");
            Assert.AreSame(d, GraphFlowDriver.Active, "the persistent driver is the Active one.");

            d.Boot();                        // start → loadA: a REAL single-mode load of scene A
            yield return null;
            yield return null;

            Assert.IsTrue(d != null, "the driver survived the first single-mode scene load (it did not before).");
            Assert.AreEqual(SceneA, SceneManager.GetActiveScene().name, "scene A is the active scene.");
            Assert.IsTrue(d.IsWaitingForSignal && d.CurrentAwaitSignal == "advance", "parked awaiting 'advance'.");

            d.RaiseSignal("advance");        // → loadB: a second REAL single-mode load → end
            yield return null;
            yield return null;

            Assert.IsTrue(d != null, "the driver survived the second single-mode scene load.");
            Assert.AreEqual(SceneB, SceneManager.GetActiveScene().name, "scene B is the active scene.");
            Assert.IsTrue(ended, "the flow reached its end across the scene loads.");

            if (d != null) Object.Destroy(d.gameObject);
        }
    }
}
