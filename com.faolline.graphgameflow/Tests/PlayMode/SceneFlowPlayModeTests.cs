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
    /// PlayMode — the genuine path EditMode cannot exercise: the real Unity lifecycle (<c>Start</c>→
    /// <c>Boot</c>, <c>Update</c>→<c>Tick</c> with real frame time) driving the host bridge end to end. The
    /// loader is injected (recording) so the assertion is deterministic; the literal
    /// <c>SceneManager.LoadScene</c> call is Unity's own API, delegated to <see cref="UnitySceneLoader"/> and
    /// covered by its graceful-failure EditMode test.
    /// </summary>
    public class SceneFlowPlayModeTests
    {
        private sealed class RecordingLoader : ISceneLoader
        {
            public readonly List<string> Loaded = new List<string>();
            public string Last => Loaded.Count == 0 ? null : Loaded[Loaded.Count - 1];
            public void LoadScene(string sceneName, LoadSceneMode mode) => Loaded.Add(sceneName);
        }

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o) Object.Destroy(o);
            _objects.Clear();
        }

        private T Track<T>(T o) where T : Object { _objects.Add(o); return o; }

        private static StatementNodeData St(string id) => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId };

        [UnityTest]
        public IEnumerator Driver_UpdatePump_ResolvesATimeWaitNode_InRealTime()
        {
            // The thin Unity hooks (Start→Boot, Update→Tick) actually fire and feed real frame time.
            var graph = Track(ScriptableObject.CreateInstance<BaseGraph>());
            graph.EntryNodeId = "wait";
            var wait = St("wait"); wait.WaitDuration = 0.1f;
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            graph.AddNode(wait); graph.AddNode(end);
            graph.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });

            var go = Track(new GameObject("driver"));
            var d = go.AddComponent<GraphFlowDriver>();
            d.Graph = graph;
            d.AutoAdvance = true;

            bool ended = false;
            d.OnEnded += _ => ended = true;

            float timeout = Time.realtimeSinceStartup + 3f;
            while (!ended && Time.realtimeSinceStartup < timeout)
                yield return null;    // Update() feeds Time.deltaTime into the runner each frame

            Assert.IsTrue(ended, "the Update pump fed enough real time to resolve the wait node.");
        }

        [UnityTest]
        public IEnumerator ReferenceFlow_UnderRealPump_LoadsA_Awaits_ThenLoadsBOnSignal()
        {
            // The full start → load A → await "advance" → load B → end flow, booted by Unity's Start() and
            // advanced by real frames — the EditMode reference flow, now under the genuine runtime.
            var loader = new RecordingLoader();

            var graph = Track(ScriptableObject.CreateInstance<BaseGraph>());
            graph.EntryNodeId = "start";
            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var loadA = St("loadA"); loadA.OnEnterActions.Add(NewLoad("A"));
            var gate = St("gate"); gate.AwaitSignalName = "advance";
            var loadB = St("loadB"); loadB.OnEnterActions.Add(NewLoad("B"));
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            graph.AddNode(start); graph.AddNode(loadA); graph.AddNode(gate); graph.AddNode(loadB); graph.AddNode(end);
            graph.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "loadA" });
            graph.AddEdge(new BaseEdgeData { FromNodeId = "loadA", ToNodeId = "gate" });
            graph.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "loadB" });
            graph.AddEdge(new BaseEdgeData { FromNodeId = "loadB", ToNodeId = "end" });

            var go = Track(new GameObject("driver"));
            var d = go.AddComponent<GraphFlowDriver>();
            d.Graph = graph;
            d.AutoAdvance = true;
            d.SceneLoader = loader;   // injected before Start() boots it next frame

            bool ended = false;
            d.OnEnded += _ => ended = true;

            yield return null;        // Start() → Boot() → loads A, parks on the await node
            Assert.AreEqual("A", loader.Last, "scene A loaded under the real pump.");
            Assert.AreEqual(1, loader.Loaded.Count, "B not loaded while parked.");
            Assert.IsTrue(d.IsRunning, "the flow is parked awaiting the signal.");

            d.RaiseSignal("advance");
            yield return null;

            Assert.AreEqual("B", loader.Last, "the signal resumed the flow and loaded B.");
            Assert.AreEqual(2, loader.Loaded.Count);
            Assert.IsTrue(ended, "the flow reached its end.");
        }

        private LoadSceneAction NewLoad(string scene)
        {
            var a = Track(ScriptableObject.CreateInstance<LoadSceneAction>());
            a.SceneName = scene;
            a.Mode = LoadSceneMode.Single;
            return a;
        }
    }
}
