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
    /// Real async loads of the committed cross-scene test scenes, driving <see cref="AsyncSceneLoader"/>
    /// through its progress/ready/completed events and its manual activation gate. Loaded by path (see
    /// <see cref="CrossSceneSurvivalTests"/>) so the test needs no Build Settings entry.
    /// </summary>
    public sealed class AsyncSceneLoaderTests
    {
        // Dedicated scenes — not shared with any other PlayMode fixture in this project, so a Single load
        // here (which permanently claims a scene for the rest of the whole cross-package PlayMode session)
        // can never collide with an unrelated fixture elsewhere. See SceneRegistryPlayModeTests for the
        // incident this convention exists to prevent.
        private const string SceneA = "AsyncLoaderSceneA";
        private const string SceneB = "AsyncLoaderSceneB";
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

        [UnityTest]
        public IEnumerator BackToBackLoads_AreQueued_NotDropped()
        {
            // Regression: a LoadScene issued while another load was in flight used to be DROPPED with a
            // warning — fatal for a graph chaining several scene operations in one auto-advance pass. Both
            // requests must now complete, in FIFO order.
            var go = Track(new GameObject("async-loader-queue"));
            var loader = go.AddComponent<EditorPathAsyncSceneLoader>();
            loader.AutoActivate = true;

            var completed = new List<string>();
            loader.SceneLoadCompleted += s => completed.Add(s);

            loader.LoadScene(SceneA, LoadSceneMode.Single);
            loader.LoadScene(SceneB, LoadSceneMode.Additive);   // in flight → must queue, not drop
            Assert.IsTrue(loader.IsLoading, "loading starts synchronously within LoadScene.");

            float timeout = Time.realtimeSinceStartup + 10f;
            while (loader.IsLoading && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.IsFalse(loader.IsLoading, "both loads finished within the timeout.");
            CollectionAssert.AreEqual(new[] { SceneA, SceneB }, completed, "both queued loads completed, in FIFO order.");
            Assert.AreEqual(SceneA, SceneManager.GetActiveScene().name, "the Single load is the active scene.");
            Assert.IsTrue(SceneManager.GetSceneByName(SceneB).isLoaded, "the queued Additive load is stacked on top.");

            // Leave a single-scene state behind for the next test.
            var unload = SceneManager.UnloadSceneAsync(SceneB);
            while (!unload.isDone) yield return null;
        }

        [UnityTest]
        public IEnumerator LoadScene_Failure_RaisesEventAndSignal_WithNameAndReason()
        {
            // The plain AsyncSceneLoader (not EditorPathAsyncSceneLoader) hits BeginLoad's real
            // Application.CanStreamedLevelBeLoaded guard directly — no committed scene needed to prove a
            // failure is now VISIBLE (event + signal), not just a dropped log line.
            var go = Track(new GameObject("async-loader-load-fail"));
            var loader = go.AddComponent<AsyncSceneLoader>();

            string failedName = null, failedReason = null;
            loader.SceneLoadFailed += (n, r) => { failedName = n; failedReason = r; };

            LogAssert.Expect(LogType.Error,
                "[GraphGameFlow] Scene 'DefinitelyNotARealScene' cannot be loaded (not in Build Settings / Addressables). Ignored.");
            loader.LoadScene("DefinitelyNotARealScene", LoadSceneMode.Additive);
            yield return null;

            Assert.AreEqual("DefinitelyNotARealScene", failedName, "the event names which scene failed.");
            StringAssert.Contains("not in Build Settings", failedReason, "the event explains why it failed.");
            Assert.IsFalse(loader.IsLoading, "the pump does not get stuck on a failed request.");
        }

        [UnityTest]
        public IEnumerator UnloadScene_Failure_RaisesEventAndSignal_WithNameAndReason()
        {
            var go = Track(new GameObject("async-loader-unload-fail"));
            var loader = go.AddComponent<AsyncSceneLoader>();

            string failedName = null, failedReason = null;
            loader.SceneUnloadFailed += (n, r) => { failedName = n; failedReason = r; };

            LogAssert.Expect(LogType.Error, "[GraphGameFlow] Scene 'NeverLoadedScene' is not loaded; unload ignored.");
            loader.UnloadScene("NeverLoadedScene");
            yield return null;

            Assert.AreEqual("NeverLoadedScene", failedName);
            StringAssert.Contains("is not loaded", failedReason);
        }

        [UnityTest]
        public IEnumerator LoadScene_Failure_ResumesADriverAwaitingEitherCompletedOrFailedSignal()
        {
            // The escape hatch this whole pair of tests exists to prove: a node awaiting BOTH the completed
            // AND the failed signal (AwaitSignalNames — logical OR) resumes on a failure instead of parking
            // forever, with a payload identifying what failed and why.
            var loadedSig = Track(SignalDef.Create("async-load-ok"));
            var failedSig = Track(SignalDef.Create("async-load-failed"));

            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var gate  = new StatementNodeData { Id = "gate", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = (string)loadedSig };
            gate.AwaitSignalNamesExtra.Add((string)failedSig);
            var end   = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(start); g.AddNode(gate); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });

            var driverGo = Track(new GameObject("async-fail-resume-driver"));
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = g;

            var loaderGo = Track(new GameObject("async-fail-resume-loader"));
            var loader = loaderGo.AddComponent<AsyncSceneLoader>();
            loader.LoadCompletedSignal = loadedSig;
            loader.LoadFailedSignal    = failedSig;
            loader.SignalDriver        = driver;
            driver.SceneLoader          = loader;

            bool ended = false;
            driver.OnEnded += _ => ended = true;

            driver.Boot();
            Assert.IsTrue(driver.IsWaitingForSignal, "parked on the gate node.");

            LogAssert.Expect(LogType.Error,
                "[GraphGameFlow] Scene 'AnotherFakeScene' cannot be loaded (not in Build Settings / Addressables). Ignored.");
            loader.LoadScene("AnotherFakeScene", LoadSceneMode.Additive);
            yield return null;

            Assert.IsTrue(ended, "the failed-load signal resumed the parked flow via the OR-await, instead of stalling it.");
        }

        [UnityTest]
        public IEnumerator LoadSceneAction_InstantFailure_ResumesLiveWithoutResumeIfAlreadyRaised()
        {
            // The ACTUAL trap this test guards against (found via independent testing, not the driver-
            // external-trigger shape of the test above): a LoadSceneAction on a node's OnEnterActions,
            // immediately followed — in the SAME synchronous auto-advance pass — by a node awaiting
            // completed-OR-failed. Before LoadRoutine deferred its failure branch by one frame, an instant
            // failure (bad scene name) fired the failure signal SYNCHRONOUSLY inside OnEnterActions, before
            // the runner had even entered the awaiting node — the signal was gone by the time anything could
            // catch it live, and ResumeIfSignalAlreadyRaised was the only way to recover it from history.
            // ResumeIfSignalAlreadyRaised is deliberately left OFF here to prove the live path alone is
            // now enough.
            var loadedSig = Track(SignalDef.Create("async-trap-ok"));
            var failedSig = Track(SignalDef.Create("async-trap-failed"));

            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start    = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var loadNode = new StatementNodeData { Id = "load", NodeType = StatementNodeData.NodeTypeId };
            var loadAction = Track(ScriptableObject.CreateInstance<LoadSceneAction>());
            loadAction.SceneName = "DefinitelyNotARealSceneForTrapTest";
            loadAction.Mode = LoadSceneMode.Additive;
            loadNode.OnEnterActions.Add(loadAction);
            var gate = new StatementNodeData { Id = "gate", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = (string)loadedSig };
            gate.AwaitSignalNamesExtra.Add((string)failedSig);
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(start); g.AddNode(loadNode); g.AddNode(gate); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "load" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "load", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });

            var driverGo = Track(new GameObject("async-trap-driver"));
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = g;

            var loaderGo = Track(new GameObject("async-trap-loader"));
            var loader = loaderGo.AddComponent<AsyncSceneLoader>();
            loader.LoadCompletedSignal = loadedSig;
            loader.LoadFailedSignal    = failedSig;
            loader.SignalDriver        = driver;
            driver.SceneLoader          = loader;

            bool ended = false;
            driver.OnEnded += _ => ended = true;

            LogAssert.Expect(LogType.Error,
                "[GraphGameFlow] Scene 'DefinitelyNotARealSceneForTrapTest' cannot be loaded (not in Build Settings / Addressables). Ignored.");
            driver.Boot();   // start -> load (OnEnterActions runs the failing LoadSceneAction) -> gate (parks), all synchronous within this one call

            Assert.IsTrue(driver.IsWaitingForSignal, "parked on the gate node — the load's own OnEnterActions already ran.");

            yield return null;   // the deferred failure signal fires here, on the loader's coroutine

            Assert.IsTrue(ended, "resumed via the LIVE failure signal — no ResumeIfSignalAlreadyRaised needed.");
        }

        [UnityTest]
        public IEnumerator StuckOperationWarning_FiresOnceAfterThreshold_WhenHeldOpenByManualActivation()
        {
            var go = Track(new GameObject("async-loader-stuck"));
            var loader = go.AddComponent<EditorPathAsyncSceneLoader>();
            loader.AutoActivate = false;
            loader.StuckOperationWarningAfter = 0.001f;   // effectively "any real delay at all"

            var fired = new List<(string Name, float Elapsed)>();
            loader.OperationTakingTooLong += (n, e) => fired.Add((n, e));

            loader.LoadScene(SceneA, LoadSceneMode.Single);

            // Held open well past the threshold, across several real frames, without ever activating.
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(1, fired.Count, "the warning fires exactly once, however many frames elapse past the threshold.");
            Assert.AreEqual(SceneA, fired[0].Name);
            Assert.Greater(fired[0].Elapsed, 0f);

            loader.ActivateReadyScene();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (loader.IsLoading && Time.realtimeSinceStartup < timeout) yield return null;
        }

        [UnityTest]
        public IEnumerator StuckOperationWarning_Disabled_NeverFires()
        {
            var go = Track(new GameObject("async-loader-stuck-disabled"));
            var loader = go.AddComponent<EditorPathAsyncSceneLoader>();
            loader.AutoActivate = false;
            loader.StuckOperationWarningAfter = 0f;   // 0 or less disables it

            bool fired = false;
            loader.OperationTakingTooLong += (_, __) => fired = true;

            loader.LoadScene(SceneB, LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsFalse(fired, "disabled (<=0) must never fire, regardless of how long the operation is held open.");

            loader.ActivateReadyScene();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (loader.IsLoading && Time.realtimeSinceStartup < timeout) yield return null;
        }
    }
}
