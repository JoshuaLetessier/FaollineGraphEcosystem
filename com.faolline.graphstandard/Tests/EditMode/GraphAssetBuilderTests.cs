using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphStandard;
using Faolline.GraphStandard.Editor;

namespace Faolline.GraphStandard.Tests
{
    /// <summary>US2 — the editor persist utility writes a graph asset with its attached actions as
    /// sub-assets, so the asset is self-contained.</summary>
    public class GraphAssetBuilderTests
    {
        private sealed class NoOpAction : BaseAction { public override void Execute(BaseContext c) { } }

        private string _path;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_path)) { AssetDatabase.DeleteAsset(_path); _path = null; }
        }

        [Test]
        public void Save_StoresAttachedActionsAsSubAssets_AndReloadsIntact()
        {
            var action = ScriptableObject.CreateInstance<NoOpAction>();
            action.name = "Act";

            var b = new GraphBuilder<BaseGraph>();
            var start = b.AddStart().AsEntry();
            var stmt  = b.AddStatement("s").OnEnter(action);
            var end   = b.AddEnd();
            start.To(stmt); stmt.To(end);
            var graph = b.Build();

            _path = "Assets/_GraphAssetBuilderTest.asset";
            GraphAssetBuilder.Save(graph, _path);

            Assert.IsTrue(AssetDatabase.Contains(action), "the in-memory action became a persisted sub-asset.");

            var loaded = AssetDatabase.LoadAllAssetsAtPath(_path);
            Assert.IsTrue(loaded.Any(o => o is BaseGraph), "the graph asset is at the path.");
            Assert.IsTrue(loaded.Any(o => o is NoOpAction), "the attached action is a sub-asset of the graph.");
        }
    }
}
