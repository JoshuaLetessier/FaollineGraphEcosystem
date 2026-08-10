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

        // Real dialogue-studio export (DLG_001 "Parler au tavernier") — dialogue.id matches the
        // production sheet's own simplistic id ("DLG_001", required for cross-resolution with quest
        // data), node ids are dialogue-studio's own nanoid ("IO1R3hKAREHK", "zVJgGpqd19rh").
        const string RealDialogueJson = @"{
            ""dialogues"": [
                {
                    ""id"": ""DLG_001"",
                    ""name"": ""Parler au tavernier"",
                    ""entryNodeId"": ""IO1R3hKAREHK"",
                    ""nodes"": [
                        { ""id"": ""IO1R3hKAREHK"", ""kind"": ""line"", ""speakerKey"": ""PNJ_000_Charlie"", ""text"": ""ba"", ""next"": ""zVJgGpqd19rh"" },
                        { ""id"": ""zVJgGpqd19rh"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ]
                }
            ]
        }";

        [Test]
        public void Generate_RealExport_NodeIdsAreStableAndComposedFromDialogueAndPivotNodeId()
        {
            var dialogues = new DialoguePivotBuilder().Build(InterchangeDialogueSet.LoadFromJson(RealDialogueJson));
            var dialogue = dialogues.Single();
            var entry = new PlanEntry("dialogue:DLG_001", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_001.asset", "DLG_001", dialogue);

            new DialogueAssetGenerator(new NullProjectAssetResolver()).Generate(entry);

            var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(entry.ProposedPath);
            var lineNode = graph.Nodes.OfType<DialogueLineNodeData>().Single();
            var endNode = graph.Nodes.OfType<Faolline.GraphCore.EndNodeData>().Single();

            Assert.AreEqual("DLG_001_IO1R3hKAREHK", lineNode.Id);
            Assert.AreEqual("DLG_001_zVJgGpqd19rh", endNode.Id);
            Assert.AreEqual(graph.EntryNodeId, lineNode.Id);

            // The whole point: a line's localization key is now predictable ahead of time from data
            // dialogue-studio already has (its own dialogue id + node id), not only knowable after
            // this generator has already run and assigned a fresh random GUID.
            var expectedKey = DialogueLocalizationKeys.ForLine(lineNode);
            Assert.AreEqual("line_DLG_001_IO1R3hKAREHK", expectedKey);
            Assert.AreEqual(expectedKey, DialogueLocalizationKeys.LinePrefix + DialogueAssetGenerator.StableNodeId(dialogue, "IO1R3hKAREHK"));
        }

        [Test]
        public void Generate_TwoDifferentDialoguesSharingARawNodeId_ProduceDifferentStableIds()
        {
            var dialogueA = new PivotDialogue("DLG_A", "A", "n1",
                new Dictionary<string, PivotDialogueNode> { ["n1"] = new PivotEnd("n1", "Completed", null) });
            var dialogueB = new PivotDialogue("DLG_B", "B", "n1",
                new Dictionary<string, PivotDialogueNode> { ["n1"] = new PivotEnd("n1", "Completed", null) });

            var entryA = new PlanEntry("dialogue:DLG_A", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_A.asset", "DLG_A", dialogueA);
            var entryB = new PlanEntry("dialogue:DLG_B", PlanEntryKind.DialogueAsset, ScratchFolder + "/DLG_B.asset", "DLG_B", dialogueB);

            var generator = new DialogueAssetGenerator(new NullProjectAssetResolver());
            generator.Generate(entryA);
            generator.Generate(entryB);

            var endA = AssetDatabase.LoadAssetAtPath<DialogueGraph>(entryA.ProposedPath).Nodes.OfType<Faolline.GraphCore.EndNodeData>().Single();
            var endB = AssetDatabase.LoadAssetAtPath<DialogueGraph>(entryB.ProposedPath).Nodes.OfType<Faolline.GraphCore.EndNodeData>().Single();

            Assert.AreNotEqual(endA.Id, endB.Id, "same raw node id 'n1' in two different dialogues must not collide once namespaced by dialogue id");
            Assert.AreEqual("DLG_A_n1", endA.Id);
            Assert.AreEqual("DLG_B_n1", endB.Id);
        }
    }
}
