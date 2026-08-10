using System.Collections.Generic;
using System.Linq;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;

namespace Faolline.GraphImport.Editor.Tests
{
    /// <summary>
    /// Reproduces (and locks down the fix for) the two CRITICAL findings from the second real-data
    /// dogfood round on branch 050: a quest step's content ref can target a dialogue generated in the
    /// SAME combined run — this is the whole point of GraphImportWindow merging both plans — but that
    /// requires (1) ProjectAssetResolver to not crash when a quest has both a QuestAsset and a
    /// FlowAsset entry sharing one SourcePivotId, and (2) the combined plan to apply dialogues before
    /// quest/flow, since only quest/flow steps ever reference dialogues, never the reverse.
    /// </summary>
    public class CombinedQuestDialogueResolutionTests
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
        public void QuestStepContentRef_TargetingADialogueInTheSameCombinedPlan_ResolvesWhenDialoguesApplyFirst()
        {
            // The quest: one step whose content targets a dialogue by pivot id "DLG_006".
            var quest = new PivotQuest("Q_001", "Rencontrer Tsuki",
                new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<PivotReference>>());
            quest.Steps = new List<PivotStep> { new PivotStep("S_000", quest, 0, new PivotReference("Dialogues", "DLG_006"), null) };

            var dialogue = new PivotDialogue("DLG_006", "Victoire", "n1",
                new Dictionary<string, PivotDialogueNode> { ["n1"] = new PivotEnd("n1", "Completed", null) });

            var questEntry = new PlanEntry("quest:Q_001", PlanEntryKind.QuestAsset, ScratchFolder + "/Q_001.asset", "Q_001", quest);
            var flowEntry = new PlanEntry("flow:Q_001", PlanEntryKind.FlowAsset, ScratchFolder + "/Q_001_Flow.asset", "Q_001", quest);
            var dialogueEntry = new PlanEntry("dialogue:DLG_006", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_006.asset", "DLG_006", dialogue);

            // Mirrors GraphImportWindow's fixed combining order: dialogues before quest/flow.
            var plan = new GenerationPlan(new List<PlanEntry> { dialogueEntry, questEntry, flowEntry });

            // Fix #1: must not throw despite questEntry/flowEntry sharing SourcePivotId "Q_001".
            var resolver = new ProjectAssetResolver(plan, ScratchFolder + "/Speakers");

            var report = PlanConflictDetector.Detect(plan);
            var generators = new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.QuestAsset] = new QuestAssetGenerator(),
                [PlanEntryKind.FlowAsset] = new FlowAssetGenerator(resolver),
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
            var result = PlanApplier.Apply(plan, report, generators);

            Assert.IsTrue(result.IsClean, string.Join("; ", result.Failures.Select(f => f.Exception.Message)));

            var flowGraph = AssetDatabase.LoadAssetAtPath<Faolline.GraphGameFlow.GameFlowGraph>(flowEntry.ProposedPath);
            var subNode = flowGraph.Nodes.OfType<SubGraphNodeData>().Single();
            var dialogueAsset = AssetDatabase.LoadAssetAtPath<DialogueGraph>(dialogueEntry.ProposedPath);
            Assert.AreEqual(dialogueAsset, subNode.TargetGraph,
                "fix #2: the flow step's content ref must resolve to the dialogue applied earlier in the same plan");
        }
    }
}
