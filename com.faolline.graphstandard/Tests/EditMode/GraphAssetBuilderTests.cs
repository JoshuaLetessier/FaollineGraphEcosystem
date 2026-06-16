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
        private sealed class NoOpCondition : BaseCondition { public override bool Evaluate(BaseContext c) => true; }
        // A node subclass with its OWN condition field (like a quest objective's CompletionCondition).
        private sealed class CustomNode : BaseNodeData { public BaseCondition Completion; }
        // A graph subclass with a graph-level action field (like a quest's CompletionReward).
        private sealed class CustomGraph : BaseGraph { public BaseAction Reward; }

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

        [Test]
        public void Save_SweepsSubclassNodeFields_AndGraphLevelFields_Generically()
        {
            var nodeCondition = ScriptableObject.CreateInstance<NoOpCondition>(); nodeCondition.name = "NodeCond";
            var graphReward   = ScriptableObject.CreateInstance<NoOpAction>();    graphReward.name   = "GraphReward";

            var graph = ScriptableObject.CreateInstance<CustomGraph>();
            graph.Reward = graphReward;                                   // a graph-level BaseAction field
            graph.AddNode(new CustomNode { Id = "n", NodeType = "custom", Completion = nodeCondition }); // a node BaseCondition field

            _path = "Assets/_GraphAssetBuilderCustomTest.asset";
            GraphAssetBuilder.Save(graph, _path);

            Assert.IsTrue(AssetDatabase.Contains(nodeCondition),
                "a node subclass's own BaseCondition field is swept as a sub-asset (not just OnEnter/Exit/Entry).");
            Assert.IsTrue(AssetDatabase.Contains(graphReward),
                "a graph-level BaseAction field is swept as a sub-asset.");
        }
    }
}
