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
    /// <see cref="CollectionEntry"/>, <see cref="CollectionName"/>).
    /// </summary>
    public class StableIdDuplicateDetectorTests
    {
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

        // ── CollectionName ────────────────────────────────────────────────

        [Test]
        public void CollectionName_ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var a = CreateAt<CollectionName>("ColA");
            var b = CreateAt<CollectionName>("ColB");
            ForceSameId(a, b, "_id");
            var originalId = a.Key;

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate CollectionName id"));
            int fixedCount = StableIdDuplicateDetector.ScanAndFix(null);

            Assert.AreEqual(1, fixedCount);
            Assert.AreNotEqual(a.Key, b.Key);
            Assert.IsTrue(a.Key == originalId || b.Key == originalId);
        }

        // ── SignalName ────────────────────────────────────────────────────

        [Test]
        public void SignalName_ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var a = CreateAt<SignalName>("SigA");
            var b = CreateAt<SignalName>("SigB");
            ForceSameId(a, b, "_id");
            var originalId = a.Key;

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate SignalName id"));
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
