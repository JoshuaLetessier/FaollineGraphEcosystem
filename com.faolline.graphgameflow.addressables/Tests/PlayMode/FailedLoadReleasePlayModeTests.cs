using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;
using Faolline.GraphCore;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Faolline.GraphGameFlow.Addressables.Tests.PlayMode
{
    /// <summary>
    /// A failed Addressables load still holds the caller's reference on its operation until released — the
    /// operation is never destroyed otherwise. These tests route a key to <see cref="DeferredFailingProvider"/>
    /// (a test-only provider that fails on demand) through a test-only locator, so the real Addressables
    /// load/fail/release machinery runs end to end with no seam in the production classes.
    /// <para>
    /// How a release is observed: while the load is still in flight, a second <c>LoadAssetAsync</c> on the same
    /// key shares the SAME cached operation (one more reference). Once the failure has been handled, releasing
    /// that probe drops the last reference only if the class under test released its own — the probe handle
    /// then turns invalid (the operation was destroyed). If the class leaked its reference, the probe stays valid.
    /// </para>
    /// </summary>
    public sealed class FailedLoadReleasePlayModeTests
    {
        private const string GraphKey = "FailedLoadReleaseTest.Graph";
        // AssetReference keys are asset GUIDs; this one matches no asset, only the test locator below.
        private const string PreloadGuid = "fa11ed00fa11ed00fa11ed00fa11ed00";

        private DeferredFailingProvider _provider;
        private ResourceLocationMap     _locator;

#if UNITY_EDITOR
        private AddressableAssetSettings _settings;
        private int _originalPlayModeDataBuilderIndex;

        // Same Play Mode script as AddressablesSceneLoaderPlayModeTests, whichever fixture initialises
        // Addressables first: "Use Asset Database", no content build needed.
        [OneTimeSetUp]
        public void UseAssetDatabasePlayModeScript()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings
                        ?? AddressableAssetSettingsDefaultObject.GetSettings(true);
            _originalPlayModeDataBuilderIndex = _settings.ActivePlayModeDataBuilderIndex;
            _settings.ActivePlayModeDataBuilderIndex = _settings.DataBuilders.FindIndex(b => b is BuildScriptFastMode);
        }

        [OneTimeTearDown]
        public void RestorePlayModeScript()
        {
            if (_settings != null) _settings.ActivePlayModeDataBuilderIndex = _originalPlayModeDataBuilderIndex;
        }
#endif

        [UnitySetUp]
        public IEnumerator RegisterFailingKeys()
        {
            // Initialised first, so loads below hit the ResourceManager directly instead of being chained
            // behind initialisation (a chain wraps the shared operation and would hide its reference count).
            yield return global::UnityEngine.AddressableAssets.Addressables.InitializeAsync(autoReleaseHandle: true);

            _provider = new DeferredFailingProvider();
            global::UnityEngine.AddressableAssets.Addressables.ResourceManager.ResourceProviders.Add(_provider);

            _locator = new ResourceLocationMap("FailedLoadReleaseTestLocator", 2);
            _locator.Add(GraphKey,    new ResourceLocationBase(GraphKey,    GraphKey,    _provider.ProviderId, typeof(BaseGraph)));
            _locator.Add(PreloadGuid, new ResourceLocationBase(PreloadGuid, PreloadGuid, _provider.ProviderId, typeof(BaseGraph)));
            global::UnityEngine.AddressableAssets.Addressables.AddResourceLocator(_locator);
        }

        [TearDown]
        public void UnregisterFailingKeys()
        {
            LogAssert.ignoreFailingMessages = false;
            global::UnityEngine.AddressableAssets.Addressables.RemoveResourceLocator(_locator);
            global::UnityEngine.AddressableAssets.Addressables.ResourceManager.ResourceProviders.Remove(_provider);
        }

        [UnityTest]
        public IEnumerator GraphCatalog_FailedResolve_ReleasesItsHandle()
        {
            LogAssert.ignoreFailingMessages = true;   // the failure is logged by Addressables and by the catalog

            var catalog = new AddressablesGraphCatalog();
            string failure = null;
            catalog.Resolve(GraphKey, _ => Assert.Fail("the load was made to fail."), r => failure = r);
            Assert.AreEqual(1, _provider.PendingCount, "the catalog's load reached the test provider.");

            var probe = global::UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<BaseGraph>(GraphKey);
            Assert.AreEqual(1, _provider.PendingCount, "the probe shares the catalog's in-flight operation.");

            _provider.FailAll();
            yield return WaitFor(() => failure != null);
            Assert.IsNotNull(failure, "the catalog reported the failure.");

            global::UnityEngine.AddressableAssets.Addressables.Release(probe);
            Assert.IsFalse(probe.IsValid(),
                "after the probe's release the operation must be destroyed — if it is still alive, the catalog " +
                "kept its own reference to the failed load.");
        }

        [UnityTest]
        public IEnumerator PreloadNextChapter_FailedPreload_ReleasesTheReference_SoItCanBeRetried()
        {
            LogAssert.ignoreFailingMessages = true;   // the failure is logged by Addressables and by the action

            var action = ScriptableObject.CreateInstance<PreloadNextChapterAction>();
            try
            {
                action.NextChapter = new AssetReferenceT<BaseGraph>(PreloadGuid);
                action.Execute(null);
                Assert.IsTrue(action.NextChapter.OperationHandle.IsValid(), "the preload is in flight.");

                _provider.FailAll();
                yield return WaitFor(() => !action.NextChapter.OperationHandle.IsValid());
                Assert.IsFalse(action.NextChapter.OperationHandle.IsValid(),
                    "a failed preload must release the AssetReference's handle.");

                // Before the fix the AssetReference still held the failed handle here: LoadAssetAsync refused
                // ("already been loaded") and returned an invalid handle, and subscribing to it threw.
                Assert.DoesNotThrow(() => action.Execute(null));
                Assert.AreEqual(1, _provider.PendingCount, "the retry reached the provider again.");

                _provider.FailAll();
                yield return WaitFor(() => !action.NextChapter.OperationHandle.IsValid());
            }
            finally { UnityEngine.Object.Destroy(action); }
        }

        private static IEnumerator WaitFor(Func<bool> condition, int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && !condition(); i++)
                yield return null;
        }

        /// <summary>Holds every request open until <see cref="FailAll"/>, then fails them.</summary>
        private sealed class DeferredFailingProvider : ResourceProviderBase
        {
            private readonly List<ProvideHandle> _pending = new List<ProvideHandle>();

            // Unique per instance: the ResourceManager caches provider lookups by id and never evicts on
            // removal, so a fixed id would route the next test's loads to the previous test's instance.
            public DeferredFailingProvider() => m_ProviderId = $"{GetType().FullName}.{Guid.NewGuid():N}";

            public int PendingCount => _pending.Count;

            public override void Provide(ProvideHandle provideHandle) => _pending.Add(provideHandle);

            public void FailAll()
            {
                var pending = _pending.ToArray();
                _pending.Clear();
                foreach (var handle in pending)
                    handle.Complete<BaseGraph>(null, false, new Exception("FailedLoadReleasePlayModeTests: load made to fail."));
            }
        }
    }
}
