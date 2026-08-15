using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor.Tests
{
    public class PlanConflictDetectorTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";
        const string ExistingAssetPath = ScratchFolder + "/Existing.asset";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");

            var obj = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(obj, ExistingAssetPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ScratchFolder);
        }

        static PlanEntry Entry(string logicalId, string path) =>
            new PlanEntry(logicalId, PlanEntryKind.DialogueAsset, path, logicalId, null);

        [Test]
        public void Detect_PathAlreadyHoldsAnAsset_IsConflict()
        {
            var plan = new GenerationPlan(new List<PlanEntry> { Entry("dialogue:DLG_001", ExistingAssetPath) });

            var report = PlanConflictDetector.Detect(plan);

            Assert.IsFalse(report.IsClean);
            Assert.AreEqual(ConflictReason.AlreadyExists, report.Conflicts[0].Reason);
        }

        [Test]
        public void Detect_TwoEntriesSamePath_BothAreConflicts()
        {
            var plan = new GenerationPlan(new List<PlanEntry>
            {
                Entry("dialogue:DLG_001", ScratchFolder + "/New.asset"),
                Entry("dialogue:DLG_002", ScratchFolder + "/New.asset")
            });

            var report = PlanConflictDetector.Detect(plan);

            Assert.AreEqual(2, report.Conflicts.Count);
            Assert.IsTrue(report.Conflicts[0].Reason == ConflictReason.DuplicateTargetWithinPlan);
            Assert.IsTrue(report.Conflicts[1].Reason == ConflictReason.DuplicateTargetWithinPlan);
        }

        [Test]
        public void Detect_NoCollisions_IsClean()
        {
            var plan = new GenerationPlan(new List<PlanEntry> { Entry("dialogue:DLG_001", ScratchFolder + "/BrandNew.asset") });

            var report = PlanConflictDetector.Detect(plan);

            Assert.IsTrue(report.IsClean);
            Assert.AreEqual(0, report.Conflicts.Count);
        }
    }
}
