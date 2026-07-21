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
    /// <see cref="GraphFlowDriver"/> keeps <see cref="GameFlowContext.LoadedScenes"/> in sync with Unity's
    /// own <c>SceneManager.sceneLoaded</c>/<c>sceneUnloaded</c> events — loader-agnostic (it never inspects
    /// which <see cref="ISceneLoader"/> did the loading) and seeded with whatever is already loaded at
    /// <see cref="GraphFlowDriver.Boot()"/>. Real scene loads only, mirroring the rest of this package's
    /// PlayMode suite.
    /// </summary>
    public sealed class SceneRegistryPlayModeTests
    {
        private const string SceneA = "GameFlowCrossSceneA";
        private const string SceneB = "GameFlowCrossSceneB";
        private const string ScenesDir = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow/Tests/PlayMode/Scenes";

        /// <summary>Loads/unloads by editor path instead of Build Settings, matching this package's other PlayMode fixtures.</summary>
        private sealed class EditorPathSceneLoader : ISceneLoader, ISceneUnloader
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

            public void UnloadScene(string sceneName) => SceneManager.UnloadSceneAsync(sceneName);
        }

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o) Object.Destroy(o);
            _objects.Clear();
        }

        private T Track<T>(T o) where T : Object { _objects.Add(o); return o; }

        // GameFlowCrossSceneA/B are shared across several PlayMode fixtures in this assembly, all running in
        // NUnit's own (randomised) order within one continuous play session. SceneManager.GetSceneByName /
        // UnloadSceneAsync(string) only ever resolve the FIRST loaded instance of a name — if an earlier
        // fixture left a duplicate-named instance loaded, those calls can silently miss it. Iterate the real
        // Scene structs instead, so cleanup and assertions are correct even with duplicates in play.
        private static bool AnyInstanceLoaded(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName && scene.isLoaded) return true;
            }
            return false;
        }

        private static IEnumerator UnloadAllInstances(string sceneName)
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == sceneName && scene.isLoaded)
                    SceneManager.UnloadSceneAsync(scene);
            }

            yield return WaitUntilNoInstanceLoaded(sceneName);
        }

        private static IEnumerator WaitUntilNoInstanceLoaded(string sceneName)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (AnyInstanceLoaded(sceneName) && Time.realtimeSinceStartup < timeout)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Boot_SeedsRegistry_WithCurrentlyLoadedScene()
        {
            var currentSceneName = SceneManager.GetActiveScene().name;

            var go = Track(new GameObject("registry-driver-seed"));
            var driver = go.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            driver.Graph.EntryNodeId = "start";
            driver.Graph.AddNode(new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId });

            driver.Boot();

            Assert.IsTrue(driver.Context.IsSceneLoaded(currentSceneName),
                "the scene active at Boot() time is seeded into the registry immediately, before any load event.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AdditiveLoadAndUnload_UpdatesRegistry()
        {
            var sceneLoader = new EditorPathSceneLoader();

            // Either GameFlowCrossSceneA or B may already be the sole permanently-active scene, left behind
            // by an earlier Single-mode test elsewhere in this run (this project's whole PlayMode session is
            // shared across EVERY package's tests, not just this one) — a Single load never "restores", and
            // Unity refuses to unload the last remaining scene. Pick whichever of the two is NOT the current
            // active scene as the target: additively loading/unloading it is then always safe (there is
            // always at least the active scene left) and it is guaranteed to start unloaded.
            string target = SceneManager.GetActiveScene().name == SceneA ? SceneB : SceneA;
            yield return UnloadAllInstances(target);   // clear any stray leftover instance before asserting a clean baseline

            var go = Track(new GameObject("registry-driver-additive"));
            var driver = go.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.SceneLoader = sceneLoader;
            driver.Graph = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            driver.Graph.EntryNodeId = "start";
            driver.Graph.AddNode(new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId });

            driver.Boot();
            Assert.IsFalse(driver.Context.IsSceneLoaded(target), "not loaded yet.");

            driver.SceneLoader.LoadScene(target, LoadSceneMode.Additive);
            yield return null;
            yield return null;

            Assert.IsTrue(driver.Context.IsSceneLoaded(target), "the registry picked up the additive load via SceneManager.sceneLoaded.");

            ((ISceneUnloader)driver.SceneLoader).UnloadScene(target);
            yield return WaitUntilNoInstanceLoaded(target);
            yield return null;

            Assert.IsFalse(driver.Context.IsSceneLoaded(target), "the registry picked up the unload via SceneManager.sceneUnloaded.");
        }

        [UnityTest]
        public IEnumerator PersistentDriver_RegistryTracksAcrossSingleLoad()
        {
            var go = Track(new GameObject("registry-driver-persistent"));
            go.SetActive(false);
            var driver = go.AddComponent<GraphFlowDriver>();
            driver.PersistAcrossScenes = true;
            driver.BootOnStart = false;
            driver.SceneLoader = new EditorPathSceneLoader();
            driver.Graph = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            driver.Graph.EntryNodeId = "start";
            driver.Graph.AddNode(new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId });
            go.SetActive(true);

            driver.Boot();

            driver.SceneLoader.LoadScene(SceneA, LoadSceneMode.Single);   // real Single load: tears down every other scene
            yield return null;
            yield return null;

            Assert.IsTrue(driver.Context.IsSceneLoaded(SceneA), "the newly active scene is tracked.");
            Assert.AreEqual(1, driver.Context.LoadedScenes.Count, "a Single load leaves exactly one scene loaded.");

            if (driver != null) Object.Destroy(driver.gameObject);
        }
    }
}
