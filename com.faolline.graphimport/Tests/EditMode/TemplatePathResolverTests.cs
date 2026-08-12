using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class TemplatePathResolverTests
    {
        static PivotQuest MakeQuest() => new PivotQuest(
            "Q_001", "Rencontrer Tsuki",
            new Dictionary<string, string> { ["chapter"] = "Everfrost" },
            new Dictionary<string, IReadOnlyList<PivotReference>>());

        [Test]
        public void Resolve_SubstitutesNameIdAndFieldTokens()
        {
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = "Assets/Graphs/{chapter}/Quests/{name}_{id}.asset"
            });

            var path = resolver.Resolve(PlanEntryKind.QuestAsset, MakeQuest());

            Assert.AreEqual("Assets/Graphs/Everfrost/Quests/Rencontrer Tsuki_Q_001.asset", path);
        }

        [Test]
        public void Resolve_DifferentKindsUseDifferentTemplates()
        {
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = "Assets/Graphs/{chapter}/Quests/{name}.asset",
                [PlanEntryKind.FlowAsset] = "Assets/Graphs/{chapter}/GameFlow/{name}.asset"
            });

            var quest = MakeQuest();

            Assert.AreEqual("Assets/Graphs/Everfrost/Quests/Rencontrer Tsuki.asset", resolver.Resolve(PlanEntryKind.QuestAsset, quest));
            Assert.AreEqual("Assets/Graphs/Everfrost/GameFlow/Rencontrer Tsuki.asset", resolver.Resolve(PlanEntryKind.FlowAsset, quest));
        }

        [Test]
        public void Resolve_UnknownToken_Throws()
        {
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = "Assets/Graphs/{unknownToken}/{name}.asset"
            });

            Assert.Throws<System.InvalidOperationException>(() => resolver.Resolve(PlanEntryKind.QuestAsset, MakeQuest()));
        }

        static PivotDialogue MakeDialogue(string id = "DLG_006", string name = "Victoire") =>
            new PivotDialogue(id, name, "n1", new Dictionary<string, PivotDialogueNode>());

        [Test]
        public void Resolve_Dialogue_SubstitutesContentTableFieldToken_WhenLookupProvided()
        {
            var contentFieldsById = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["DLG_006"] = new Dictionary<string, string> { ["chapter"] = "Everfrost" }
            };
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/{chapter}/Dialogues/{name}_{id}.asset"
            }, contentFieldsById);

            var path = resolver.Resolve(PlanEntryKind.DialogueAsset, MakeDialogue());

            Assert.AreEqual("Assets/Graphs/Everfrost/Dialogues/Victoire_DLG_006.asset", path);
        }

        [Test]
        public void Resolve_Dialogue_UnknownDialogueIdInLookup_ThrowsOnFieldToken()
        {
            var contentFieldsById = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["DLG_OTHER"] = new Dictionary<string, string> { ["chapter"] = "Ashwake" }
            };
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/{chapter}/{name}.asset"
            }, contentFieldsById);

            Assert.Throws<System.InvalidOperationException>(() => resolver.Resolve(PlanEntryKind.DialogueAsset, MakeDialogue()));
        }

        [Test]
        public void Resolve_Dialogue_NoLookupGiven_StillSupportsNameAndId()
        {
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/Dialogues/{name}_{id}.asset"
            });

            var path = resolver.Resolve(PlanEntryKind.DialogueAsset, MakeDialogue());

            Assert.AreEqual("Assets/Graphs/Dialogues/Victoire_DLG_006.asset", path);
        }
    }
}
