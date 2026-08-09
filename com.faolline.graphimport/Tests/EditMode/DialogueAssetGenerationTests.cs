using System.Collections.Generic;
using System.Linq;
using Faolline.GraphCore;
using Faolline.GraphDialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor.Tests
{
    public class DialogueAssetGenerationTests
    {
        const string ScratchFolder = "Assets/GraphImportTestScratch";

        sealed class StubResolver : IProjectAssetResolver
        {
            public BaseGraph Graph;
            public BaseGraph ResolveGraph(string targetTable, string targetId) => Graph;
            public Speaker ResolveSpeaker(string speakerKey) => null;
        }

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(ScratchFolder))
                AssetDatabase.CreateFolder("Assets", "GraphImportTestScratch");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ScratchFolder);

        static PivotDialogue MakeLineChoiceEndDialogue()
        {
            var nodes = new Dictionary<string, PivotDialogueNode>
            {
                ["n1"] = new PivotLine("n1", "tsuki", "Bonjour", "n2"),
                ["n2"] = new PivotChoice("n2", new List<PivotChoiceOption>
                {
                    new PivotChoiceOption("Demander", "n3"),
                    new PivotChoiceOption("Partir", "n4")
                }),
                ["n3"] = new PivotEnd("n3", "Completed", "asked"),
                ["n4"] = new PivotEnd("n4", "Completed", "left")
            };
            return new PivotDialogue("DLG_001", "Test Dialogue", "n1", nodes);
        }

        [Test]
        public void Generate_LineChoiceEnd_BuildsCorrectDialogueGraph()
        {
            var dialogue = MakeLineChoiceEndDialogue();
            var entry = new PlanEntry("dialogue:DLG_001", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_001.asset", "DLG_001", dialogue);

            new DialogueAssetGenerator(new NullProjectAssetResolver()).Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(entry.ProposedPath);
            Assert.IsNotNull(graph);
            Assert.AreEqual(4, graph.Nodes.Count);
            Assert.IsNotNull(graph.EntryNodeId);

            var lineNode = graph.Nodes.OfType<DialogueLineNodeData>().Single();
            Assert.AreEqual("tsuki", lineNode.SpeakerKey);
            Assert.AreEqual("Bonjour", lineNode.Title);

            var choiceNode = graph.Nodes.OfType<Faolline.GraphCore.ChoiceNodeData>().Single();
            Assert.AreEqual(2, choiceNode.Choices.Count);

            Assert.AreEqual(2, graph.Nodes.OfType<Faolline.GraphCore.EndNodeData>().Count());
        }

        [Test]
        public void Generate_UnresolvedSubDialogue_ProducesNullTargetGraph_NotAnException()
        {
            var nodes = new Dictionary<string, PivotDialogueNode>
            {
                ["n1"] = new PivotSubDialogueLink("n1", new PivotReference("Dialogues", "DLG_999"), "n2"),
                ["n2"] = new PivotEnd("n2", "Completed", null)
            };
            var dialogue = new PivotDialogue("DLG_001", "Test", "n1", nodes);
            var entry = new PlanEntry("dialogue:DLG_001", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_001.asset", "DLG_001", dialogue);

            Assert.DoesNotThrow(() => new DialogueAssetGenerator(new NullProjectAssetResolver()).Generate(entry));

            var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(entry.ProposedPath);
            var subNode = graph.Nodes.OfType<SubGraphNodeData>().Single();
            Assert.IsNull(subNode.TargetGraph);
        }

        [Test]
        public void Generate_ResolvedSubDialogue_SetsTargetGraph()
        {
            var target = ScriptableObject.CreateInstance<DialogueGraph>();
            AssetDatabase.CreateAsset(target, ScratchFolder + "/Target.asset");

            var nodes = new Dictionary<string, PivotDialogueNode>
            {
                ["n1"] = new PivotSubDialogueLink("n1", new PivotReference("Dialogues", "DLG_999"), "n2"),
                ["n2"] = new PivotEnd("n2", "Completed", null)
            };
            var dialogue = new PivotDialogue("DLG_001", "Test", "n1", nodes);
            var entry = new PlanEntry("dialogue:DLG_001", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_001.asset", "DLG_001", dialogue);

            new DialogueAssetGenerator(new StubResolver { Graph = target }).Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(entry.ProposedPath);
            var subNode = graph.Nodes.OfType<SubGraphNodeData>().Single();
            Assert.AreEqual(target, subNode.TargetGraph);
        }
    }
}
