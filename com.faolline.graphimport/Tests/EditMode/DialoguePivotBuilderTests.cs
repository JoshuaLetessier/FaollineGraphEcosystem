using System.Linq;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class DialoguePivotBuilderTests
    {
        const string ValidJson = @"{
            ""dialogues"": [
                {
                    ""id"": ""DLG_001"",
                    ""name"": ""Test Dialogue"",
                    ""entryNodeId"": ""n1"",
                    ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""line"", ""speakerKey"": ""tsuki"", ""text"": ""Bonjour"", ""next"": ""n2"" },
                        { ""id"": ""n2"", ""kind"": ""choice"", ""options"": [
                            { ""id"": ""o1"", ""label"": ""Demander"", ""next"": ""n3"" },
                            { ""id"": ""o2"", ""label"": ""Partir"", ""next"": ""n4"" }
                        ] },
                        { ""id"": ""n3"", ""kind"": ""end"", ""reason"": ""Completed"", ""outcomeLabel"": ""asked"" },
                        { ""id"": ""n4"", ""kind"": ""end"", ""reason"": ""Completed"", ""outcomeLabel"": ""left"" }
                    ]
                }
            ]
        }";

        [Test]
        public void Build_ValidInterchange_ProducesCorrectPivotStructure()
        {
            var interchange = InterchangeDialogueSet.LoadFromJson(ValidJson);

            var dialogues = new DialoguePivotBuilder().Build(interchange);

            Assert.AreEqual(1, dialogues.Count);
            var dialogue = dialogues[0];
            Assert.AreEqual("DLG_001", dialogue.Id);
            Assert.AreEqual("n1", dialogue.EntryNodeId);
            Assert.AreEqual(4, dialogue.Nodes.Count);

            var line = (PivotLine)dialogue.Nodes["n1"];
            Assert.AreEqual("tsuki", line.SpeakerKey);
            Assert.AreEqual("Bonjour", line.Text);
            Assert.AreEqual("n2", line.Next);

            var choice = (PivotChoice)dialogue.Nodes["n2"];
            Assert.AreEqual(2, choice.Options.Count);
            Assert.AreEqual("n3", choice.Options[0].Next);
            Assert.AreEqual("o1", choice.Options[0].Id);
            Assert.AreEqual("o2", choice.Options[1].Id);

            var end = (PivotEnd)dialogue.Nodes["n3"];
            Assert.AreEqual("Completed", end.Reason);
            Assert.AreEqual("asked", end.OutcomeLabel);
        }

        [Test]
        public void Build_OptionWithoutId_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""Test"", ""entryNodeId"": ""c"", ""nodes"": [
                        { ""id"": ""c"", ""kind"": ""choice"", ""options"": [
                            { ""id"": ""o1"", ""label"": ""A"", ""next"": ""e"" },
                            { ""label"": ""B"", ""next"": ""e"" }
                        ] },
                        { ""id"": ""e"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var ex = Assert.Throws<DialogueStructureException>(() => new DialoguePivotBuilder().Build(interchange));
            Assert.AreEqual(DialogueStructureIssue.MissingOptionId, ex.Issue);
            Assert.AreEqual("DLG_001", ex.DialogueId);
            Assert.AreEqual("c", ex.NodeId);
        }

        [Test]
        public void Build_OptionIdReusedAcrossTwoChoicesOfTheSameDialogue_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""Test"", ""entryNodeId"": ""c1"", ""nodes"": [
                        { ""id"": ""c1"", ""kind"": ""choice"", ""options"": [ { ""id"": ""o1"", ""label"": ""A"", ""next"": ""c2"" } ] },
                        { ""id"": ""c2"", ""kind"": ""choice"", ""options"": [ { ""id"": ""o1"", ""label"": ""B"", ""next"": ""e"" } ] },
                        { ""id"": ""e"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var ex = Assert.Throws<DialogueStructureException>(() => new DialoguePivotBuilder().Build(interchange));
            Assert.AreEqual(DialogueStructureIssue.DuplicateOptionId, ex.Issue);
            Assert.AreEqual("c2", ex.NodeId);
            Assert.AreEqual("o1", ex.OptionId);
        }

        [Test]
        public void Build_SameOptionIdInTwoDifferentDialogues_IsAllowed()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_A"", ""name"": ""A"", ""entryNodeId"": ""c"", ""nodes"": [
                        { ""id"": ""c"", ""kind"": ""choice"", ""options"": [ { ""id"": ""o1"", ""label"": ""A"", ""next"": ""e"" } ] },
                        { ""id"": ""e"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] },
                    { ""id"": ""DLG_B"", ""name"": ""B"", ""entryNodeId"": ""c"", ""nodes"": [
                        { ""id"": ""c"", ""kind"": ""choice"", ""options"": [ { ""id"": ""o1"", ""label"": ""B"", ""next"": ""e"" } ] },
                        { ""id"": ""e"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            Assert.DoesNotThrow(() => new DialoguePivotBuilder().Build(interchange),
                "option ids only need to be unique within a dialogue — the generator namespaces them by dialogue id.");
        }

        [Test]
        public void Build_DanglingNextReference_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""Test"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""line"", ""speakerKey"": ""a"", ""text"": ""x"", ""next"": ""does_not_exist"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var ex = Assert.Throws<DialogueStructureException>(() => new DialoguePivotBuilder().Build(interchange));
            Assert.AreEqual("DLG_001", ex.DialogueId);
            Assert.AreEqual(DialogueStructureIssue.DanglingNext, ex.Issue);
        }

        [Test]
        public void Build_DuplicateNodeId_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""Test"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""end"", ""reason"": ""Completed"" },
                        { ""id"": ""n1"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var ex = Assert.Throws<DialogueStructureException>(() => new DialoguePivotBuilder().Build(interchange));
            Assert.AreEqual(DialogueStructureIssue.DuplicateNodeId, ex.Issue);
        }

        [Test]
        public void Build_EntryNodeIdNotFound_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""Test"", ""entryNodeId"": ""missing"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var ex = Assert.Throws<DialogueStructureException>(() => new DialoguePivotBuilder().Build(interchange));
            Assert.AreEqual(DialogueStructureIssue.InvalidEntryNode, ex.Issue);
        }

        [Test]
        public void Build_SubDialogueTarget_ResolvesByNameAcrossTheSet()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""First"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""subDialogue"", ""targetDialogue"": ""Second"" }
                    ] },
                    { ""id"": ""DLG_002"", ""name"": ""Second"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""end"", ""reason"": ""Completed"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var dialogues = new DialoguePivotBuilder().Build(interchange);

            var link = (PivotSubDialogueLink)dialogues.Single(d => d.Id == "DLG_001").Nodes["n1"];
            Assert.AreEqual("DLG_002", link.TargetDialogueRef.TargetId);
        }

        [Test]
        public void Build_SubDialogueTargetNotFound_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""First"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""subDialogue"", ""targetDialogue"": ""DoesNotExist"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            var ex = Assert.Throws<DialogueReferenceException>(() => new DialoguePivotBuilder().Build(interchange));
            Assert.AreEqual(DialogueReferenceReason.NotFound, ex.Reason);
        }

        [Test]
        public void Build_SubDialogueCycle_Throws()
        {
            const string json = @"{
                ""dialogues"": [
                    { ""id"": ""DLG_001"", ""name"": ""First"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""subDialogue"", ""targetDialogue"": ""DLG_002"" }
                    ] },
                    { ""id"": ""DLG_002"", ""name"": ""Second"", ""entryNodeId"": ""n1"", ""nodes"": [
                        { ""id"": ""n1"", ""kind"": ""subDialogue"", ""targetDialogue"": ""DLG_001"" }
                    ] }
                ]
            }";
            var interchange = InterchangeDialogueSet.LoadFromJson(json);

            Assert.Throws<DialogueCycleException>(() => new DialoguePivotBuilder().Build(interchange));
        }
    }
}
