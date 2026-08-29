using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Duplicating an <see cref="IStableGuidIdentity"/> asset (Ctrl+D, or a file copy) copies its serialized
    /// stable-id field — the detector must find the shared id and regenerate the duplicate's, keeping the
    /// original's intact, scoped separately per concrete type (<see cref="BaseGraph"/>,
    /// <see cref="CollectionEntry"/>, <see cref="CollectionDef"/>).
    /// </summary>
    public class StableIdDuplicateDetectorTests
    {
        private class StubNodeData : BaseNodeData { }

        private const string TempFolder = "Assets/Temp_StableIdDetectorTest";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "Temp_StableIdDetectorTest");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TempFolder);

        private static T CreateAt<T>(string fileName) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, $"{TempFolder}/{fileName}.asset");
            return asset;
        }

        // Simulates the Ctrl+D outcome: force b's serialized id field to carry a's id (in memory — no
        // save, so the import postprocessor does not pre-fix it and the test exercises ScanAndFix
        // deterministically).
        private static void ForceSameId(ScriptableObject a, ScriptableObject b, string fieldName)
        {
            var idA = ((IStableGuidIdentity)a).StableId;
            var so = new SerializedObject(b);
            so.FindProperty(fieldName).stringValue = idA;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(idA, ((IStableGuidIdentity)b).StableId, "setup: the two assets share one id");
        }

        // ── BaseGraph ─────────────────────────────────────────────────────

        [Test]
        public void BaseGraph_ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var a = CreateAt<BaseGraph>("GraphA");
            var b = CreateAt<BaseGraph>("GraphB");
            ForceSameId(a, b, "_graphId");
            var originalId = a.GraphId;

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate BaseGraph id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);

            Assert.AreEqual(1, fixedCount);
            Assert.AreNotEqual(a.GraphId, b.GraphId, "the duplicate pair must end up with distinct ids");
            Assert.IsTrue(a.GraphId == originalId || b.GraphId == originalId,
                "exactly one of the two keeps the original id");
        }

        [Test]
        public void BaseGraph_ScanAndFix_PreferRegenerate_TheImportedCopyLosesTheTie()
        {
            var a = CreateAt<BaseGraph>("GraphA");
            var b = CreateAt<BaseGraph>("GraphB");
            ForceSameId(a, b, "_graphId");
            var originalId = a.GraphId;
            var pathB = AssetDatabase.GetAssetPath(b);

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate BaseGraph id"));
            StableIdDuplicateDetector.ScanAndFix(new HashSet<string> { pathB });

            Assert.AreEqual(originalId, a.GraphId, "the pre-existing asset keeps its id");
            Assert.AreNotEqual(originalId, b.GraphId, "the imported copy gets a fresh id");
        }

        [Test]
        public void NodeIds_ScanAndFix_RegeneratesDuplicateNodeIds_EvenWhenGraphIdsAlreadyDiffer()
        {
            // The real-world case: two graphs whose OWN ids are already distinct (e.g. auto-fixed on an
            // earlier import, long before their nodes ever got the same treatment), but whose embedded
            // nodes still carry duplicated ids. A graph-id-only scan finds nothing here — node ids must be
            // checked directly, independent of whether the containing graphs collide.
            var a = CreateAt<BaseGraph>("GraphA");
            var b = CreateAt<BaseGraph>("GraphB");
            Assert.AreNotEqual(a.GraphId, b.GraphId, "setup: the graphs' own ids are already distinct");

            var nodeAId = System.Guid.NewGuid().ToString("D");
            var nodeBId = System.Guid.NewGuid().ToString("D");
            foreach (var g in new[] { a, b })
            {
                g.AddNode(new StubNodeData { Id = nodeAId, NodeType = "test" });
                g.AddNode(new StubNodeData { Id = nodeBId, NodeType = "test" });
                g.AddEdge(new BaseEdgeData { FromNodeId = nodeAId, ToNodeId = nodeBId });
                g.AddGroup(new GraphGroupData { NodeIds = { nodeAId, nodeBId } });
                g.EntryNodeId = nodeAId;
            }

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate node id"));
            LogAssert.Expect(LogType.Warning, new Regex("Duplicate node id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);
            Assert.AreEqual(2, fixedCount, "both duplicated node ids (nodeA and nodeB) get regenerated");

            // The keeper (first found) keeps its original node ids; the other graph's nodes are regenerated.
            var regenerated = a.Nodes[0].Id != nodeAId ? a : b;
            var keeper = regenerated == a ? b : a;

            Assert.AreEqual(nodeAId, keeper.Nodes[0].Id, "the keeper's node ids are untouched");
            Assert.AreEqual(nodeBId, keeper.Nodes[1].Id, "the keeper's node ids are untouched");

            var newNodeAId = regenerated.Nodes[0].Id;
            var newNodeBId = regenerated.Nodes[1].Id;
            Assert.AreNotEqual(nodeAId, newNodeAId);
            Assert.AreNotEqual(nodeBId, newNodeBId);
            Assert.AreNotEqual(newNodeAId, newNodeBId, "the two regenerated node ids must not collide with each other either");

            Assert.AreEqual(newNodeAId, regenerated.EntryNodeId, "entry node reference remapped");
            Assert.AreEqual(newNodeAId, regenerated.Edges[0].FromNodeId, "edge source remapped");
            Assert.AreEqual(newNodeBId, regenerated.Edges[0].ToNodeId, "edge target remapped");
            CollectionAssert.AreEquivalent(new[] { newNodeAId, newNodeBId }, regenerated.Groups[0].NodeIds, "group node list remapped");
        }

        [Test]
        public void NodeIds_ScanAndFix_NoDuplicateNodeIds_IsANoOp()
        {
            var a = CreateAt<BaseGraph>("GraphA");
            a.AddNode(new StubNodeData { Id = System.Guid.NewGuid().ToString("D"), NodeType = "test" });

            Assert.AreEqual(0, StableIdDuplicateDetector.ScanAndFix(null));
            LogAssert.NoUnexpectedReceived();
        }

        // ── BaseEdgeData ──────────────────────────────────────────────────

        [Test]
        public void EdgeIds_ScanAndFix_RegeneratesDuplicateEdgeIds_EvenWhenGraphIdsAlreadyDiffer()
        {
            var a = CreateAt<BaseGraph>("GraphA");
            var b = CreateAt<BaseGraph>("GraphB");

            var edgeId = System.Guid.NewGuid().ToString("D");
            foreach (var g in new[] { a, b })
                g.AddEdge(new BaseEdgeData { Id = edgeId, FromNodeId = "from", ToNodeId = "to" });

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate edge id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);
            Assert.AreEqual(1, fixedCount);

            var regenerated = a.Edges[0].Id != edgeId ? a : b;
            var keeper = regenerated == a ? b : a;

            Assert.AreEqual(edgeId, keeper.Edges[0].Id, "the keeper's edge id is untouched");
            Assert.AreNotEqual(edgeId, regenerated.Edges[0].Id, "the duplicate's edge id was regenerated");
        }

        // ── GraphGroupData ────────────────────────────────────────────────

        [Test]
        public void GroupIds_ScanAndFix_RegeneratesDuplicateGroupIds_EvenWhenGraphIdsAlreadyDiffer()
        {
            var a = CreateAt<BaseGraph>("GraphA");
            var b = CreateAt<BaseGraph>("GraphB");

            var groupId = System.Guid.NewGuid().ToString("D");
            foreach (var g in new[] { a, b })
                g.AddGroup(new GraphGroupData { Id = groupId });

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate group id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);
            Assert.AreEqual(1, fixedCount);

            var regenerated = a.Groups[0].Id != groupId ? a : b;
            var keeper = regenerated == a ? b : a;

            Assert.AreEqual(groupId, keeper.Groups[0].Id, "the keeper's group id is untouched");
            Assert.AreNotEqual(groupId, regenerated.Groups[0].Id, "the duplicate's group id was regenerated");
        }

        // ── Cross-kind scoping ────────────────────────────────────────────

        [Test]
        public void NodeIdAndEdgeId_SharingTheSameIdString_AreNotFlaggedAgainstEachOther()
        {
            // A node and an edge that happen to carry the exact same GUID string, in the same graph, are
            // never compared to each other — ids are scoped per kind (node/edge/group), same rule as the
            // per-asset-type scan above.
            var a = CreateAt<BaseGraph>("GraphA");
            var sharedId = System.Guid.NewGuid().ToString("D");
            a.AddNode(new StubNodeData { Id = sharedId, NodeType = "test" });
            a.AddEdge(new BaseEdgeData { Id = sharedId, FromNodeId = "x", ToNodeId = "y" });

            Assert.AreEqual(0, StableIdDuplicateDetector.ScanAndFix(null));
            LogAssert.NoUnexpectedReceived();
        }

        // ── CollectionEntry ───────────────────────────────────────────────

        [Test]
        public void CollectionEntry_ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var a = CreateAt<CollectionEntry>("EntryA");
            var b = CreateAt<CollectionEntry>("EntryB");
            ForceSameId(a, b, "_id");
            var originalId = a.Key;

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate CollectionEntry id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);

            Assert.AreEqual(1, fixedCount);
            Assert.AreNotEqual(a.Key, b.Key);
            Assert.IsTrue(a.Key == originalId || b.Key == originalId);
        }

        // ── CollectionDef ────────────────────────────────────────────────

        [Test]
        public void CollectionDef_ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var a = CreateAt<CollectionDef>("ColA");
            var b = CreateAt<CollectionDef>("ColB");
            ForceSameId(a, b, "_id");
            var originalId = a.Key;

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate CollectionDef id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);

            Assert.AreEqual(1, fixedCount);
            Assert.AreNotEqual(a.Key, b.Key);
            Assert.IsTrue(a.Key == originalId || b.Key == originalId);
        }

        // ── SignalDef ────────────────────────────────────────────────────

        [Test]
        public void SignalName_ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var a = CreateAt<SignalDef>("SigA");
            var b = CreateAt<SignalDef>("SigB");
            ForceSameId(a, b, "_id");
            var originalId = a.Key;

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate SignalDef id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);

            Assert.AreEqual(1, fixedCount);
            Assert.AreNotEqual(a.Key, b.Key);
            Assert.IsTrue(a.Key == originalId || b.Key == originalId);
        }

        // ── Cross-type scoping ────────────────────────────────────────────

        [Test]
        public void DifferentTypes_SharingTheSameIdString_AreNotFlagged()
        {
            // A BaseGraph and a CollectionEntry that happen to carry the exact same GUID string are never
            // compared to each other — the scan groups strictly per concrete type.
            var graph = CreateAt<BaseGraph>("GraphX");
            var entry = CreateAt<CollectionEntry>("EntryX");
            var so = new SerializedObject(entry);
            so.FindProperty("_id").stringValue = graph.GraphId;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(graph.GraphId, entry.Key, "setup: same id string, different types");

            Assert.AreEqual(0, StableIdDuplicateDetector.ScanAndFix(null));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ScanAndFix_NoDuplicates_IsANoOp()
        {
            CreateAt<BaseGraph>("Solo");
            Assert.AreEqual(0, StableIdDuplicateDetector.ScanAndFix(null));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
