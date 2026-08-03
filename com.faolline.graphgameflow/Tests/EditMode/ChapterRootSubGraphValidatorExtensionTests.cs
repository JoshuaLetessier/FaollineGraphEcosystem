using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow.Editor;

namespace Faolline.GraphGameFlow.Tests
{
    /// <summary>
    /// Tests the class directly (mirrors <c>AddressablesSceneKeyProviderTests</c>'s pattern) rather than through
    /// the shared <see cref="GraphValidatorExtensionRegistry.Extensions"/>/<see cref="GraphKeySourceRegistry.Providers"/>
    /// singletons, so it stays independent of whatever else self-registered via [InitializeOnLoadMethod] in this
    /// project/session.
    /// </summary>
    public class ChapterRootSubGraphValidatorExtensionTests
    {
        private const string TestFolder = "Assets/__ChapterRootSubGraphValidatorExtensionTests__";
        private readonly List<string> _assetPaths = new List<string>();

        private sealed class FakeKeyProvider : IGraphKeySourceProvider
        {
            public string PromotedGuid;
            public string Key = "chapter-2";
            public string SourceLabel => "Fake";
            public IReadOnlyList<string> GetKeys() => new[] { Key };
            public bool CanPromote(string graphAssetPath, string graphId) => true;
            public void Promote(string graphAssetPath, string graphId) { }
            public bool TryResolveGuid(string assetGuid, out string key)
            {
                key = Key;
                return assetGuid == PromotedGuid;
            }
        }

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "__ChapterRootSubGraphValidatorExtensionTests__");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _assetPaths) AssetDatabase.DeleteAsset(path);
            _assetPaths.Clear();
            AssetDatabase.DeleteAsset(TestFolder);
            foreach (var p in new List<IGraphKeySourceProvider>(GraphKeySourceRegistry.Providers))
                GraphKeySourceRegistry.Unregister(p);
        }

        private BaseGraph CreatePersistedGraph(string name)
        {
            var graph = ScriptableObject.CreateInstance<BaseGraph>();
            var path = $"{TestFolder}/{name}.asset";
            AssetDatabase.CreateAsset(graph, path);
            _assetPaths.Add(path);
            return graph;
        }

        [Test]
        public void CheckSubGraphTarget_Null_ReturnsNull()
        {
            var ext = new ChapterRootSubGraphValidatorExtension();
            Assert.IsNull(ext.CheckSubGraphTarget(null));
        }

        [Test]
        public void CheckSubGraphTarget_NonPersistedGraph_ReturnsNull()
        {
            var target = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var ext = new ChapterRootSubGraphValidatorExtension();
                Assert.IsNull(ext.CheckSubGraphTarget(target), "an in-memory, never-saved graph cannot be a registered chapter root.");
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void CheckSubGraphTarget_PersistedButNotPromoted_ReturnsNull()
        {
            var target = CreatePersistedGraph("NotPromoted");
            var provider = new FakeKeyProvider { PromotedGuid = "some-other-guid" };
            GraphKeySourceRegistry.Register(provider);

            var ext = new ChapterRootSubGraphValidatorExtension();
            Assert.IsNull(ext.CheckSubGraphTarget(target));
        }

        [Test]
        public void CheckSubGraphTarget_PromotedChapterRoot_ReturnsMessage()
        {
            var target = CreatePersistedGraph("PromotedChapter");
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            var provider = new FakeKeyProvider { PromotedGuid = guid, Key = "chapter-2" };
            GraphKeySourceRegistry.Register(provider);

            var ext = new ChapterRootSubGraphValidatorExtension();
            var message = ext.CheckSubGraphTarget(target);

            Assert.IsNotNull(message);
            StringAssert.Contains("chapter-2", message);
        }
    }
}
