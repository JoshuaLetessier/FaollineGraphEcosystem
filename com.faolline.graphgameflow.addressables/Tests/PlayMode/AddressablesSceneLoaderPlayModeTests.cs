using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Faolline.GraphGameFlow.Addressables.Tests.PlayMode
{
    /// <summary>
    /// Real Addressables loads of this package's own dedicated test scenes (never shared with the
    /// graphgameflow core package's fixtures — see the constants below), registered as Addressable entries
    /// for the duration of this fixture (<see cref="RegisterAddressableTestScenes"/>).
    /// Runs under the "Use Asset Database (fastest)" Play Mode script — the standard way to exercise
    /// Addressables from the Editor with no content build. The registration step is Editor-only
    /// (<c>AddressableAssetSettings</c> has no Player-side equivalent — content authoring is always an
    /// Editor concern), guarded by <c>#if UNITY_EDITOR</c> exactly like
    /// <c>CrossSceneSurvivalTests.EditorPathSceneLoader</c>; the assembly itself stays unrestricted
    /// (<c>includePlatforms: []</c>) so Unity Test Runner's normal EditMode/PlayMode exclusion — driven by
    /// this asmdef's <c>testPlatforms</c> — keeps working (restricting to <c>["Editor"]</c> was tried and
    /// broke that exclusion, letting these PlayMode tests run — and fail — under an EditMode batch).
    /// <para>
    /// One-time side effect: if this project has never used Addressables before, the fixture's
    /// <c>AddressableAssetSettingsDefaultObject.GetSettings(true)</c> call creates the standard
    /// <c>Assets/AddressableAssetsData/</c> settings asset — the same one-time initialisation any real
    /// Addressables usage in this project requires. It is not deleted by teardown (only the two test entries
    /// and the Play Mode data builder index are restored) since removing it would leave the project's
    /// Addressables configuration half-broken rather than just absent.
    /// </para>
    /// </summary>
    public sealed class AddressablesSceneLoaderPlayModeTests
    {
        // Dedicated scenes, private to this package's own tests — not the graphgameflow core package's
        // GameFlowCrossSceneA/B (which several of ITS OWN fixtures already share), so a Single load here can
        // never collide with an unrelated fixture in another package's test assembly. See
        // AsyncSceneLoaderTests (core package) for the incident this convention exists to prevent.
        private const string SceneAPath = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow.addressables/Tests/PlayMode/Scenes/AddressablesSceneA.unity";
        private const string SceneBPath = "Assets/FaollineGraphEcosystem/com.faolline.graphgameflow.addressables/Tests/PlayMode/Scenes/AddressablesSceneB.unity";
        private const string SceneAName = "AddressablesSceneA";
        private const string SceneBName = "AddressablesSceneB";
        private const string KeyA = "AddrTest.SceneA";
        private const string KeyB = "AddrTest.SceneB";

#if UNITY_EDITOR
        private AddressableAssetSettings _settings;
        private AddressableAssetEntry    _entryA;
        private AddressableAssetEntry    _entryB;
        private int  _originalPlayModeDataBuilderIndex;

        [OneTimeSetUp]
        public void RegisterAddressableTestScenes()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings
                        ?? AddressableAssetSettingsDefaultObject.GetSettings(true);
            Assert.IsNotNull(_settings, "AddressableAssetSettingsDefaultObject.GetSettings(true) must produce usable settings.");

            _originalPlayModeDataBuilderIndex = _settings.ActivePlayModeDataBuilderIndex;
            int fastModeIndex = _settings.DataBuilders.FindIndex(b => b is BuildScriptFastMode);
            Assert.GreaterOrEqual(fastModeIndex, 0, "the Addressables package always ships a Fast Mode data builder.");
            _settings.ActivePlayModeDataBuilderIndex = fastModeIndex;

            var group = _settings.DefaultGroup;
            var guidA = AssetDatabase.AssetPathToGUID(SceneAPath);
            var guidB = AssetDatabase.AssetPathToGUID(SceneBPath);
            Assert.IsFalse(string.IsNullOrEmpty(guidA), $"test scene missing at {SceneAPath}");
            Assert.IsFalse(string.IsNullOrEmpty(guidB), $"test scene missing at {SceneBPath}");

            _entryA = _settings.CreateOrMoveEntry(guidA, group);
            _entryA.address = KeyA;
            _entryB = _settings.CreateOrMoveEntry(guidB, group);
            _entryB.address = KeyB;
        }

        [OneTimeTearDown]
        public void UnregisterAddressableTestScenes()
        {
            if (_settings == null) return;
            if (_entryA != null) _settings.RemoveAssetEntry(_entryA.guid);
            if (_entryB != null) _settings.RemoveAssetEntry(_entryB.guid);
            _settings.ActivePlayModeDataBuilderIndex = _originalPlayModeDataBuilderIndex;
        }
#endif

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o) Object.Destroy(o);
            _objects.Clear();
        }

        private T Track<T>(T o) where T : Object { _objects.Add(o); return o; }

        private static IEnumerator WaitForQueue(AddressablesSceneLoader loader)
        {
            float timeout = Time.realtimeSinceStartup + 15f;
            while (loader.IsLoading && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.IsFalse(loader.IsLoading, "the loader queue drained within the timeout.");
        }

        [UnityTest]
        public IEnumerator AutoActivate_LoadsSceneToCompletion_RaisingAllEventsInOrder()
        {
            var go = Track(new GameObject("addr-loader-auto"));
            var loader = go.AddComponent<AddressablesSceneLoader>();
            loader.AutoActivate = true;

            var events = new List<string>();
            loader.SceneLoadStarted   += k => events.Add($"started:{k}");
            loader.SceneLoadReady     += k => events.Add($"ready:{k}");
            loader.SceneLoadCompleted += k => events.Add($"completed:{k}");
            var progressSamples = new List<float>();
            loader.SceneLoadProgress += (_, p) => progressSamples.Add(p);

            loader.LoadScene(KeyA, LoadSceneMode.Single);
            Assert.IsTrue(loader.IsLoading, "loading starts synchronously within LoadScene.");

            yield return WaitForQueue(loader);

            Assert.AreEqual(SceneAName, SceneManager.GetActiveScene().name, "the Addressable key resolved to the right scene.");
            CollectionAssert.AreEqual(
                new[] { $"started:{KeyA}", $"ready:{KeyA}", $"completed:{KeyA}" }, events,
                "lifecycle events fire once, in order, keyed by the Addressable key.");
            Assert.IsTrue(progressSamples.Count > 0, "progress was reported at least once.");
            Assert.AreEqual(1f, progressSamples[progressSamples.Count - 1], "progress reaches 1 before completion.");
        }

        [UnityTest]
        public IEnumerator ManualActivation_HoldsSceneReady_UntilActivateReadySceneIsCalled()
        {
            var go = Track(new GameObject("addr-loader-manual"));
            var loader = go.AddComponent<AddressablesSceneLoader>();
            loader.AutoActivate = false;

            bool ready = false, completed = false;
            loader.SceneLoadReady     += _ => ready = true;
            loader.SceneLoadCompleted += _ => completed = true;

            loader.LoadScene(KeyB, LoadSceneMode.Additive);   // additive: never destroys the runner's own scene

            float timeout = Time.realtimeSinceStartup + 15f;
            while (!ready && Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.IsTrue(ready, "the load reached ready within the timeout.");
            Assert.IsFalse(completed, "activation is held open; the load must not complete yet.");
            Assert.IsTrue(loader.IsLoading, "still loading while activation is withheld.");
            Assert.IsFalse(SceneManager.GetSceneByName(SceneBName).isLoaded, "the scene has not activated yet.");

            loader.ActivateReadyScene();
            yield return WaitForQueue(loader);

            Assert.IsTrue(completed, "the load completed after manual activation.");
            Assert.IsTrue(SceneManager.GetSceneByName(SceneBName).isLoaded, "the scene is now loaded and active.");

            loader.UnloadScene(KeyB);   // clean up: leave a single-scene baseline for the next test
            yield return WaitForQueue(loader);
        }

        // NOTE on scene reuse across this fixture: AutoActivate_LoadsSceneToCompletion_RaisingAllEventsInOrder
        // Single-loads KeyA — which, by Single-mode's own contract, keeps AddressablesSceneA loaded for the
        // rest of the play session (there is no "restore" from a Single load; PlayMode tests share one
        // session). Every OTHER test below therefore uses KeyB exclusively, load-then-unload within itself,
        // so none of them depend on execution order — NUnit does not guarantee one.

        [UnityTest]
        public IEnumerator BackToBackLoads_AreQueued_NotDropped()
        {
            var go = Track(new GameObject("addr-loader-queue"));
            var loader = go.AddComponent<AddressablesSceneLoader>();

            var events = new List<string>();
            loader.SceneLoadCompleted   += k => events.Add($"loaded:{k}");
            loader.SceneUnloadCompleted += k => events.Add($"unloaded:{k}");

            loader.LoadScene(KeyB, LoadSceneMode.Additive);
            loader.UnloadScene(KeyB);   // issued while the load is still in flight: must queue, not drop or reorder
            Assert.IsTrue(loader.IsLoading);

            yield return WaitForQueue(loader);

            CollectionAssert.AreEqual(new[] { $"loaded:{KeyB}", $"unloaded:{KeyB}" }, events, "both queued operations completed, in FIFO order.");
            Assert.IsFalse(SceneManager.GetSceneByName(SceneBName).isLoaded, "the scene ends up unloaded — the queued unload was not dropped.");
        }

        [UnityTest]
        public IEnumerator UnloadScene_RoundTrips_AndUnknownKeyLogsGracefulError()
        {
            var go = Track(new GameObject("addr-loader-unload"));
            var loader = go.AddComponent<AddressablesSceneLoader>();

            loader.LoadScene(KeyB, LoadSceneMode.Additive);
            yield return WaitForQueue(loader);
            Assert.IsTrue(SceneManager.GetSceneByName(SceneBName).isLoaded);

            loader.UnloadScene(KeyB);
            yield return WaitForQueue(loader);
            Assert.IsFalse(SceneManager.GetSceneByName(SceneBName).isLoaded, "the scene is unloaded.");

            string failedKey = null, failedReason = null;
            loader.SceneUnloadFailed += (k, r) => { failedKey = k; failedReason = r; };

            LogAssert.Expect(LogType.Error, $"[GraphGameFlow] Scene '{KeyB}' was not loaded by this AddressablesSceneLoader; unload ignored.");
            loader.UnloadScene(KeyB);   // already unloaded through this loader — no handle on record
            yield return WaitForQueue(loader);

            Assert.AreEqual(KeyB, failedKey, "the failure event names which key failed — not just a log line easy to miss.");
            StringAssert.Contains("was not loaded", failedReason, "the failure event explains why.");
        }

        [UnityTest]
        public IEnumerator LoadScene_UnknownKey_RaisesFailureEventAndSignal()
        {
            var go = Track(new GameObject("addr-loader-load-fail"));
            var loader = go.AddComponent<AddressablesSceneLoader>();

            string failedKey = null, failedReason = null;
            loader.SceneLoadFailed += (k, r) => { failedKey = k; failedReason = r; };

            LogAssert.ignoreFailingMessages = true;   // Addressables itself also logs its own InvalidKeyException line
            loader.LoadScene("AddrTest.KeyThatWasNeverRegistered", LoadSceneMode.Additive);
            yield return WaitForQueue(loader);
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual("AddrTest.KeyThatWasNeverRegistered", failedKey, "the failure event names which key failed.");
            Assert.IsFalse(string.IsNullOrEmpty(failedReason), "the failure event carries a reason, not an empty string.");
        }

        [UnityTest]
        public IEnumerator LoadScene_Failure_ResumesADriverAwaitingEitherCompletedOrFailedSignal()
        {
            // The escape hatch this test exists to prove: a node awaiting BOTH the completed AND the failed
            // signal (AwaitSignalNames — logical OR) resumes on a failure instead of parking forever.
            var loadedSig = Track(SignalDef.Create("addr-load-ok"));
            var failedSig = Track(SignalDef.Create("addr-load-failed"));

            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var gate  = new StatementNodeData { Id = "gate", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = (string)loadedSig };
            gate.AwaitSignalNamesExtra.Add((string)failedSig);
            var end   = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(start); g.AddNode(gate); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });

            var driverGo = Track(new GameObject("addr-fail-resume-driver"));
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = g;

            var loaderGo = Track(new GameObject("addr-fail-resume-loader"));
            var loader = loaderGo.AddComponent<AddressablesSceneLoader>();
            loader.LoadCompletedSignal = loadedSig;
            loader.LoadFailedSignal    = failedSig;
            loader.SignalDriver        = driver;
            driver.SceneLoader          = loader;

            bool ended = false;
            driver.OnEnded += _ => ended = true;

            driver.Boot();
            Assert.IsTrue(driver.IsWaitingForSignal, "parked on the gate node.");

            LogAssert.ignoreFailingMessages = true;
            loader.LoadScene("AddrTest.AnotherKeyThatWasNeverRegistered", LoadSceneMode.Additive);
            yield return WaitForQueue(loader);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(ended, "the failed-load signal resumed the parked flow via the OR-await, instead of stalling it.");
        }

        [UnityTest]
        public IEnumerator LoadCompletedSignal_ResumesAwaitingDriver()
        {
            var loadedSig = Track(SignalDef.Create("addr-scene-ready"));

            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var gate  = new StatementNodeData { Id = "gate", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = (string)loadedSig };
            var end   = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(start); g.AddNode(gate); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });

            var driverGo = Track(new GameObject("addr-signal-driver"));
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = g;

            var loaderGo = Track(new GameObject("addr-signal-loader"));
            var loader = loaderGo.AddComponent<AddressablesSceneLoader>();
            loader.LoadCompletedSignal = loadedSig;
            loader.SignalDriver = driver;
            driver.SceneLoader = loader;

            bool ended = false;
            driver.OnEnded += _ => ended = true;

            driver.Boot();
            Assert.IsTrue(driver.IsWaitingForSignal && driver.CurrentAwaitSignal == (string)loadedSig);

            loader.LoadScene(KeyB, LoadSceneMode.Additive);
            yield return WaitForQueue(loader);

            Assert.IsTrue(ended, "the load's completion signal resumed the parked await and completed the flow.");

            loader.UnloadScene(KeyB);
            yield return WaitForQueue(loader);
        }

        [UnityTest]
        public IEnumerator PauseDriverWhileLoading_HoldsTimedWait_UntilQueueDrains()
        {
            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var wait = new StatementNodeData { Id = "wait", NodeType = StatementNodeData.NodeTypeId, WaitDuration = 30f };
            var end  = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId });
            g.AddNode(wait); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "wait" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "wait", ToNodeId = "end" });

            var driverGo = Track(new GameObject("addr-pause-driver"));
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = g;

            var loaderGo = Track(new GameObject("addr-pause-loader"));
            var loader = loaderGo.AddComponent<AddressablesSceneLoader>();
            loader.PauseDriverWhileLoading = true;
            loader.SignalDriver = driver;
            driver.SceneLoader = loader;

            driver.Boot();
            Assert.IsTrue(driver.IsWaitingForTime);
            yield return null;
            yield return null;
            Assert.Less(driver.WaitRemaining, 30f, "the wait ticks while unpaused.");

            loader.LoadScene(KeyB, LoadSceneMode.Additive);   // additive: the test scene and driver stay alive
            Assert.IsTrue(driver.Paused, "the loader paused the driver synchronously with the first request.");
            float frozen = driver.WaitRemaining;

            float timeout = Time.realtimeSinceStartup + 15f;
            while (loader.IsLoading && Time.realtimeSinceStartup < timeout)
            {
                Assert.AreEqual(frozen, driver.WaitRemaining, 0.0001f, "the timed wait must hold while the queue is busy.");
                yield return null;
            }

            Assert.IsFalse(loader.IsLoading);
            Assert.IsFalse(driver.Paused, "the loader resumed the driver when the queue drained.");
            yield return null;
            yield return null;
            Assert.Less(driver.WaitRemaining, frozen, "the timed wait resumed ticking after the load.");

            loader.UnloadScene(KeyB);
            yield return WaitForQueue(loader);
        }

        [UnityTest]
        public IEnumerator LoadSceneAction_InstantFailure_ResumesLiveWithoutResumeIfAlreadyRaised()
        {
            // The ACTUAL trap this test guards against (found via independent testing, not the driver-
            // external-trigger shape of LoadCompletedSignal_ResumesAwaitingDriver above): a LoadSceneAction
            // on a node's OnEnterActions, immediately followed — in the SAME synchronous auto-advance pass —
            // by a node awaiting completed-OR-failed. An invalid Addressable key can resolve (and fail)
            // synchronously, before the runner had even entered the awaiting node; without the one-frame
            // defer in LoadRoutine's failure branch, the signal was gone by the time anything could catch it
            // live. ResumeIfSignalAlreadyRaised is deliberately left OFF here to prove the live path alone
            // is now enough.
            var loadedSig = Track(SignalDef.Create("addr-trap-ok"));
            var failedSig = Track(SignalDef.Create("addr-trap-failed"));

            var g = Track(ScriptableObject.CreateInstance<GameFlowGraph>());
            g.EntryNodeId = "start";
            var start    = new StartNodeData { Id = "start", NodeType = StartNodeData.NodeTypeId };
            var loadNode = new StatementNodeData { Id = "load", NodeType = StatementNodeData.NodeTypeId };
            var loadAction = Track(ScriptableObject.CreateInstance<LoadSceneAction>());
            loadAction.SceneName = "AddrTest.TrapTestBadKey";
            loadAction.Mode = LoadSceneMode.Additive;
            loadNode.OnEnterActions.Add(loadAction);
            var gate = new StatementNodeData { Id = "gate", NodeType = StatementNodeData.NodeTypeId, AwaitSignalName = (string)loadedSig };
            gate.AwaitSignalNamesExtra.Add((string)failedSig);
            var end = new EndNodeData { Id = "end", NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed };
            g.AddNode(start); g.AddNode(loadNode); g.AddNode(gate); g.AddNode(end);
            g.AddEdge(new BaseEdgeData { FromNodeId = "start", ToNodeId = "load" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "load", ToNodeId = "gate" });
            g.AddEdge(new BaseEdgeData { FromNodeId = "gate", ToNodeId = "end" });

            var driverGo = Track(new GameObject("addr-trap-driver"));
            var driver = driverGo.AddComponent<GraphFlowDriver>();
            driver.BootOnStart = false;
            driver.Graph = g;

            var loaderGo = Track(new GameObject("addr-trap-loader"));
            var loader = loaderGo.AddComponent<AddressablesSceneLoader>();
            loader.LoadCompletedSignal = loadedSig;
            loader.LoadFailedSignal    = failedSig;
            loader.SignalDriver        = driver;
            driver.SceneLoader          = loader;

            bool ended = false;
            driver.OnEnded += _ => ended = true;

            LogAssert.ignoreFailingMessages = true;   // Addressables itself also logs its own InvalidKeyException line
            driver.Boot();   // start -> load (OnEnterActions runs the failing LoadSceneAction) -> gate (parks), all synchronous within this one call

            Assert.IsTrue(driver.IsWaitingForSignal, "parked on the gate node — the load's own OnEnterActions already ran.");

            yield return WaitForQueue(loader);   // the deferred failure signal fires once the loader's coroutine resolves
            LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(ended, "resumed via the LIVE failure signal — no ResumeIfSignalAlreadyRaised needed.");
        }

        [UnityTest]
        public IEnumerator StuckOperationWarning_FiresOnceAfterThreshold_WhenHeldOpenByManualActivation()
        {
            var go = Track(new GameObject("addr-loader-stuck"));
            var loader = go.AddComponent<AddressablesSceneLoader>();
            loader.AutoActivate = false;
            loader.StuckOperationWarningAfter = 0.001f;   // effectively "any real delay at all"

            var fired = new List<(string Key, float Elapsed)>();
            loader.OperationTakingTooLong += (k, e) => fired.Add((k, e));

            loader.LoadScene(KeyB, LoadSceneMode.Additive);

            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(1, fired.Count, "the warning fires exactly once, however many frames elapse past the threshold.");
            Assert.AreEqual(KeyB, fired[0].Key);
            Assert.Greater(fired[0].Elapsed, 0f);

            loader.ActivateReadyScene();
            yield return WaitForQueue(loader);

            loader.UnloadScene(KeyB);
            yield return WaitForQueue(loader);
        }

        [UnityTest]
        public IEnumerator StuckOperationWarning_Disabled_NeverFires()
        {
            var go = Track(new GameObject("addr-loader-stuck-disabled"));
            var loader = go.AddComponent<AddressablesSceneLoader>();
            loader.AutoActivate = false;
            loader.StuckOperationWarningAfter = 0f;   // 0 or less disables it

            bool fired = false;
            loader.OperationTakingTooLong += (_, __) => fired = true;

            loader.LoadScene(KeyB, LoadSceneMode.Additive);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsFalse(fired, "disabled (<=0) must never fire, regardless of how long the operation is held open.");

            loader.ActivateReadyScene();
            yield return WaitForQueue(loader);

            loader.UnloadScene(KeyB);
            yield return WaitForQueue(loader);
        }
    }
}
