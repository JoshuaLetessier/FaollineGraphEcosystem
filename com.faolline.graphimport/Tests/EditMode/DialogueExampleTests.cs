using System.IO;
using System.Linq;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor.Tests
{
    /// <summary>
    /// Runs the shipped sample (Samples/DialogueExample/dialogues.json) end to end through
    /// interchange -> pivot -> plan -> apply, mirroring 048's CryptiqueExampleTests — a regression
    /// guard against the sample drifting from the generator, and quickstart.md's executable form (T023).
    /// </summary>
    public class DialogueExampleTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";
        static string SampleRoot => Path.Combine(Application.dataPath, "FaollineGraphEcosystem", "com.faolline.graphimport", "Samples", "DialogueExample");

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ScratchFolder);

        [Test]
        public void Sample_EndToEnd_GeneratesBothDialoguesWithASubDialogueLink()
        {
            var json = File.ReadAllText(Path.Combine(SampleRoot, "dialogues.json"));
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var dialogues = new DialoguePivotBuilder().Build(interchange);
            Assert.AreEqual(2, dialogues.Count);

            var pathResolver = new TemplatePathResolver(new System.Collections.Generic.Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = ScratchFolder + "/{id}.asset"
            });
            var plan = new PlanBuilder(pathResolver).BuildDialogues(dialogues);
            Assert.AreEqual(2, plan.Entries.Count);

            var report = PlanConflictDetector.Detect(plan);
            Assert.IsTrue(report.IsClean);

            var generators = new System.Collections.Generic.Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(new NullProjectAssetResolver())
            };
            var result = PlanApplier.Apply(plan, report, generators);
            Assert.IsTrue(result.IsClean);
            Assert.AreEqual(2, result.Created.Count);

            var victoire = AssetDatabase.LoadAssetAtPath<DialogueGraph>(ScratchFolder + "/DLG_006.asset");
            Assert.IsNotNull(victoire);
            var subNode = victoire.Nodes.OfType<Faolline.GraphCore.SubGraphNodeData>().Single();
            Assert.IsNull(subNode.TargetGraph); // NullProjectAssetResolver — documented-safe unresolved state
        }
    }
}
