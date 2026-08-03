using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// <see cref="GraphLinkNodeData.TargetGraph"/> is now GUID-backed (<see cref="GraphLinkNodeData.TargetGraphGuid"/>)
    /// instead of a hard serialized <see cref="BaseGraph"/> reference, so a documentary link no longer forces
    /// its target into the owning graph's <c>AssetDatabase</c> dependency closure — the whole point of this
    /// feature (spec 047, Lot 1). The public <c>TargetGraph</c> property signature is unchanged.
    /// </summary>
    public class GraphLinkSoftReferenceTests
    {
        private const string TestFolder = "Assets/__GraphLinkSoftReferenceTests__";
        private readonly List<string> _assetPaths = new List<string>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "__GraphLinkSoftReferenceTests__");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _assetPaths) AssetDatabase.DeleteAsset(path);
            _assetPaths.Clear();
            AssetDatabase.DeleteAsset(TestFolder);
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
        public void AssignPersistedAsset_RoundTripsThroughGuid()
        {
            var target = CreatePersistedGraph("Target");
            var link = new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId };

            link.TargetGraph = target;

            Assert.AreEqual(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)), link.TargetGraphGuid,
                "assigning a persisted asset stores its GUID.");
            Assert.AreSame(target, link.TargetGraph, "the property resolves back to the same asset.");
        }

        [Test]
        public void ClearingTarget_ClearsGuid()
        {
            var target = CreatePersistedGraph("Target");
            var link = new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId, TargetGraph = target };

            link.TargetGraph = null;

            Assert.IsTrue(string.IsNullOrEmpty(link.TargetGraphGuid));
            Assert.IsNull(link.TargetGraph);
        }

        [Test]
        public void NeverAssigned_ResolvesToNull_AndEmptyGuid()
        {
            var link = new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId };

            Assert.IsTrue(string.IsNullOrEmpty(link.TargetGraphGuid));
            Assert.IsNull(link.TargetGraph);
        }

        [Test]
        public void NonPersistedInstance_StillRoundTripsWithinSession()
        {
            // Existing tests (GraphLinkInspectorTests) assign an in-memory, never-saved BaseGraph instance —
            // that must keep working identically; only a REAL asset gets a durable GUID.
            var target = ScriptableObject.CreateInstance<BaseGraph>();
            try
            {
                var link = new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId, TargetGraph = target };
                Assert.AreSame(target, link.TargetGraph);
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void OwnerGraphDependencies_DoNotIncludeGraphLinkTarget()
        {
            var target = CreatePersistedGraph("Target");
            var owner = CreatePersistedGraph("Owner");
            owner.AddNode(new GraphLinkNodeData { Id = "link", NodeType = GraphLinkNodeData.NodeTypeId, TargetGraph = target });
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();

            var deps = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(owner), recursive: true);

            Assert.IsFalse(deps.Contains(AssetDatabase.GetAssetPath(target)),
                "a GraphLink is documentary-only — its target must not appear in the owner's build/asset dependency closure.");
        }
    }
}
