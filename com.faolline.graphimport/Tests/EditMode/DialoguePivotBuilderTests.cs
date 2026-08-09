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
                            { ""label"": ""Demander"", ""next"": ""n3"" },
                            { ""label"": ""Partir"", ""next"": ""n4"" }
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

            var end = (PivotEnd)dialogue.Nodes["n3"];
            Assert.AreEqual("Completed", end.Reason);
            Assert.AreEqual("asked", end.OutcomeLabel);
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
