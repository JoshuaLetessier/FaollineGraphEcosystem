using System.Collections.Generic;
using NUnit.Framework;

namespace Faolline.GraphImport.Tests
{
    public class TemplatePathResolverTests
    {
        static PivotDialogue MakeDialogue(string id = "DLG_006", string name = "Victoire") =>
            new PivotDialogue(id, name, "n1", new Dictionary<string, PivotDialogueNode>());

        [Test]
        public void Resolve_Dialogue_SubstitutesNameAndIdTokens()
        {
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/Dialogues/{name}_{id}.asset"
            });

            var path = resolver.Resolve(PlanEntryKind.DialogueAsset, MakeDialogue());

            Assert.AreEqual("Assets/Graphs/Dialogues/Victoire_DLG_006.asset", path);
        }

        [Test]
        public void Resolve_Dialogue_UnknownToken_Throws()
        {
            var resolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = "Assets/Graphs/{unknownToken}/{name}.asset"
            });

            Assert.Throws<System.InvalidOperationException>(() => resolver.Resolve(PlanEntryKind.DialogueAsset, MakeDialogue()));
        }
    }
}
