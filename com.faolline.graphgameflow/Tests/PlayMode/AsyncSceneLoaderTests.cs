using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Tests.PlayMode
{
    /// <summary>
    /// Real async loads of the committed cross-scene test scenes, driving <see cref="AsyncSceneLoader"/>
    /// through its progress/ready/completed events and its manual activation gate. Loaded by path (see
    /// <see cref="CrossSceneSurvivalTests"/>) so the test needs no Build Settings entry.
    /// </summary>
    public sealed class AsyncSceneLoaderTests
    {
        private const string SceneA = "GameFlowCrossSceneA";
        private const string SceneB = "GameFlowCrossSceneB";
        private const string ScenesDir = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow/Tests/PlayMode/Scenes";

        /// <summary>Loads by editor path instead of Build Settings index, exactly like <c>CrossSceneSurvivalTests.EditorPathSceneLoader</c>.</summary>
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

        [UnityTest]
        public IEnumerator AutoActivate_LoadsSceneToCompletion_RaisingAllEventsInOrder()
        {
            var go = Track(new GameObject("async-loader"));
            var loader = go.AddComponent<EditorPathAsyncSceneLoader>();
            loader.AutoActivate = true;

            var events = new List<string>();
            loader.SceneLoadStarted   += s => events.Add($"started:{s}");
            loader.SceneLoadReady     += s => events.Add($"ready:{s}");
            loader.SceneLoadCompleted += s => events.Add($"completed:{s}");
            var progressSamples = new List<float>();
            loader.SceneLoadProgress += (_, p) => progressSamples.Add(p);

            loader.LoadScene(SceneA, LoadSceneMode.Single);
            Assert.IsTrue(loader.IsLoading, "loading starts synchronously within LoadScene.");

            float timeout = Time.realtimeSinceStartup + 5f;
            while (loader.IsLoading && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.IsFalse(loader.IsLoading, "the load finished within the timeout.");
            Assert.AreEqual(SceneA, SceneManager.GetActiveScene().name, "the target scene is active.");
            CollectionAssert.AreEqual(
                new[] { $"started:{SceneA}", $"ready:{SceneA}", $"completed:{SceneA}" }, events,
                "lifecycle events fire once, in order.");
            Assert.IsTrue(progressSamples.Count > 0, "progress was reported at least once.");
            Assert.AreEqual(1f, progressSamples[progressSamples.Count - 1], "progress reaches 1 before completion.");
        }

        [UnityTest]
        public IEnumerator ManualActivation_HoldsSceneReady_UntilActivateReadySceneIsCalled()
        {
            var go = Track(new GameObject("async-loader-manual"));
            var loader = go.AddComponent<EditorPathAsyncSceneLoader>();
            loader.AutoActivate = false;

            bool ready = false;
            bool completed = false;
            loader.SceneLoadReady     += _ => ready = true;
            loader.SceneLoadCompleted += _ => completed = true;

            loader.LoadScene(SceneB, LoadSceneMode.Single);

            float timeout = Time.realtimeSinceStartup + 5f;
            while (!ready && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.IsTrue(ready, "the load reached ready within the timeout.");
            Assert.IsFalse(completed, "activation is held open; the load must not complete yet.");
            Assert.IsTrue(loader.IsLoading, "still loading while activation is withheld.");
            Assert.AreNotEqual(SceneB, SceneManager.GetActiveScene().name, "the scene has not activated yet.");

            loader.ActivateReadyScene();

            timeout = Time.realtimeSinceStartup + 5f;
            while (!completed && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.IsTrue(completed, "the load completed after manual activation.");
            Assert.AreEqual(SceneB, SceneManager.GetActiveScene().name, "the target scene is now active.");
        }
    }
}
