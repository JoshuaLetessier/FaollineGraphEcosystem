using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
    /// <see cref="IGraphCatalog.Release"/> on <see cref="AddressablesGraphCatalog"/>, called through the interface:
    /// one call releases every successful resolve made with that key. The key is routed to
    /// <see cref="InstantProvider"/> (test-only, succeeds at once) through a test-only locator. A release is
    /// observed with a probe handle on the same cached operation, as in <see cref="FailedLoadReleasePlayModeTests"/>:
    /// releasing the probe destroys the operation only if the catalog released all of its own references.
    /// </summary>
    public sealed class GraphCatalogReleasePlayModeTests
    {
        private const string GraphKey = "GraphCatalogReleaseTest.Graph";

        private BaseGraph           _graph;
        private InstantProvider     _provider;
        private ResourceLocationMap _locator;

#if UNITY_EDITOR
        private AddressableAssetSettings _settings;
        private int _originalPlayModeDataBuilderIndex;

        // Same Play Mode script as the other fixtures of this package, whichever initialises Addressables first.
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
        public IEnumerator RegisterKey()
        {
            // Initialised first, so loads below hit the ResourceManager directly instead of being chained
            // behind initialisation (a chain wraps the shared operation and would hide its reference count).
            yield return global::UnityEngine.AddressableAssets.Addressables.InitializeAsync(autoReleaseHandle: true);

            _graph = ScriptableObject.CreateInstance<BaseGraph>();
            _provider = new InstantProvider(_graph);
            global::UnityEngine.AddressableAssets.Addressables.ResourceManager.ResourceProviders.Add(_provider);

            _locator = new ResourceLocationMap("GraphCatalogReleaseTestLocator", 1);
            _locator.Add(GraphKey, new ResourceLocationBase(GraphKey, GraphKey, _provider.ProviderId, typeof(BaseGraph)));
            global::UnityEngine.AddressableAssets.Addressables.AddResourceLocator(_locator);
        }

        [TearDown]
        public void UnregisterKey()
        {
            global::UnityEngine.AddressableAssets.Addressables.RemoveResourceLocator(_locator);
            global::UnityEngine.AddressableAssets.Addressables.ResourceManager.ResourceProviders.Remove(_provider);
            UnityEngine.Object.Destroy(_graph);
        }

        [UnityTest]
        public IEnumerator Release_ThroughTheInterface_ReleasesEveryResolveOfTheKey()
        {
            IGraphCatalog catalog = new AddressablesGraphCatalog();
            var resolved = new List<BaseGraph>();

            catalog.Resolve(GraphKey, resolved.Add, r => Assert.Fail(r));
            yield return WaitFor(() => resolved.Count == 1);
            catalog.Resolve(GraphKey, resolved.Add, r => Assert.Fail(r));   // same key again, e.g. a re-warp onto the current graph
            yield return WaitFor(() => resolved.Count == 2);

            Assert.AreEqual(2, resolved.Count, "both resolves succeeded.");
            Assert.AreSame(_graph, resolved[0]);
            Assert.AreSame(_graph, resolved[1]);

            var probe = global::UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<BaseGraph>(GraphKey);
            yield return WaitFor(() => probe.IsDone);
            Assert.AreEqual(1, _provider.ProvideCount, "the second resolve and the probe share the first one's cached operation.");

            catalog.Release(GraphKey);
            Assert.IsTrue(probe.IsValid(), "the probe's own reference keeps the operation alive.");

            global::UnityEngine.AddressableAssets.Addressables.Release(probe);
            Assert.IsFalse(probe.IsValid(),
                "after the probe's release the operation must be destroyed — if it is still alive, the catalog's " +
                "Release left one of the two resolves held.");
        }

        private static IEnumerator WaitFor(Func<bool> condition, int maxFrames = 120)
        {
            for (int i = 0; i < maxFrames && !condition(); i++)
                yield return null;
        }

        /// <summary>Succeeds every request at once with the same graph, and counts them.</summary>
        private sealed class InstantProvider : ResourceProviderBase
        {
            private readonly BaseGraph _graph;

            // Unique per instance: the ResourceManager caches provider lookups by id and never evicts on removal.
            public InstantProvider(BaseGraph graph)
            {
                _graph = graph;
                m_ProviderId = $"{GetType().FullName}.{Guid.NewGuid():N}";
            }

            public int ProvideCount { get; private set; }

            public override void Provide(ProvideHandle provideHandle)
            {
                ProvideCount++;
                provideHandle.Complete(_graph, true, null);
            }
        }
    }
}
