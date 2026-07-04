using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;

namespace Faolline.GraphCore.Tests
{
    /// <summary>
    /// Duplicating a graph asset copies its serialized GraphId (OnEnable only assigns when empty) — the
    /// detector must find the shared id and regenerate the duplicate's, keeping the original's intact.
    /// </summary>
    public class GraphIdDuplicateDetectorTests
    {
        private const string TempFolder = "Assets/Temp_GraphIdDetectorTest";
        private string _pathA, _pathB;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "Temp_GraphIdDetectorTest");
            _pathA = TempFolder + "/GraphA.asset";
            _pathB = TempFolder + "/GraphB.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BaseGraph>(), _pathA);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BaseGraph>(), _pathB);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TempFolder);

        // Simulates the Ctrl+D outcome: force B to carry A's id (in memory — no save, so the import
        // postprocessor does not pre-fix it and the test exercises ScanAndFix deterministically).
        private (BaseGraph a, BaseGraph b) MakeDuplicates()
        {
            var a = AssetDatabase.LoadAssetAtPath<BaseGraph>(_pathA);
            var b = AssetDatabase.LoadAssetAtPath<BaseGraph>(_pathB);
            var so = new SerializedObject(b);
            so.FindProperty("_graphId").stringValue = a.GraphId;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(a.GraphId, b.GraphId, "setup: the two assets share one GraphId");
            return (a, b);
        }

        [Test]
        public void ScanAndFix_RegeneratesOneDuplicate_KeepsTheOther()
        {
            var (a, b) = MakeDuplicates();
            var originalId = a.GraphId;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate GraphId"));
            int fixedCount = GraphIdDuplicateDetector.ScanAndFix(null);

            Assert.AreEqual(1, fixedCount);
            Assert.AreNotEqual(a.GraphId, b.GraphId, "the duplicate pair must end up with distinct ids");
            Assert.IsTrue(a.GraphId == originalId || b.GraphId == originalId,
                "exactly one of the two keeps the original id");
        }

        [Test]
        public void ScanAndFix_NoDuplicates_IsANoOp()
        {
            Assert.AreEqual(0, GraphIdDuplicateDetector.ScanAndFix(null));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ScanAndFix_PreferRegenerate_TheImportedCopyLosesTheTie()
        {
            var (a, b) = MakeDuplicates();
            var originalId = a.GraphId;

            // B is the "just imported" copy → B must be the one regenerated, A keeps its id.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate GraphId"));
            GraphIdDuplicateDetector.ScanAndFix(new HashSet<string> { _pathB });

            Assert.AreEqual(originalId, a.GraphId, "the pre-existing asset keeps its id");
            Assert.AreNotEqual(originalId, b.GraphId, "the imported copy gets a fresh id");
        }
    }
}
