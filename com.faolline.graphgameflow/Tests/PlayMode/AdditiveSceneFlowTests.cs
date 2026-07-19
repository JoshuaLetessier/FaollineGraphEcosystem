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
    /// The additive end-to-end that was missing: one graph drives a REAL hub + overlay scene system through
    /// an <see cref="AsyncSceneLoader"/> — Single-load the hub, Additive-load an overlay on top, unload it
    /// again — with the flow parked on await-signal gates that the loader's completion signals resume. No
    /// manual event wiring anywhere: the loader's <c>LoadCompletedSignal</c>/<c>UnloadCompletedSignal</c> are
    /// the only bridge, so this locks the whole "flow waits for its scenes" contract, not just the loader.
    /// </summary>
    public sealed class AdditiveSceneFlowTests
    {
        private const string HubScene     = "GameFlowCrossSceneA";
        private const string OverlayScene = "GameFlowCrossSceneB";
        private const string ScenesDir    = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow/Tests/PlayMode/Scenes";

        /// <summary>Loads by editor path instead of Build Settings, exactly like <c>AsyncSceneLoaderTests</c>.</summary>
        private sealed class EditorPathAsyncSceneLoader : AsyncSceneLoader
        {
            protected override AsyncOperation BeginLoad(string sceneName, LoadSceneMode mode)
            {
#if UNITY_EDITOR
                var path = $"{ScenesDir}/{sceneName}.unity";
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(mode));
#else
                return SceneManager.LoadSceneAsync(sceneName, mode);
#endif
            }
        }

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o) Object.Destroy(o);
            _objects.Clear();
        }

        private T Track<T>(T o) where T : Object { _objects.Add(o); return o; }

        private LoadSceneAction Load(string scene, LoadSceneMode mode)
        {
            var a = Track(ScriptableObject.CreateInstance<LoadSceneAction>());
            a.SceneName = scene; a.Mode = mode;
            return a;
        }

        private UnloadSceneAction Unload(string scene)
        {
            var a = Track(ScriptableObject.CreateInstance<UnloadSceneAction>());
            a.SceneName = scene;
            return a;
        }

        private static StatementNodeData Statement(string id, string awaitSignal = null)
            => new StatementNodeData { Id = id, NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = awaitSignal ?? "" };

        [UnityTest]
        public IEnumerator HubAndOverlay_LoadAdditive_ThenUnload_FlowGatedOnCompletionSignals()
        {
            var loadedSig   = Track(SignalDef.Create("scene-load-done"));
            var unloadedSig = Track(SignalDef.Create("scene-unload-done"));

            // start → loadHub(Single) → gate(await load) → loadOverlay(Additive) → gate(await load)
            //       → unloadOverlay → gate(await unload) → end
            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start       = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var loadHub     = Statement("loadHub");
            loadHub.OnEnterActions.Add(Load(HubScene, LoadSceneMode.Single));
            var gateHub     = Statement("gateHub", (string)loadedSig);
            var loadOverlay = Statement("loadOverlay");
            loadOverlay.OnEnterActions.Add(Load(OverlayScene, LoadSceneMode.Additive));
            var gateOverlay = Statement("gateOverlay", (string)loadedSig);
            var unloadNode  = Statement("unloadOverlay");
            unloadNode.OnEnterActions.Add(Unload(OverlayScene));
            var gateUnload  = Statement("gateUnload", (string)unloadedSig);
            var end         = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            foreach (var n in new BaseNodeData[] { start, loadHub, gateHub, loadOverlay, gateOverlay, unloadNode, gateUnload, end })
                g.AddNode(n);
            string prev = null;
            foreach (var id in new[] { "start", "loadHub", "gateHub", "loadOverlay", "gateOverlay", "unloadOverlay", "gateUnload", "end" })
            {
                if (prev != null) g.AddEdge(new BaseEdgeData { FromNodeId = prev, ToNodeId = id });
                prev = id;
            }

            // Persistent driver (the hub load is Single: it tears the test scene down under the driver).
            var driverGo = Track(new GameObject("additive-flow-driver"));
            driverGo.SetActive(false);
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.PersistAcrossScenes = true;
            driver.BootOnStart         = false;
            driver.Graph               = g;
            driverGo.SetActive(true);

            var loaderGo = Track(new GameObject("additive-flow-loader"));
            var loader = loaderGo.AddComponent<EditorPathAsyncSceneLoader>();
            loader.LoadCompletedSignal   = loadedSig;
            loader.UnloadCompletedSignal = unloadedSig;
            loader.SignalDriver          = driver;
            driver.SceneLoader           = loader;

            var overlayWasLoaded = false;
            loader.SceneLoadCompleted += s => { if (s == OverlayScene) overlayWasLoaded = SceneManager.GetSceneByName(OverlayScene).isLoaded; };

            bool ended = false;
            driver.OnEnded += _ => ended = true;

            driver.Boot();
            Assert.IsTrue(driver.IsRunning, "the flow booted.");

            float timeout = Time.realtimeSinceStartup + 15f;
            while (!ended && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.IsTrue(ended, "the flow ran hub-load → overlay-load → overlay-unload to the end, gated only by completion signals.");
            Assert.AreEqual(HubScene, SceneManager.GetActiveScene().name, "the hub is the active scene.");
            Assert.IsTrue(overlayWasLoaded, "the overlay really was stacked additively at its completion.");
            Assert.IsFalse(SceneManager.GetSceneByName(OverlayScene).isLoaded, "the overlay is unloaded again.");
            Assert.IsFalse(loader.IsLoading, "the loader queue drained.");

            if (driver != null) Object.Destroy(driver.gameObject);
        }
    }
}
