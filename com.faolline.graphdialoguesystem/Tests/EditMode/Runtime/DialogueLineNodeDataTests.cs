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
                SpeakerKey = "npc", TextKey = "dlg.hi"
            };
            Assert.IsInstanceOf<StatementNodeData>(node);
            Assert.AreEqual("npc", node.SpeakerKey);
            Assert.AreEqual("dlg.hi", node.TextKey);
            Assert.AreEqual("neutral", node.ExpressionKey, "ExpressionKey defaults to 'neutral'.");
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
