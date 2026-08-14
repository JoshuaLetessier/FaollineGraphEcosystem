using System.Collections.Generic;
using System.Linq;
using Faolline.GraphCore;
using Faolline.GraphGameFlow;
using Faolline.GraphQuest;
using NUnit.Framework;
using UnityEditor;

namespace Faolline.GraphImport.Editor.Tests
{
    public class AssetGenerationTests
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

        static PivotQuest MakeLinearQuest()
        {
            var quest = new PivotQuest("Q_001", "Rencontrer Tsuki",
                new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<PivotReference>>());
            quest.Steps = new List<PivotStep>
            {
                new PivotStep("S_000", quest, 0, new PivotReference("Puzzles", "PZ_000"), null)
            };
            return quest;
        }

        static PivotQuest MakeBranchingQuest()
        {
            var quest = new PivotQuest("Q_001", "Rencontrer Tsuki",
                new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<PivotReference>>());
            var victoire = new PivotStep("S_004", quest, 4, new PivotReference("Dialogues", "DLG_006"), "victoire_jd");
            var defaite = new PivotStep("S_005", quest, 4, new PivotReference("Dialogues", "DLG_007"), "defaite_jd");
            var suite = new PivotStep("S_006", quest, 5, new PivotReference("Puzzles", "PZ_001"), null);
            quest.Steps = new List<PivotStep> { victoire, defaite, suite };
            quest.Branches = new List<PivotBranch> { new PivotBranch(quest, 4, new List<PivotStep> { victoire, defaite }) };
            return quest;
        }

        [Test]
        public void QuestAssetGenerator_BuildsQuestGraphAssetWithOneObjectivePerStep()
        {
            var quest = MakeLinearQuest();
            var entry = new PlanEntry("quest:Q_001", PlanEntryKind.QuestAsset, ScratchFolder + "/Q_001.asset", "Q_001", quest);

            new QuestAssetGenerator().Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<QuestGraph>(entry.ProposedPath);
            Assert.IsNotNull(graph);
            Assert.AreEqual("Q_001", graph.QuestId);
            Assert.AreEqual(1, graph.Nodes.Count);
        }

        [Test]
        public void FlowAssetGenerator_LinearQuest_BuildsStartToEndChain()
        {
            var quest = MakeLinearQuest();
            var entry = new PlanEntry("flow:Q_001", PlanEntryKind.FlowAsset, ScratchFolder + "/Q_001_Flow.asset", "Q_001", quest);

            new FlowAssetGenerator().Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<GameFlowGraph>(entry.ProposedPath);
            Assert.IsNotNull(graph);
            // Start + 1 SubGraph step + End
            Assert.AreEqual(3, graph.Nodes.Count);
            Assert.AreEqual(2, graph.Edges.Count);
            Assert.IsNotNull(graph.EntryNodeId);
        }

        [Test]
        public void FlowAssetGenerator_BranchingQuest_BuildsChoiceNodeWithReconvergence()
        {
            var quest = MakeBranchingQuest();
            var entry = new PlanEntry("flow:Q_001", PlanEntryKind.FlowAsset, ScratchFolder + "/Q_001_Flow.asset", "Q_001", quest);

            new FlowAssetGenerator().Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<GameFlowGraph>(entry.ProposedPath);
            Assert.IsNotNull(graph);
            // Start, Choice, 2 branch SubGraph nodes, 1 reconverging SubGraph node, End = 6
            Assert.AreEqual(6, graph.Nodes.Count);

            var choiceNode = System.Linq.Enumerable.OfType<ChoiceNodeData>(graph.Nodes).GetEnumerator();
            Assert.IsTrue(choiceNode.MoveNext());
            Assert.AreEqual(2, choiceNode.Current.Choices.Count);

            // Both branch outputs must converge onto the same next node (S_006) — 2 edges into it.
            var edgesIntoConverger = 0;
            foreach (var edge in graph.Edges)
                foreach (var node in graph.Nodes)
                    if (node.Id == edge.ToNodeId && node is SubGraphNodeData subGraphNodeData && subGraphNodeData.Title == "S_006")
                        edgesIntoConverger++;

            Assert.AreEqual(2, edgesIntoConverger);
        }

        [Test]
        public void FlowAssetGenerator_ContentFieldsProvided_TitlesSubGraphNodeWithResolvedContentName()
        {
            var quest = MakeLinearQuest(); // step S_000 -> Puzzles:PZ_000
            var entry = new PlanEntry("flow:Q_001", PlanEntryKind.FlowAsset, ScratchFolder + "/Q_001_Flow.asset", "Q_001", quest);
            var contentFieldsById = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["PZ_000"] = new Dictionary<string, string> { ["name"] = "jeu de dé" }
            };

            new FlowAssetGenerator(contentFieldsById: contentFieldsById).Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<GameFlowGraph>(entry.ProposedPath);
            var subGraphNode = System.Linq.Enumerable.OfType<SubGraphNodeData>(graph.Nodes).Single();
            Assert.AreEqual("jeu de dé", subGraphNode.Title,
                "the step's title is the resolved content's own declared 'name' field, not the raw step id");
        }

        [Test]
        public void FlowAssetGenerator_ContentFieldsMissingForStep_FallsBackToStepId()
        {
            var quest = MakeLinearQuest(); // step S_000 -> Puzzles:PZ_000, not in the lookup below
            var entry = new PlanEntry("flow:Q_001", PlanEntryKind.FlowAsset, ScratchFolder + "/Q_001_Flow.asset", "Q_001", quest);

            new FlowAssetGenerator(contentFieldsById: new Dictionary<string, IReadOnlyDictionary<string, string>>()).Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<GameFlowGraph>(entry.ProposedPath);
            var subGraphNode = System.Linq.Enumerable.OfType<SubGraphNodeData>(graph.Nodes).Single();
            Assert.AreEqual("S_000", subGraphNode.Title,
                "no content-table 'name' field known for this step's content -> falls back to the raw step id, never throws");
        }
    }
}
