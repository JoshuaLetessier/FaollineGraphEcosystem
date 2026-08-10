using System.Collections.Generic;
using System.Linq;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;

namespace Faolline.GraphImport.Editor.Tests
{
    /// <summary>
    /// Exercises CombinedImportBatch.BuildAndApply directly (the pure logic extracted out of
    /// RunInternal, which itself can't be unit tested — it calls EditorApplication.Exit).
    /// </summary>
    public class CombinedImportBatchTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ScratchFolder);

        [Test]
        public void BuildAndApply_QuestStepReferencingADialogueInTheSameRun_Resolves()
        {
            var quest = new PivotQuest("Q_001", "Rencontrer Tsuki",
                new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<PivotReference>>());
            quest.Steps = new List<PivotStep> { new PivotStep("S_000", quest, 0, new PivotReference("Dialogues", "DLG_006"), null) };

            var dialogue = new PivotDialogue("DLG_006", "Victoire", "n1",
                new Dictionary<string, PivotDialogueNode> { ["n1"] = new PivotEnd("n1", "Completed", null) });

            var questEntry = new PlanEntry("quest:Q_001", PlanEntryKind.QuestAsset, ScratchFolder + "/Q_001.asset", "Q_001", quest);
            var flowEntry = new PlanEntry("flow:Q_001", PlanEntryKind.FlowAsset, ScratchFolder + "/Q_001_Flow.asset", "Q_001", quest);
            var dialogueEntry = new PlanEntry("dialogue:DLG_006", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_006.asset", "DLG_006", dialogue);

            var result = CombinedImportBatch.BuildAndApply(
                new List<PlanEntry> { dialogueEntry },
                new List<PlanEntry> { questEntry, flowEntry },
                ScratchFolder + "/Speakers");

            Assert.IsTrue(result.IsClean, string.Join("; ", result.Apply.Failures.Select(f => f.Exception.Message)));

            var flowGraph = AssetDatabase.LoadAssetAtPath<Faolline.GraphGameFlow.GameFlowGraph>(flowEntry.ProposedPath);
            var subNode = flowGraph.Nodes.OfType<SubGraphNodeData>().Single();
            var dialogueAsset = AssetDatabase.LoadAssetAtPath<DialogueGraph>(dialogueEntry.ProposedPath);
            Assert.AreEqual(dialogueAsset, subNode.TargetGraph);
        }

        [Test]
        public void BuildAndApply_NoDialogueEntries_QuestFlowOnlyRunStillSucceeds()
        {
            var quest = new PivotQuest("Q_002", "Aller à l'orphelinat",
                new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<PivotReference>>());
            var questEntry = new PlanEntry("quest:Q_002", PlanEntryKind.QuestAsset, ScratchFolder + "/Q_002.asset", "Q_002", quest);

            var result = CombinedImportBatch.BuildAndApply(
                new List<PlanEntry>(),
                new List<PlanEntry> { questEntry },
                ScratchFolder + "/Speakers");

            Assert.IsTrue(result.IsClean);
            Assert.AreEqual(1, result.Apply.Created.Count);
        }
    }
}
