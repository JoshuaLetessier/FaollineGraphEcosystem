using Faolline.GraphLocalization;
using NUnit.Framework;
using Faolline.GraphCore;
using Faolline.GraphDialogue;

namespace Faolline.GraphDialogue.Tests
{
    /// <summary>EditMode tests for the dialogue line node data type.</summary>
    public class DialogueLineNodeDataTests
    {
        [Test]
        public void NodeTypeId_IsDialogueLine()
        {
            Assert.AreEqual("graphdialogue/line", DialogueLineNodeData.NodeTypeId);
        }

        [Test]
        public void IsStatementNode_AndCarriesLineFields()
        {
            var node = new DialogueLineNodeData
            {
                Id = "n", NodeType = DialogueLineNodeData.NodeTypeId,
                SpeakerKey = "npc"
            };
            Assert.IsInstanceOf<StatementNodeData>(node);
            Assert.AreEqual("npc", node.SpeakerKey);
            Assert.AreEqual("neutral", node.ExpressionKey, "ExpressionKey defaults to 'neutral'.");
            // The localization key is derived from the node Id, not a stored field.
            Assert.AreEqual("line_n", DialogueLocalizationKeys.ForLine(node));
        }

        [Test]
        public void ExpressionKey_NullOrEmpty_FallsBackToNeutral()
        {
            var node = new DialogueLineNodeData { ExpressionKey = "happy" };
            Assert.AreEqual("happy", node.ExpressionKey);
            node.ExpressionKey = "";
            Assert.AreEqual("neutral", node.ExpressionKey);
        }
    }
}
